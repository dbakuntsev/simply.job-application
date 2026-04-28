using Radzen;

namespace Simply.JobApplication.Tests.Helpers;

public record AppServiceMocks(
    IIndexedDbService Db,
    DataSyncFake DataSync,
    IDocxService Docx,
    IAiProviderFactory AiFactory);

public static class BunitContextExtensions
{
    // Registers all mocked application services into a bUnit BunitContext.
    // Sets JSInterop to Loose mode so unhandled JS calls (Monaco, popovers, eval) are silently ignored.
    // Returns mocks for direct configuration in tests.
    public static AppServiceMocks AddAppServices(this BunitContext ctx,
        IIndexedDbService? db = null)
    {
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var dbMock     = db ?? new TestIndexedDbBuilder().Build();
        var dataSync   = new DataSyncFake();
        var docxMock   = Substitute.For<IDocxService>();
        var aiFactory  = Substitute.For<IAiProviderFactory>();

        ctx.Services.AddSingleton(dbMock);
        ctx.Services.AddSingleton<IDataSyncService>(dataSync);
        ctx.Services.AddSingleton(docxMock);
        ctx.Services.AddSingleton(aiFactory);
        ctx.Services.AddSingleton<HttpClient>();
        ctx.Services.AddSingleton<AppStateService>();
        ctx.Services.AddRadzenComponents();

        return new AppServiceMocks(dbMock, dataSync, docxMock, aiFactory);
    }
}
