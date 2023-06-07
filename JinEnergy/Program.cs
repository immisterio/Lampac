using JinEnergy;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Logging.SetMinimumLevel(LogLevel.None);
var webAssemblyHost = builder.Build();

AppInit.JSRuntime = webAssemblyHost.Services.GetRequiredService<IJSRuntime>();

//builder.RootComponents.Add<App>("#app");
//builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

//await builder.Build().RunAsync();
