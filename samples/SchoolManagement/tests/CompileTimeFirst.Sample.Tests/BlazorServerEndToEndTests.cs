using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CompileTimeFirst.Sample.Tests;

public sealed partial class BlazorServerEndToEndTests
{
    [Fact]
    public async Task Anonymous_functional_page_redirects_to_login()
    {
        using var factory = CreateFactory();
        using var httpClient = CreateClient(factory, allowAutoRedirect: false);

        var response = await httpClient.GetAsync("questions");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.AbsolutePath);
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
    public async Task Parallel_logins_complete_without_sqlite_connection_failures()
    {
        using var factory = CreateFactory();

        var loginTasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var httpClient = CreateClient(factory, allowAutoRedirect: false);
            await SignInAsync(httpClient, "account1");
        });

        await Task.WhenAll(loginTasks);
    }

    [Fact]
    public async Task Missing_antiforgery_is_a_bad_request_for_server_login_and_logout()
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
    public async Task Logout_removes_access_to_server_pages()
    {
        using var factory = CreateFactory();
        using var httpClient = CreateClient(factory, allowAutoRedirect: false);
        await SignInAsync(httpClient, "account1");

        var home = await httpClient.GetStringAsync("");
        var response = await httpClient.PostAsync(
            "account/logout",
            Form(("__RequestVerificationToken", ReadAntiforgeryToken(home))));
        var pageResponse = await httpClient.GetAsync("questions");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, pageResponse.StatusCode);
        Assert.Equal("/login", pageResponse.Headers.Location?.AbsolutePath);
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<BlazorServerEntryPoint> factory,
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

    private static WebApplicationFactory<BlazorServerEntryPoint> CreateFactory() =>
        new WebApplicationFactory<BlazorServerEntryPoint>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                    services.AddDataProtection().UseEphemeralDataProtectionProvider());
            });
}
