using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Simply.JobApplication.Components;

// Base class for form components that need navigation guards.
// Override IsDirty, StayLabel, and LeaveLabel. In markup add:
//   <NavigationLock OnBeforeInternalNavigation="HandleLocationChanging" />
//   <NavigationGuardModal Show="_showGuardModal" StayLabel="StayLabel"
//                         LeaveLabel="LeaveLabel"
//                         OnStay="() => OnGuardStay()" OnLeave="() => OnGuardLeave()" />
//
// ConfirmExternalNavigations is not available in Blazor WASM 8.x; this base
// class manages the window.beforeunload handler via sjaSetBeforeUnload (app.js).
public abstract class NavigationGuardBase : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    protected bool _showGuardModal;
    private TaskCompletionSource<bool>? _guardTcs;
    private bool _lastDirty;

    protected virtual bool IsDirty => false;
    protected virtual string StayLabel => "Stay and continue editing";
    protected virtual string LeaveLabel => "Discard changes and leave";

    // Called after every render; keeps the beforeunload handler in sync with IsDirty.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsDirty != _lastDirty)
        {
            _lastDirty = IsDirty;
            try { await JS.InvokeVoidAsync("sjaSetBeforeUnload", IsDirty); } catch { }
        }
    }

    // Wire to NavigationLock.OnBeforeInternalNavigation.
    // Awaiting holds the navigation until the user decides.
    protected async Task HandleLocationChanging(LocationChangingContext context)
    {
        if (!IsDirty) return;

        _showGuardModal = true;
        _guardTcs = new TaskCompletionSource<bool>();
        StateHasChanged();

        var shouldLeave = await _guardTcs.Task;
        _showGuardModal = false;

        if (!shouldLeave)
            context.PreventNavigation();

        StateHasChanged();
    }

    // Wire to NavigationGuardModal.OnStay.
    protected virtual void OnGuardStay() => _guardTcs?.TrySetResult(false);

    // Wire to NavigationGuardModal.OnLeave.
    protected virtual void OnGuardLeave() => _guardTcs?.TrySetResult(true);

    public virtual async ValueTask DisposeAsync()
    {
        // Disarm beforeunload when the component is torn down (e.g. navigation).
        if (_lastDirty)
        {
            try { await JS.InvokeVoidAsync("sjaSetBeforeUnload", false); } catch { }
        }
    }
}
