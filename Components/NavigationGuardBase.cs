using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Simply.JobApplication.Components;

// Base class for form components that need navigation guards.
// Override IsDirty, StayLabel, and LeaveLabel. In markup add:
//   <NavigationLock OnBeforeInternalNavigation="HandleLocationChanging"
//                   ConfirmExternalNavigations="IsDirty" />
//   <NavigationGuardModal Show="_showGuardModal" StayLabel="StayLabel"
//                         LeaveLabel="LeaveLabel"
//                         OnStay="OnGuardStay" OnLeave="OnGuardLeave" />
public abstract class NavigationGuardBase : ComponentBase
{
    protected bool _showGuardModal;
    private TaskCompletionSource<bool>? _guardTcs;

    protected virtual bool IsDirty => false;
    protected virtual string StayLabel => "Stay and continue editing";
    protected virtual string LeaveLabel => "Discard changes and leave";

    // Wire to NavigationLock.OnBeforeInternalNavigation.
    // Awaiting holds the navigation until the user decides.
    protected async ValueTask HandleLocationChanging(LocationChangingContext context)
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
    protected void OnGuardStay() => _guardTcs?.TrySetResult(false);

    // Wire to NavigationGuardModal.OnLeave.
    protected void OnGuardLeave() => _guardTcs?.TrySetResult(true);
}
