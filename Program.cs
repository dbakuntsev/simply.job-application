using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Simply.JobApplication;
using Simply.JobApplication.Services;
using Simply.JobApplication.Services.AI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<IndexedDbService>();
builder.Services.AddScoped<DocxService>();
builder.Services.AddScoped<AppStateService>();
builder.Services.AddScoped<AiProviderFactory>();
builder.Services.AddScoped(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });

await builder.Build().RunAsync();
