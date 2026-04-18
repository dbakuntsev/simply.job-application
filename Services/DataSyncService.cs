using Microsoft.JSInterop;

namespace Simply.JobApplication.Services;

public class DataSyncService : IDataSyncService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<DataSyncService>? _selfRef;
    private readonly string _tabId = Guid.NewGuid().ToString();

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

    public DataSyncService(IJSRuntime js) => _js = js;

    private async Task<IJSObjectReference> ModuleAsync()
    {
        if (_module is null)
        {
            _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/datasync.js");
            _selfRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("initialize", _tabId, _selfRef);
        }
        return _module;
    }

    public async Task BroadcastAsync(string entity, string? id, string @event)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("broadcast", entity, id, @event);
    }

    [JSInvokable]
    public void OnMessageReceived(string entity, string? id, string @event)
    {
        if (entity == "session" && @event == "cleared")
        {
            OnSessionsCleared?.Invoke();
            return;
        }

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

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("dispose"); } catch { }
            await _module.DisposeAsync();
        }
        _selfRef?.Dispose();
    }
}
