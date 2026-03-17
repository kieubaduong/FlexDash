using FlexDash.Client;
using FlexDash.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("https://localhost:7095/") });
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<SignalRService>();

await builder.Build().RunAsync();
