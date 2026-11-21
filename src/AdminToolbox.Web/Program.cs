// SPDX-License-Identifier: MIT
using System.Globalization;
using AdminToolbox.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddLocalization();
builder.Services.TryAddSingleton<LazyAssemblyLoader>();

var host = builder.Build();

// Language: stored choice, else browser language, else English (resolved in wwwroot/js/boot.js).
var js = host.Services.GetRequiredService<IJSRuntime>();
var culture = new CultureInfo(await js.InvokeAsync<string>("atb.lang"));
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
CultureInfo.CurrentCulture = culture;
CultureInfo.CurrentUICulture = culture;

await host.RunAsync();
