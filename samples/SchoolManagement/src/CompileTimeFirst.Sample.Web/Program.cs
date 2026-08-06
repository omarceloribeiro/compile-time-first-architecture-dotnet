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
using CompileTimeFirst.Sample.Web.Components.Pages.Grades;
using CompileTimeFirst.Sample.Web.Components.Pages.QuestionOptions;
using CompileTimeFirst.Sample.Web.Components.Pages.Questions;
using CompileTimeFirst.Sample.Web.Components.Pages.Subjects;
using CompileTimeFirst.Sample.Web.OData;
using CompileTimeFirst.Validation;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((_, options) =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

var databaseRoot = new InMemoryDatabaseRoot();
const string databaseName = "compile-time-first-school-web";

builder.Services.AddDbContextFactory<SchoolDbContext>(options =>
    options.UseInMemoryDatabase(databaseName, databaseRoot));
RemoveDirectContextRegistration<SchoolDbContext>(builder.Services);

builder.Services.AddDbContextFactory<ReadOnlySchoolDbContext>(options =>
    options.UseInMemoryDatabase(databaseName, databaseRoot));
RemoveDirectContextRegistration<ReadOnlySchoolDbContext>(builder.Services);

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

builder.Services.AddScoped<SubjectsViewModel>();
builder.Services.AddScoped<GradesViewModel>();
builder.Services.AddScoped<QuestionsViewModel>();
builder.Services.AddScoped<QuestionOptionsViewModel>();
builder.Services.AddScoped<AutoSubjectsViewModel>();

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
                typeof(CompileTimeFirst.Sample.Web.Components.Pages.IViewModel).Assembly,
                typeof(ClientServices).Assembly
            ],
            MarkerInterfaces:
            [
                typeof(IUseCase),
                typeof(CompileTimeFirst.Sample.Web.Components.Pages.IViewModel),
                typeof(CompileTimeFirst.Sample.Web.Client.IViewModel)
            ]));
}

static void ValidateClientComposition()
{
    using var provider = ClientServices.BuildValidatedProvider(new Uri("https://localhost/"));

    DependencyInjectionGraphValidator.Validate(
        provider,
        new DependencyInjectionValidationOptions(
            Assemblies: [typeof(ClientServices).Assembly],
            MarkerInterfaces: [typeof(CompileTimeFirst.Sample.Web.Client.IViewModel)]));
}

static async Task SeedAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchoolDbContext>>();
    await using var db = await factory.CreateDbContextAsync();

    if (await db.Subjects.AnyAsync())
    {
        return;
    }

    db.Subjects.Add(new Subject
    {
        Id = Guid.NewGuid(),
        Name = "Computing"
    });
    db.Grades.Add(new Grade
    {
        Id = Guid.NewGuid(),
        Name = "Grade 5",
        Order = 5
    });

    await db.SaveChangesAsync();
}

public partial class Program;
