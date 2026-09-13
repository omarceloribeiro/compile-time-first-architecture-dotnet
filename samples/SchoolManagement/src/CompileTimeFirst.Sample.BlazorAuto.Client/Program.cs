using CompileTimeFirst.Sample.ReadModel;
using CompileTimeFirst.Sample.BlazorAuto.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddODataReadClient(new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

await builder.Build().RunAsync();
