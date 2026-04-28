namespace Simply.JobApplication.Tests.M9;

// M9-1: ConflictAlertModal component tests.
// M9-2: DeletionAlertModal (RemoteDeletionAlertModal) component tests.
public class ModalComponentTests : BunitContext
{
    // ── M9-1: ConflictAlertModal ──────────────────────────────────────────────

    [Fact]
    public void ConflictAlertModal_VersionMismatch_ShowsCorrectMessage()
    {
        const string msg = "This record was changed in another tab. Your changes have not been saved.";
        var cut = Render<ConflictAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Message, msg));

        Assert.Contains("Your changes have not been saved", cut.Find(".modal-body").TextContent);
    }

    [Fact]
    public void ConflictAlertModal_BroadcastConflict_ShowsCorrectMessage()
    {
        const string msg = "This record was changed in another tab. Your unsaved changes may conflict.";
        var cut = Render<ConflictAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Message, msg));

        Assert.Contains("Your unsaved changes may conflict", cut.Find(".modal-body").TextContent);
    }

    [Fact]
    public async Task ConflictAlertModal_ClickDiscardAndReload_InvokesCallback()
    {
        var called = false;
        var cut = Render<ConflictAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Message, "Changed in another tab.")
            .Add(x => x.OnDiscardAndReload, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Find(".btn-primary").ClickAsync(new());

        Assert.True(called);
    }

    [Fact]
    public async Task ConflictAlertModal_ClickKeepEditing_InvokesCallback()
    {
        var called = false;
        var cut = Render<ConflictAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Message, "Changed in another tab.")
            .Add(x => x.OnKeepEditing, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Find(".btn-outline-secondary").ClickAsync(new());

        Assert.True(called);
    }

    [Fact]
    public void ConflictAlertModal_WhenShowFalse_DoesNotRender()
    {
        var cut = Render<ConflictAlertModal>(p => p.Add(x => x.Show, false));
        Assert.Empty(cut.FindAll(".modal"));
    }

    [Fact]
    public void ConflictAlertModal_ShowKeepEditingFalse_HidesKeepEditingButton()
    {
        var cut = Render<ConflictAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Message, "Changed.")
            .Add(x => x.ShowKeepEditing, false));

        Assert.Empty(cut.FindAll(".btn-outline-secondary"));
    }

    // ── M9-2: DeletionAlertModal (RemoteDeletionAlertModal) ──────────────────

    [Fact]
    public void RemoteDeletionAlertModal_RendersConfiguredTitleAndMessage()
    {
        var cut = Render<DeletionAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Title, "Organization Deleted")
            .Add(x => x.Message, "The organization was deleted in another tab."));

        Assert.Contains("Organization Deleted", cut.Find(".modal-header").TextContent);
        Assert.Contains("The organization was deleted in another tab", cut.Find(".modal-body").TextContent);
    }

    [Fact]
    public async Task RemoteDeletionAlertModal_DismissButton_InvokesOnDismissCallback()
    {
        var called = false;
        var cut = Render<DeletionAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Title, "Item Deleted")
            .Add(x => x.Message, "Gone.")
            .Add(x => x.OnDismiss, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Find(".btn-primary").ClickAsync(new());

        Assert.True(called);
    }

    [Fact]
    public void RemoteDeletionAlertModal_BlocksUntilDismissed_ShowsModalWhenShow()
    {
        var cut = Render<DeletionAlertModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Title, "Item Deleted")
            .Add(x => x.Message, "This item was deleted."));

        Assert.NotEmpty(cut.FindAll(".modal.d-block"));
    }

    [Fact]
    public void RemoteDeletionAlertModal_WhenShowFalse_DoesNotRender()
    {
        var cut = Render<DeletionAlertModal>(p => p.Add(x => x.Show, false));
        Assert.Empty(cut.FindAll(".modal"));
    }
}
