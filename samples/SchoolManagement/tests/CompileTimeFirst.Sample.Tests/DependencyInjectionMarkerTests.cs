extern alias ServerHost;

using Microsoft.Extensions.DependencyInjection;
using ServerHost::CompileTimeFirst.Validation;

namespace CompileTimeFirst.Sample.Tests;

public sealed class DependencyInjectionMarkerTests
{
    [Fact]
    public void Classification_markers_do_not_need_service_registrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Dependency>();
        services.AddSingleton<IRunProbe, Probe>();
        using var provider = services.BuildServiceProvider();
        Validate(provider);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_specific_contract_or_dependency_still_fails(bool registerContract)
    {
        var services = new ServiceCollection();
        if (registerContract)
        {
            services.AddSingleton<IRunProbe, Probe>();
        }

        using var provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => Validate(provider));
    }

    private static void Validate(IServiceProvider provider) =>
        DependencyInjectionGraphValidator.Validate(provider,
            new DependencyInjectionValidationOptions(
                [typeof(DependencyInjectionMarkerTests).Assembly],
                [typeof(IProbe), typeof(IReadProbe)],
                ValidateBlazorComponents: false));

    private interface IProbe;
    private interface IReadProbe : IProbe;
    private interface IRunProbe : IReadProbe
    {
        string Run();
    }

    private sealed class Dependency;
    private sealed class Probe(Dependency dependency) : IRunProbe
    {
        public string Run() => dependency.ToString()!;
    }
}
