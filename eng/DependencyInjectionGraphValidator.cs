using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CompileTimeFirst.Validation;

public sealed record DependencyInjectionValidationOptions(
    IReadOnlyCollection<Assembly> Assemblies,
    IReadOnlyCollection<Type> MarkerInterfaces,
    bool ValidateBlazorComponents = true);

public static class DependencyInjectionGraphValidator
{
    private const string BlazorComponentInterface = "Microsoft.AspNetCore.Components.IComponent";
    private const string BlazorInjectAttribute = "Microsoft.AspNetCore.Components.InjectAttribute";

    public static void Validate(
        IServiceProvider rootProvider,
        DependencyInjectionValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(rootProvider);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Assemblies.Count == 0)
        {
            throw new ArgumentException("At least one assembly must be supplied.", nameof(options));
        }

        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var assemblies = options.Assemblies.Distinct().ToArray();

        foreach (var markerInterface in options.MarkerInterfaces.Distinct())
        {
            ValidateMarkerServices(provider, assemblies, markerInterface);
        }

        if (options.ValidateBlazorComponents)
        {
            ValidateBlazorInjections(provider, assemblies);
        }
    }

    private static void ValidateMarkerServices(
        IServiceProvider provider,
        IEnumerable<Assembly> assemblies,
        Type markerInterface)
    {
        if (!markerInterface.IsInterface)
        {
            throw new ArgumentException(
                $"Marker type {markerInterface.FullName} must be an interface.",
                nameof(markerInterface));
        }

        var implementationTypes = assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                markerInterface.IsAssignableFrom(type))
            .Distinct()
            .ToArray();

        foreach (var implementationType in implementationTypes)
        {
            var contracts = implementationType
                .GetInterfaces()
                .Where(contract =>
                    contract != markerInterface &&
                    markerInterface.IsAssignableFrom(contract))
                .Distinct()
                .ToArray();

            if (contracts.Length == 0)
            {
                _ = provider.GetRequiredService(implementationType);
                continue;
            }

            foreach (var contract in contracts)
            {
                var registrations = provider.GetServices(contract).ToArray();

                if (!registrations.Any(instance =>
                        instance is not null &&
                        implementationType.IsInstanceOfType(instance)))
                {
                    throw new InvalidOperationException(
                        $"{implementationType.FullName} implements {contract.FullName}, " +
                        "but that implementation is not registered in dependency injection.");
                }
            }
        }
    }

    private static void ValidateBlazorInjections(
        IServiceProvider provider,
        IEnumerable<Assembly> assemblies)
    {
        var componentTypes = assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.GetInterfaces().Any(@interface =>
                    @interface.FullName == BlazorComponentInterface))
            .Distinct()
            .ToArray();

        foreach (var componentType in componentTypes)
        {
            _ = ActivatorUtilities.CreateInstance(provider, componentType);

            foreach (var property in componentType.GetProperties(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                var injectAttribute = property
                    .GetCustomAttributes(inherit: true)
                    .FirstOrDefault(attribute =>
                        attribute.GetType().FullName == BlazorInjectAttribute);

                if (injectAttribute is null)
                {
                    continue;
                }

                var key = injectAttribute
                    .GetType()
                    .GetProperty("Key", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(injectAttribute);

                _ = key is null
                    ? provider.GetRequiredService(property.PropertyType)
                    : provider.GetRequiredKeyedService(property.PropertyType, key);
            }
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
