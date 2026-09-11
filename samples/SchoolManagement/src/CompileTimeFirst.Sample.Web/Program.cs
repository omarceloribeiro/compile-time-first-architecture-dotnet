using CompileTimeFirst.Sample.Application;
using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Application.Grades;
using CompileTimeFirst.Sample.Application.QuestionOptions;
using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Application.Subjects;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.Web.Client;
using CompileTimeFirst.Sample.Web.Client.Pages.AutoSubjects;
using CompileTimeFirst.Sample.Web.Components;
using CompileTimeFirst.Sample.Web.Services;
using CompileTimeFirst.Sample.Web.OData;
using CompileTimeFirst.Validation;
using Microsoft.AspNetCore.OData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((_, options) =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// SQLite in-memory, kept alive by one open connection for the process lifetime. A relational
// provider is required here: the composite foreign keys that make a cross-tenant reference
// impossible are only enforced by a database that enforces foreign keys at all.
var connection = new SqliteConnection("Filename=:memory:");
connection.Open();

// Scoped factories, so the tenant accessor can be injected into the factory instead of into the
// context. See TenantDbContextFactories.
builder.Services.AddDbContextFactory<SchoolDbContext, TenantSchoolDbContextFactory>(
    options => options.UseSqlite(connection), ServiceLifetime.Scoped);
RemoveDirectContextRegistration<SchoolDbContext>(builder.Services);

builder.Services.AddDbContextFactory<ReadOnlySchoolDbContext, TenantReadOnlySchoolDbContextFactory>(
    options => options.UseSqlite(connection), ServiceLifetime.Scoped);
RemoveDirectContextRegistration<ReadOnlySchoolDbContext>(builder.Services);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CurrentTenantSelection>();
builder.Services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentTenantSelection>());
builder.Services.AddScoped<IReadSchoolDbFactory, ReadSchoolDbFactory>();
builder.Services.AddScoped<IReadProviderInfo>(provider =>
    (IReadProviderInfo)provider.GetRequiredService<IReadSchoolDbFactory>());
builder.Services.AddScoped<IReadQueryExecutor, EfReadQueryExecutor>();
builder.Services.AddScoped<SchoolODataReadScope>();

builder.Services.AddScoped<ICreateSubjectUseCase, CreateSubjectUseCase>();
builder.Services.AddScoped<ICreateGradeUseCase, CreateGradeUseCase>();
builder.Services.AddScoped<ICreateQuestionUseCase, CreateQuestionUseCase>();
builder.Services.AddScoped<ICreateQuestionOptionUseCase, CreateQuestionOptionUseCase>();
builder.Services.AddScoped<IGetSchoolDashboardUseCase, GetSchoolDashboardUseCase>();
builder.Services.AddScoped<IExportQuestionsUseCase, ExportQuestionsUseCase>();


builder.Services
    .AddControllers()
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .Count()
        .SetMaxTop(100)
        .AddRouteComponents("odata", SchoolODataModel.Create()));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (args.Contains("--validate-di", StringComparer.OrdinalIgnoreCase))
{
    ValidateServerComposition(app.Services);
    ValidateClientComposition();
    Console.WriteLine("Dependency injection validation succeeded.");
    return;
}

await SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CompileTimeFirst.Sample.Web.Client._Imports).Assembly);

app.Run();

static void RemoveDirectContextRegistration<TContext>(IServiceCollection services)
    where TContext : DbContext
{
    var descriptor = services.FirstOrDefault(item => item.ServiceType == typeof(TContext));
    if (descriptor is not null)
    {
        services.Remove(descriptor);
    }
}

static void ValidateServerComposition(IServiceProvider provider)
{
    DependencyInjectionGraphValidator.Validate(
        provider,
        new DependencyInjectionValidationOptions(
            Assemblies:
            [
                typeof(IUseCase).Assembly,
                typeof(App).Assembly,
                typeof(ClientServices).Assembly
            ],
            MarkerInterfaces: [typeof(IUseCase)]));
}

static void ValidateClientComposition()
{
    using var provider = ClientServices.BuildValidatedProvider(new Uri("https://localhost/"));

    DependencyInjectionGraphValidator.Validate(
        provider,
        new DependencyInjectionValidationOptions(
            Assemblies: [typeof(ClientServices).Assembly],
            MarkerInterfaces: []));
}

static async Task SeedAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchoolDbContext>>();
    await using var db = await factory.CreateDbContextAsync();

    await db.Database.EnsureCreatedAsync();

    // Seeding runs outside any user context, so no tenant is resolved and the filter matches
    // nothing. Lifting one named filter is the authorized escape hatch; the tenant of every row is
    // then stated explicitly rather than inherited from ambient state.
    if (await db.Tenants.IgnoreQueryFilters([DomainModelConfiguration.TenantFilter]).AnyAsync())
    {
        return;
    }

    var northTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var southTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    db.Tenants.AddRange(
        new Tenant(northTenantId, "North School"),
        new Tenant(southTenantId, "South School"));

    db.Subjects.AddRange(
        new Subject(Guid.NewGuid(), northTenantId, "Computing"),
        new Subject(Guid.NewGuid(), southTenantId, "Geography"));
    db.Grades.AddRange(
        new Grade(Guid.NewGuid(), northTenantId, "Grade 5", 5),
        new Grade(Guid.NewGuid(), southTenantId, "Grade 9", 9));

    await db.SaveChangesAsync();
}

public partial class Program;
