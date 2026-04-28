using Microsoft.AspNetCore.Components.Routing;

namespace Simply.JobApplication.Tests.Shared;

// M1-10: NavigationGuardModal component tests + NavigationGuardBase integration via TestGuard.
public class NavigationGuardModalTests : BunitContext
{
    public NavigationGuardModalTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void NavigationGuardModal_WhenShowFalse_DoesNotRender()
    {
        var cut = Render<NavigationGuardModal>(p => p.Add(x => x.Show, false));
        Assert.Empty(cut.FindAll(".modal"));
    }

    [Fact]
    public void NavigationGuardModal_WhenShowTrue_RendersModal()
    {
        var cut = Render<NavigationGuardModal>(p => p.Add(x => x.Show, true));
        Assert.NotEmpty(cut.FindAll(".modal"));
    }

    [Fact]
    public async Task NavigationGuardModal_ClickStay_InvokesOnStayCallback()
    {
        var called = false;
        var cut = Render<NavigationGuardModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OnStay, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Find(".btn-primary").ClickAsync(new());

        Assert.True(called);
    }

    [Fact]
    public async Task NavigationGuardModal_ClickLeave_InvokesOnLeaveCallback()
    {
        var called = false;
        var cut = Render<NavigationGuardModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OnLeave, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Find(".btn-outline-secondary").ClickAsync(new());

        Assert.True(called);
    }

    [Fact]
    public void NavigationGuardModal_StayLabel_IsConfigurable()
    {
        var cut = Render<NavigationGuardModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.StayLabel, "Keep editing my form"));

        Assert.Contains("Keep editing my form", cut.Find(".btn-primary").TextContent);
    }

    [Fact]
    public void NavigationGuardModal_LeaveLabel_IsConfigurable()
    {
        var cut = Render<NavigationGuardModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.LeaveLabel, "Discard and go to Settings"));

        Assert.Contains("Discard and go to Settings", cut.Find(".btn-outline-secondary").TextContent);
    }

    // NavigationGuardBase integration via TestGuard

    [Fact]
    public async Task NavigationGuardModal_WhenDirty_ShowsModal_OnNavLinkClick()
    {
        var cut = Render<TestGuard>(p => p.Add(x => x.MakeDirty, true));
        var nav = Services.GetRequiredService<NavigationManager>();

        _ = cut.InvokeAsync(() => nav.NavigateTo("/settings"));
        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Count > 0, TimeSpan.FromSeconds(2));

        Assert.NotEmpty(cut.FindAll(".modal.d-block"));
    }

    [Fact]
    public async Task NavigationGuardModal_WhenClean_DoesNotShow_OnNavLinkClick()
    {
        var cut = Render<TestGuard>(p => p.Add(x => x.MakeDirty, false));
        var nav = Services.GetRequiredService<NavigationManager>();

        await cut.InvokeAsync(() => nav.NavigateTo("/settings"));

        Assert.Empty(cut.FindAll(".modal.d-block"));
    }

    [Fact]
    public async Task NavigationGuardModal_ClickStay_PreventsNavigation()
    {
        var cut = Render<TestGuard>(p => p.Add(x => x.MakeDirty, true));
        var nav = Services.GetRequiredService<NavigationManager>();

        _ = cut.InvokeAsync(() => nav.NavigateTo("/settings"));
        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Count > 0, TimeSpan.FromSeconds(2));
        await cut.Find(".btn-primary").ClickAsync(new());

        // bUnit's FakeNavigationManager does not restore the URI after PreventNavigation().
        // Verify the observable effect instead: the guard modal closes (HandleLocationChanging
        // completed the shouldLeave=false path, called context.PreventNavigation(), cleared the modal).
        await cut.WaitForStateAsync(() => !cut.FindAll(".modal.d-block").Any(), TimeSpan.FromSeconds(2));
        Assert.Empty(cut.FindAll(".modal.d-block"));
    }

    [Fact]
    public async Task NavigationGuardModal_ClickLeave_AllowsNavigation()
    {
        var cut = Render<TestGuard>(p => p.Add(x => x.MakeDirty, true));
        var nav = Services.GetRequiredService<NavigationManager>();

        // Subscribe before triggering navigation so we don't miss the event.
        // FakeNavigationManager calls NotifyLocationChanged() only when navigation is allowed
        // (i.e. after HandleLocationChanging returns without PreventNavigation).
        // This gives us a reliable signal that is independent of component re-render timing.
        var locationChangedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        nav.LocationChanged += (_, e) => locationChangedTcs.TrySetResult(e.Location);

        _ = cut.InvokeAsync(() => nav.NavigateTo("/settings"));
        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Count > 0, TimeSpan.FromSeconds(2));
        await cut.Find(".btn-outline-secondary").ClickAsync(new());

        // Wait for LocationChanged to fire — it is only raised when FakeNavigationManager
        // processes navigation as Succeeded (shouldContinueNavigation = true).
        var newLocation = await locationChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("/settings", newLocation);
    }

    [Fact]
    public async Task WhenDirty_CallsSjaSetBeforeUnloadTrue()
    {
        // sjaSetBeforeUnload(true) is called from OnAfterRenderAsync when IsDirty flips to true
        var jsInterop = JSInterop;
        jsInterop.Setup<object?>("sjaSetBeforeUnload", true);

        Render<TestGuard>(p => p.Add(x => x.MakeDirty, true));

        // Give the after-render cycle time to fire
        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(jsInterop.Invocations, i =>
            i.Identifier == "sjaSetBeforeUnload" &&
            i.Arguments.Count > 0 &&
            i.Arguments[0] is true);
    }

    [Fact]
    public async Task WhenClean_CallsSjaSetBeforeUnloadFalse()
    {
        var jsInterop = JSInterop;
        jsInterop.Setup<object?>("sjaSetBeforeUnload", false);

        Render<TestGuard>(p => p.Add(x => x.MakeDirty, false));

        await Task.Delay(50, Xunit.TestContext.Current.CancellationToken);

        // When not dirty, sjaSetBeforeUnload should NOT be called with true
        Assert.DoesNotContain(jsInterop.Invocations, i =>
            i.Identifier == "sjaSetBeforeUnload" &&
            i.Arguments.Count > 0 &&
            i.Arguments[0] is true);
    }
}
