using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.Web.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddODataReadClient(new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
