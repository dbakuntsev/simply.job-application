namespace Simply.JobApplication.Services.AI;

public interface IAiProviderFactory
{
    IReadOnlyList<IAiProvider> All { get; }
    IAiProvider Get(string providerId);
}
