using CompileTimeFirst.Sample.Application;
using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Application.Grades;
using CompileTimeFirst.Sample.Application.QuestionOptions;
using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Application.Subjects;
using CompileTimeFirst.Sample.BlazorServer.Components;
using CompileTimeFirst.Sample.BlazorServer.Services;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Validation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddDbContextFactory<SchoolDbContext, TenantSchoolDbContextFactory>(
    options => options.UseSqlite(connection), ServiceLifetime.Scoped);
builder.Services.AddDbContextFactory<ReadOnlySchoolDbContext, TenantReadOnlySchoolDbContextFactory>(
    options => options.UseSqlite(connection), ServiceLifetime.Scoped);
RemoveDirectContextRegistration<ReadOnlySchoolDbContext>(builder.Services);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ClaimsCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<ClaimsCurrentUser>());
builder.Services.AddScoped<CircuitHandler, CurrentUserCircuitHandler>();
builder.Services.AddScoped<IReadSchoolDbFactory, ReadSchoolDbFactory>();
builder.Services.AddScoped<IReadProviderInfo>(provider =>
    (IReadProviderInfo)provider.GetRequiredService<IReadSchoolDbFactory>());
builder.Services.AddScoped<IReadQueryExecutor, EfReadQueryExecutor>();

builder.Services.AddScoped<ICreateSubjectUseCase, CreateSubjectUseCase>();
builder.Services.AddScoped<ICreateGradeUseCase, CreateGradeUseCase>();
builder.Services.AddScoped<ICreateQuestionUseCase, CreateQuestionUseCase>();
builder.Services.AddScoped<ICreateQuestionOptionUseCase, CreateQuestionOptionUseCase>();
builder.Services.AddScoped<IGetSchoolDashboardUseCase, GetSchoolDashboardUseCase>();
builder.Services.AddScoped<IExportQuestionsUseCase, ExportQuestionsUseCase>();

builder.Services
    .AddIdentity<SchoolUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<SchoolDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<SchoolUser>, TenantUserClaimsPrincipalFactory>();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (args.Contains("--validate-di", StringComparer.OrdinalIgnoreCase))
{
    ValidateComposition(app.Services);
    Console.WriteLine("Dependency injection validation succeeded.");
    return;
}

await SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseWhen(
    context => HttpMethods.IsGet(context.Request.Method),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    context.RequestServices.GetRequiredService<ClaimsCurrentUser>().SetPrincipal(context.User);
    await next(context);
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
MapAuthenticationEndpoints(app);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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

static void ValidateComposition(IServiceProvider provider)
{
    DependencyInjectionGraphValidator.Validate(
        provider,
        new DependencyInjectionValidationOptions(
            Assemblies:
            [
                typeof(IUseCase).Assembly,
                typeof(App).Assembly
            ],
            MarkerInterfaces: [typeof(IUseCase)]));
}

static void MapAuthenticationEndpoints(WebApplication app)
{
    app.MapPost(
            "/account/login",
            async (HttpContext context, SignInManager<SchoolUser> signInManager, IAntiforgery antiforgery) =>
            {
                if (!await HasValidAntiforgeryTokenAsync(context, antiforgery))
                {
                    return Results.BadRequest();
                }

                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var result = await signInManager.PasswordSignInAsync(
                    form["username"].ToString(),
                    form["password"].ToString(),
                    isPersistent: false,
                    lockoutOnFailure: false);

                return Results.LocalRedirect(result.Succeeded ? "/" : "/login?error=invalid");
            })
        .AllowAnonymous();

    app.MapPost(
            "/account/logout",
            async (HttpContext context, SignInManager<SchoolUser> signInManager, IAntiforgery antiforgery) =>
            {
                if (!await HasValidAntiforgeryTokenAsync(context, antiforgery))
                {
                    return Results.BadRequest();
                }

                await signInManager.SignOutAsync();
                return Results.LocalRedirect("/login");
            })
        .RequireAuthorization();
}

static async Task<bool> HasValidAntiforgeryTokenAsync(HttpContext context, IAntiforgery antiforgery)
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        return false;
    }
}

static async Task SeedAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchoolDbContext>>();
    await using var db = await factory.CreateDbContextAsync();

    await db.Database.EnsureCreatedAsync();

    // Seeding runs outside any user context, so no tenant is resolved and the filter matches
    // nothing. Lifting one named filter is the authorized escape hatch; every seeded tenant is
    // stated explicitly rather than inherited from ambient state.
    var northTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var southTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    if (!await db.Tenants.IgnoreQueryFilters([DomainModelConfiguration.TenantFilter]).AnyAsync())
    {
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

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SchoolUser>>();
    await EnsureUserAsync(
        userManager,
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
        "account1",
        northTenantId);
    await EnsureUserAsync(
        userManager,
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
        "account2",
        southTenantId);
}

static async Task EnsureUserAsync(
    UserManager<SchoolUser> userManager,
    Guid userId,
    string userName,
    Guid tenantId)
{
    var existing = await userManager.FindByNameAsync(userName);
    if (existing is not null)
    {
        if (existing.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Seeded account '{userName}' belongs to an unexpected tenant.");
        }

        return;
    }

    var result = await userManager.CreateAsync(
        new SchoolUser(userId, userName, tenantId),
        "Sample123!");

    if (!result.Succeeded)
    {
        throw new InvalidOperationException(
            $"Could not seed account '{userName}': {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}

public sealed class BlazorServerEntryPoint;
