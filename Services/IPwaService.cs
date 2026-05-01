namespace Simply.JobApplication.Services;

public interface IPwaService
{
    bool IsInstallable { get; }
    bool IsStandalone { get; }
    bool UpdateAvailable { get; }
    event Action? StateChanged;
    Task EnsureInitializedAsync();
    Task PromptInstallAsync();
    Task ApplyUpdateAsync();
}
