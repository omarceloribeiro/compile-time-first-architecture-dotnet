using CompileTimeFirst.Sample.BlazorAuto.Client;
using CompileTimeFirst.Sample.BlazorAuto.Components;
using CompileTimeFirst.Sample.BlazorAuto.OData;
using CompileTimeFirst.Sample.BlazorAuto.Services;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Validation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((_, options) =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// The keeper preserves the named in-memory database, but operation-scoped DbContexts never share
// its connection instance. Each context opens its own connection from this connection string.
var connectionString = CreateInMemoryConnectionString("CompileTimeFirst.BlazorAuto");
builder.Services.AddSingleton(_ =>
{
    var keeperConnection = new SqliteConnection(connectionString);
    keeperConnection.Open();
    return keeperConnection;
});

builder.Services.AddDbContextFactory<SchoolDbContext, TenantSchoolDbContextFactory>(
    options => options.UseSqlite(connectionString), ServiceLifetime.Scoped);
builder.Services.AddDbContextFactory<ReadOnlySchoolDbContext, TenantReadOnlySchoolDbContextFactory>(
    options => options.UseSqlite(connectionString), ServiceLifetime.Scoped);
RemoveDirectContextRegistration<ReadOnlySchoolDbContext>(builder.Services);

builder.Services.AddScoped<ClaimsCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<ClaimsCurrentUser>());
builder.Services.AddScoped<CircuitHandler, CurrentUserCircuitHandler>();
builder.Services.AddScoped<IReadSchoolDbFactory, ReadSchoolDbFactory>();
builder.Services.AddScoped<IReadProviderInfo>(provider =>
    (IReadProviderInfo)provider.GetRequiredService<IReadSchoolDbFactory>());
builder.Services.AddScoped<IReadQueryExecutor, EfReadQueryExecutor>();
builder.Services.AddScoped<SchoolODataReadScope>();

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
    options.Cookie.Name = ".CompileTimeFirst.BlazorAuto.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/odata"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        else
        {
            context.Response.Redirect("/login");
        }

        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/odata"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        }
        else
        {
            context.Response.Redirect("/login");
        }

        return Task.CompletedTask;
    };
});

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
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

var app = builder.Build();

if (args.Contains("--validate-di", StringComparer.OrdinalIgnoreCase))
{
    ValidateServerComposition(app.Services);
    ValidateClientComposition();
    Console.WriteLine("Dependency injection validation succeeded.");
    return;
}

_ = app.Services.GetRequiredService<SqliteConnection>();
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

app.UseWhen(
    context => HttpMethods.IsGet(context.Request.Method) &&
               !context.Request.Path.StartsWithSegments("/odata"),
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
app.MapControllers().RequireAuthorization();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CompileTimeFirst.Sample.BlazorAuto.Client._Imports).Assembly);

app.Run();

static string CreateInMemoryConnectionString(string hostName) =>
    new SqliteConnectionStringBuilder
    {
        DataSource = $"{hostName}.{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared
    }.ToString();

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
                typeof(App).Assembly,
                typeof(ClientServices).Assembly
            ],
            MarkerInterfaces: []));
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

public sealed class BlazorAutoEntryPoint;
