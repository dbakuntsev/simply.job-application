namespace Simply.JobApplication.Tests.M0.Helpers;

public class DataSyncFake : IDataSyncService
{
    public event Action<string?, string>? OnOrganizationChanged;
    public event Action<string?, string>? OnContactChanged;
    public event Action<string?, string>? OnContactOpportunityRoleChanged;
    public event Action<string?, string>? OnOpportunityChanged;
    public event Action<string?, string>? OnOpportunityFieldHistoryChanged;
    public event Action<string?, string>? OnCorrespondenceChanged;
    public event Action<string?, string>? OnCorrespondenceFileChanged;
    public event Action<string?, string>? OnBaseResumeChanged;
    public event Action<string?, string>? OnBaseResumeVersionChanged;
    public event Action<string?, string>? OnSessionChanged;
    public event Action? OnSessionsCleared;

    public List<(string entity, string? id, string @event)> Broadcasts { get; } = new();

    public Task BroadcastAsync(string entity, string? id, string @event)
    {
        Broadcasts.Add((entity, id, @event));
        return Task.CompletedTask;
    }

    public void Raise(string entity, string? id, string @event)
    {
        if (entity == "session" && @event == "cleared") { OnSessionsCleared?.Invoke(); return; }

        Action<string?, string>? handler = entity switch
        {
            "organization"            => OnOrganizationChanged,
            "contact"                 => OnContactChanged,
            "contactOpportunityRole"  => OnContactOpportunityRoleChanged,
            "opportunity"             => OnOpportunityChanged,
            "opportunityFieldHistory" => OnOpportunityFieldHistoryChanged,
            "correspondence"          => OnCorrespondenceChanged,
            "correspondenceFile"      => OnCorrespondenceFileChanged,
            "baseResume"              => OnBaseResumeChanged,
            "baseResumeVersion"       => OnBaseResumeVersionChanged,
            "session"                 => OnSessionChanged,
            _                         => null,
        };
        handler?.Invoke(id, @event);
    }
}
