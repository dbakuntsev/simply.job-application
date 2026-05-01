using Microsoft.JSInterop;

namespace Simply.JobApplication.Services;

public class PwaService : IPwaService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<PwaService>? _ref;
    private IJSObjectReference? _module;
    private bool _initialized;

    public bool IsInstallable { get; private set; }
    public bool IsStandalone { get; private set; }
    public bool UpdateAvailable { get; private set; }

    public event Action? StateChanged;

    public PwaService(IJSRuntime js) => _js = js;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true; // Set before await — single-threaded WASM, no re-entry risk.
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/pwa.js");
        IsStandalone = await _module.InvokeAsync<bool>("isStandalone");
        IsInstallable = await _module.InvokeAsync<bool>("isInstallable");
        _ref = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("init", _ref);
    }

    public async Task PromptInstallAsync()
    {
        if (_module is null) return;
        await _module.InvokeAsync<bool>("promptInstall");
    }

    public async Task ApplyUpdateAsync()
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("applyUpdate");
    }

    [JSInvokable]
    public void OnInstallPromptAvailable()
    {
        IsInstallable = true;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnAppInstalled()
    {
        IsInstallable = false;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnUpdateWaiting()
    {
        UpdateAvailable = true;
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        if (_module is not null) await _module.DisposeAsync();
    }
}
