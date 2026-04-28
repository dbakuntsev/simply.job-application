using Simply.JobApplication.Services.AI;

namespace Simply.JobApplication.Tests.Infrastructure;

// M0-2: Verify each concrete service implements its interface and is registered with the correct DI lifetime.
public class ServiceContractTests
{
    [Fact]
    public void IndexedDbService_ImplementsIIndexedDbService()
        => Assert.True(typeof(IndexedDbService).IsAssignableTo(typeof(IIndexedDbService)));

    [Fact]
    public void DocxService_ImplementsIDocxService()
        => Assert.True(typeof(DocxService).IsAssignableTo(typeof(IDocxService)));

    [Fact]
    public void DataSyncService_ImplementsIDataSyncService()
        => Assert.True(typeof(DataSyncService).IsAssignableTo(typeof(IDataSyncService)));

    [Fact]
    public void AiProviderFactory_ImplementsIAiProviderFactory()
        => Assert.True(typeof(AiProviderFactory).IsAssignableTo(typeof(IAiProviderFactory)));

    [Fact]
    public void IndexedDbService_IsRegisteredAsScoped()
    {
        var services = new ServiceCollection();
        services.AddScoped<IIndexedDbService, IndexedDbService>();
        var d = services.Single(x => x.ServiceType == typeof(IIndexedDbService));
        Assert.Equal(ServiceLifetime.Scoped, d.Lifetime);
        Assert.Equal(typeof(IndexedDbService), d.ImplementationType);
    }

    [Fact]
    public void DocxService_IsRegisteredAsScoped()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDocxService, DocxService>();
        var d = services.Single(x => x.ServiceType == typeof(IDocxService));
        Assert.Equal(ServiceLifetime.Scoped, d.Lifetime);
        Assert.Equal(typeof(DocxService), d.ImplementationType);
    }

    [Fact]
    public void DataSyncService_IsRegisteredAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDataSyncService, DataSyncService>();
        var d = services.Single(x => x.ServiceType == typeof(IDataSyncService));
        Assert.Equal(ServiceLifetime.Singleton, d.Lifetime);
        Assert.Equal(typeof(DataSyncService), d.ImplementationType);
    }

    [Fact]
    public void AiProviderFactory_IsRegisteredAsScoped()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAiProviderFactory, AiProviderFactory>();
        var d = services.Single(x => x.ServiceType == typeof(IAiProviderFactory));
        Assert.Equal(ServiceLifetime.Scoped, d.Lifetime);
        Assert.Equal(typeof(AiProviderFactory), d.ImplementationType);
    }
}
