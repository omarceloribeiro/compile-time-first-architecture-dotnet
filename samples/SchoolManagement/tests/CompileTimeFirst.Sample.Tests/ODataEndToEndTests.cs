using CompileTimeFirst.Sample.Web.Client.OData;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CompileTimeFirst.Sample.Tests;

public sealed class ODataEndToEndTests
{
    [Fact]
    public async Task Questions_page_renders_seeded_editor()
    {
        using var factory = CreateFactory();
        using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

        var html = await httpClient.GetStringAsync("questions");

        Assert.Contains("Create Question", html, StringComparison.Ordinal);
        Assert.Contains("Computing", html, StringComparison.Ordinal);
        Assert.Contains("Grade 5", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Microsoft_odata_client_executes_portable_query_against_web_endpoint()
    {
        using var factory = CreateFactory();
        using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });
        var readFactory = new ODataReadSchoolDbFactory(
            new Uri(httpClient.BaseAddress!, "odata/"));
        var executor = new ODataReadQueryExecutor(httpClient);

        await using var db = await readFactory.CreateAsync();
        var subjects = await executor.ToListAsync(
            db.Subjects.Where(x => x.IsActive).OrderBy(x => x.Name));
        var page = await executor.ToPageAsync(
            db.Subjects.Where(x => x.IsActive).OrderBy(x => x.Name),
            skip: 0,
            take: 10);
        var count = await executor.CountAsync(db.Subjects.Where(x => x.IsActive));
        var any = await executor.AnyAsync(db.Subjects.Where(x => x.Name == "Computing"));

        var subject = Assert.Single(subjects);
        Assert.Equal("Computing", subject.Name);
        Assert.Equal(["Computing"], page.Items.Select(x => x.Name));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, count);
        Assert.True(any);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                    services.AddDataProtection().UseEphemeralDataProtectionProvider());
            });
    }
}
