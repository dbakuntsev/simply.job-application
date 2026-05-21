using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Simply.JobApplication;
using Simply.JobApplication.Services;
using Simply.JobApplication.Services.AI;
using Simply.JobApplication.Services.QnA;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddRadzenComponents();
builder.Services.AddScoped<IIndexedDbService, IndexedDbService>();
builder.Services.AddSingleton<IDataSyncService, DataSyncService>();
builder.Services.AddScoped<IDocxService, DocxService>();
builder.Services.AddScoped<AppStateService>();
builder.Services.AddScoped<AppStartupService>();
builder.Services.AddScoped<AppToastService>();
builder.Services.AddScoped<IAiProviderFactory, AiProviderFactory>();
builder.Services.AddScoped<IQualityValidator, QualityValidator>();
builder.Services.AddScoped<Stage2RejectionSampler>();
builder.Services.AddScoped<Simply.JobApplication.Services.AI.OpenAi.OpenAiProvider>();
builder.Services.AddScoped<IDemoDataService, DemoDataService>();
builder.Services.AddScoped<IPwaService, PwaService>();
builder.Services.AddSingleton<ILoggerService, LoggerService>();
builder.Services.AddSingleton<IUsageRecorder>(NullUsageRecorder.Instance);
builder.Services.AddSingleton<IRateLimitGate>(NullRateLimitGate.Instance);
builder.Services.AddScoped(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });

await builder.Build().RunAsync();
