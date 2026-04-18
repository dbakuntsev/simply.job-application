namespace Simply.JobApplication.Services;

public interface IDataSyncService
{
    Task BroadcastAsync(string entity, string? id, string @event);

    event Action<string?, string> OnOrganizationChanged;
    event Action<string?, string> OnContactChanged;
    event Action<string?, string> OnContactOpportunityRoleChanged;
    event Action<string?, string> OnOpportunityChanged;
    event Action<string?, string> OnOpportunityFieldHistoryChanged;
    event Action<string?, string> OnCorrespondenceChanged;
    event Action<string?, string> OnCorrespondenceFileChanged;
    event Action<string?, string> OnBaseResumeChanged;
    event Action<string?, string> OnBaseResumeVersionChanged;
    event Action<string?, string> OnSessionChanged;
    event Action OnSessionsCleared;
}
