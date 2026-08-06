using CompileTimeFirst.Sample.ConsoleApp;
using Microsoft.Extensions.DependencyInjection;

using var serviceProvider = CompositionRoot.Build();
CompositionRoot.Validate(serviceProvider);

if (args.Contains("--validate-di", StringComparer.OrdinalIgnoreCase))
{
    System.Console.WriteLine("Dependency injection validation succeeded.");
    return;
}

using var scope = serviceProvider.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<SampleRunner>();
await runner.RunAsync();
