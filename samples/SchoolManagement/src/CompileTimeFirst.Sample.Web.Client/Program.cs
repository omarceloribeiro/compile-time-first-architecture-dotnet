using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.Web.Client;
using CompileTimeFirst.Sample.Web.Client.Pages.AutoSubjects;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddODataReadClient(new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddScoped<AutoSubjectsViewModel>();

await builder.Build().RunAsync();
