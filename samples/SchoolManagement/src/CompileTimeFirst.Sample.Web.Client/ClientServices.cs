using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.Web.Client.OData;
using Microsoft.Extensions.DependencyInjection;

namespace CompileTimeFirst.Sample.Web.Client;

public static class ClientServices
{
    public static IServiceCollection AddODataReadClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        var serviceRoot = new Uri(baseAddress, "odata/");
        services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress });
        services.AddScoped<IReadSchoolDbFactory>(provider =>
            new ODataReadSchoolDbFactory(serviceRoot, provider.GetRequiredService<HttpClient>()));
        services.AddScoped<IReadProviderInfo>(provider =>
            (IReadProviderInfo)provider.GetRequiredService<IReadSchoolDbFactory>());
        services.AddScoped<IReadQueryExecutor, ODataReadQueryExecutor>();
        return services;
    }

    public static ServiceProvider BuildValidatedProvider(Uri baseAddress)
    {
        var services = new ServiceCollection();
        services.AddODataReadClient(baseAddress);
        services.AddScoped<Pages.AutoSubjects.AutoSubjectsViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
