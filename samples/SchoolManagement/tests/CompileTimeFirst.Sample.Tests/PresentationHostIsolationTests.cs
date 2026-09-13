using CompileTimeFirst.Sample.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompileTimeFirst.Sample.Tests;

public sealed class PresentationHostIsolationTests
{
    [Fact]
    public void Server_and_auto_use_distinct_identity_cookie_names()
    {
        using var serverFactory = new WebApplicationFactory<BlazorServerEntryPoint>();
        using var autoFactory = new WebApplicationFactory<BlazorAutoEntryPoint>();

        var serverCookieName = ReadIdentityCookieName(serverFactory.Services);
        var autoCookieName = ReadIdentityCookieName(autoFactory.Services);

        Assert.Equal(".CompileTimeFirst.BlazorServer.Identity", serverCookieName);
        Assert.Equal(".CompileTimeFirst.BlazorAuto.Identity", autoCookieName);
        Assert.NotEqual(serverCookieName, autoCookieName);
    }

    [Fact]
    public async Task Server_contexts_own_independent_connections_to_one_ephemeral_database()
    {
        using var factory = new WebApplicationFactory<BlazorServerEntryPoint>();

        await AssertIndependentContextConnectionsAsync(factory.Services);
    }

    [Fact]
    public async Task Auto_contexts_own_independent_connections_to_one_ephemeral_database()
    {
        using var factory = new WebApplicationFactory<BlazorAutoEntryPoint>();

        await AssertIndependentContextConnectionsAsync(factory.Services);
    }

    private static string? ReadIdentityCookieName(IServiceProvider services) =>
        services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme)
            .Cookie.Name;

    private static async Task AssertIndependentContextConnectionsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchoolDbContext>>();
        await using var first = await factory.CreateDbContextAsync();
        await using var second = await factory.CreateDbContextAsync();

        Assert.NotSame(first.Database.GetDbConnection(), second.Database.GetDbConnection());

        var counts = await Task.WhenAll(first.Users.CountAsync(), second.Users.CountAsync());
        Assert.All(counts, count => Assert.Equal(2, count));
    }
}
