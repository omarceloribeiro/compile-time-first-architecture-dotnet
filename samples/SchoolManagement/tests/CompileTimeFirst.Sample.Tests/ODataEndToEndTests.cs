using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.BlazorAuto.Client.OData;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompileTimeFirst.Sample.Tests;

public sealed partial class ODataEndToEndTests
{
    [Fact]
    public async Task Anonymous_odata_response_is_status_only()
    {
        using var factory = CreateFactory();
        using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost/")
        });

        var odataResponse = await httpClient.GetAsync("odata/Subjects");
        var body = await odataResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, odataResponse.StatusCode);
        Assert.Null(odataResponse.Headers.Location);
        Assert.Null(odataResponse.Content.Headers.ContentType);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public async Task Account_two_reads_only_south_school_through_odata()
    {
        using var factory = CreateFactory();
        using var httpClient = CreateClient(factory);
        await SignInAsync(httpClient, "account2");
        var readFactory = new ODataReadSchoolDbFactory(new Uri(httpClient.BaseAddress!, "odata/"));
        var executor = new ODataReadQueryExecutor(httpClient);

        await using var db = await readFactory.CreateAsync();
        var subjects = await executor.ToListAsync(
            db.Subjects.OrderBy(x => x.Name).ThenBy(x => x.Id));

        Assert.Equal(["Geography"], subjects.Select(x => x.Name));
    }

    [Fact]
    public async Task Parallel_authenticated_odata_reads_complete_without_sqlite_connection_failures()
    {
        using var factory = CreateFactory();

        var readTasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var httpClient = CreateClient(factory);
            await SignInAsync(httpClient, "account1");
            using var response = await httpClient.GetAsync(
                "odata/Subjects?$select=Id,Name&$orderby=Name,Id");
            return response.StatusCode;
        });

        var statusCodes = await Task.WhenAll(readTasks);

        Assert.All(statusCodes, statusCode => Assert.Equal(HttpStatusCode.OK, statusCode));
    }

    [Fact]
    public async Task Persisted_tenant_claim_cannot_override_the_account_tenant()
    {
        using var factory = CreateFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SchoolUser>>();
            var user = await userManager.FindByNameAsync("account1");
            Assert.NotNull(user);
            var result = await userManager.AddClaimAsync(
                user,
                new Claim(SchoolClaimTypes.TenantId, SouthTenantId.ToString("D")));
            Assert.True(result.Succeeded);
        }

        using var httpClient = CreateClient(factory);
        await SignInAsync(httpClient, "account1");
        var readFactory = new ODataReadSchoolDbFactory(new Uri(httpClient.BaseAddress!, "odata/"));
        var executor = new ODataReadQueryExecutor(httpClient);

        await using var db = await readFactory.CreateAsync();
        var subjects = await executor.ToListAsync(
            db.Subjects.OrderBy(x => x.Name).ThenBy(x => x.Id));

        Assert.Equal(["Computing"], subjects.Select(x => x.Name));
    }

    [Fact]
    public async Task Microsoft_odata_client_executes_paged_portable_query_with_identity_cookie()
    {
        using var factory = CreateFactory();
        using var httpClient = CreateClient(factory);
        await SignInAsync(httpClient, "account1");
        await AddSubjectsAsync(factory);
        var readFactory = new ODataReadSchoolDbFactory(new Uri(httpClient.BaseAddress!, "odata/"));
        var executor = new ODataReadQueryExecutor(httpClient);

        await using var db = await readFactory.CreateAsync();
        var subjects = await executor.ToListAsync(
            db.Subjects
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id));
        var page = await executor.ToPageAsync(
            db.Subjects
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id),
            skip: 1,
            take: 1);
        var count = await executor.CountAsync(db.Subjects.Where(x => x.IsActive));
        var any = await executor.AnyAsync(db.Subjects.Where(x => x.Name == "Computing"));

        Assert.Equal(["Computing", "Mathematics", "Science"], subjects.Select(x => x.Name));
        Assert.Single(page.Items);
        Assert.Equal(["Mathematics"], page.Items.Select(x => x.Name));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, count);
        Assert.True(any);
    }

    [Fact]
    public async Task Logout_removes_access_to_odata()
    {
        using var factory = CreateFactory();
        using var httpClient = CreateClient(factory, allowAutoRedirect: false);
        await SignInAsync(httpClient, "account1");

        var home = await httpClient.GetStringAsync("");
        var response = await httpClient.PostAsync(
            "account/logout",
            Form(("__RequestVerificationToken", ReadAntiforgeryToken(home))));
        var odataResponse = await httpClient.GetAsync("odata/Subjects");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, odataResponse.StatusCode);
    }

    [Fact]
    public async Task Missing_antiforgery_is_a_bad_request_for_auto_login_and_logout()
    {
        using var factory = CreateFactory();
        using var httpClient = CreateClient(factory, allowAutoRedirect: false);

        var loginResponse = await httpClient.PostAsync(
            "account/login",
            Form(("username", "account1"), ("password", "Sample123!")));
        var loginBody = await loginResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, loginResponse.StatusCode);
        Assert.DoesNotContain("AntiforgeryValidationException", loginBody, StringComparison.Ordinal);

        await SignInAsync(httpClient, "account1");
        var logoutResponse = await httpClient.PostAsync("account/logout", Form());
        var logoutBody = await logoutResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, logoutResponse.StatusCode);
        Assert.DoesNotContain("AntiforgeryValidationException", logoutBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Interactive_auto_boundary_does_not_render_infrastructure_exception_message()
    {
        const string sensitiveMessage = "database password was secret";
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IReadQueryExecutor>();
            services.AddScoped<IReadQueryExecutor>(_ => new ThrowingReadQueryExecutor(sensitiveMessage));
        });
        using var httpClient = CreateClient(factory);
        await SignInAsync(httpClient, "account1");

        var html = await httpClient.GetStringAsync("auto-subjects");

        Assert.Contains("The subjects could not be loaded. The error has been logged.", html, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, html, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<BlazorAutoEntryPoint> factory,
        bool allowAutoRedirect = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            BaseAddress = new Uri("https://localhost/")
        });

    private static async Task SignInAsync(HttpClient httpClient, string userName)
    {
        var loginPage = await httpClient.GetStringAsync("login");
        var response = await httpClient.PostAsync(
            "account/login",
            Form(
                ("username", userName),
                ("password", "Sample123!"),
                ("__RequestVerificationToken", ReadAntiforgeryToken(loginPage))));

        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Equal("/", response.Headers.Location?.OriginalString);
        }
        else
        {
            response.EnsureSuccessStatusCode();
        }
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] values) =>
        new(values.Select(value => new KeyValuePair<string, string>(value.Key, value.Value)));

    private static string ReadAntiforgeryToken(string html)
    {
        var match = AntiforgeryTokenRegex().Match(html);
        Assert.True(match.Success, "The rendered page did not contain an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    private static async Task AddSubjectsAsync(WebApplicationFactory<BlazorAutoEntryPoint> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchoolDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();

        db.Subjects.AddRange(
            new Subject(Guid.NewGuid(), NorthTenantId, "Mathematics"),
            new Subject(Guid.NewGuid(), NorthTenantId, "Science"));
        await db.SaveChangesAsync();
    }

    private static readonly Guid NorthTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SouthTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static WebApplicationFactory<BlazorAutoEntryPoint> CreateFactory(
        Action<IServiceCollection>? configure = null)
    {
        return new WebApplicationFactory<BlazorAutoEntryPoint>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                    configure?.Invoke(services);
                });
            });
    }

    private sealed class ThrowingReadQueryExecutor(string message) : IReadQueryExecutor
    {
        public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) =>
            Task.FromException<List<T>>(new InvalidOperationException(message));

        public Task<PageResult<T>> ToPageAsync<T>(
            IQueryable<T> query,
            int skip,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromException<PageResult<T>>(new InvalidOperationException(message));

        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) =>
            Task.FromException<T?>(new InvalidOperationException(message));

        public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) =>
            Task.FromException<T?>(new InvalidOperationException(message));

        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) =>
            Task.FromException<int>(new InvalidOperationException(message));

        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) =>
            Task.FromException<bool>(new InvalidOperationException(message));
    }
}
