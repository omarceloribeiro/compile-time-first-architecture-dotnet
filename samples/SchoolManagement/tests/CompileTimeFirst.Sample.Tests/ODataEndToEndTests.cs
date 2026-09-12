using System.Net;
using System.Text.RegularExpressions;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.Web.Client.OData;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompileTimeFirst.Sample.Tests;

public sealed partial class ODataEndToEndTests
{
    [Fact]
    public async Task Anonymous_requests_are_denied()
    {
        using var factory = CreateFactory();
        using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost/")
        });

        var pageResponse = await httpClient.GetAsync("questions");
        var odataResponse = await httpClient.GetAsync("odata/Subjects");

        Assert.Equal(HttpStatusCode.Redirect, pageResponse.StatusCode);
        Assert.Equal("/login", pageResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, odataResponse.StatusCode);
    }

    [Fact]
    public async Task Account_one_renders_only_north_school_editor_data()
    {
        using var factory = CreateFactory();
        using var httpClient = CreateClient(factory);
        await SignInAsync(httpClient, "account1");

        var html = await httpClient.GetStringAsync("questions");

        Assert.Contains("Create Question", html, StringComparison.Ordinal);
        Assert.Contains("Computing", html, StringComparison.Ordinal);
        Assert.Contains("Grade 5", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Geography", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Grade 9", html, StringComparison.Ordinal);
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
        WebApplicationFactory<Program> factory,
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

    private static async Task AddSubjectsAsync(WebApplicationFactory<Program> factory)
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

    private static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? configure = null)
    {
        return new WebApplicationFactory<Program>()
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
