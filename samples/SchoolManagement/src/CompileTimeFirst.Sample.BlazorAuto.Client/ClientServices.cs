using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.BlazorAuto.Client.OData;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CompileTimeFirst.Sample.BlazorAuto.Client;

public static class ClientServices
{
    public static IServiceCollection AddODataReadClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        var serviceRoot = new Uri(baseAddress, "odata/");
        services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress });
        services.AddScoped<IReadSchoolDbFactory>(_ => new ODataReadSchoolDbFactory(serviceRoot));
        services.AddScoped<IReadProviderInfo>(provider =>
            (IReadProviderInfo)provider.GetRequiredService<IReadSchoolDbFactory>());
        services.AddScoped<IReadQueryExecutor, ODataReadQueryExecutor>();
        return services;
    }

    public static ServiceProvider BuildValidatedProvider(Uri baseAddress)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, ValidationAuthenticationStateProvider>();
        services.AddODataReadClient(baseAddress);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private sealed class ValidationAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly AuthenticationState Anonymous = new(new System.Security.Claims.ClaimsPrincipal());

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(Anonymous);
    }
}
