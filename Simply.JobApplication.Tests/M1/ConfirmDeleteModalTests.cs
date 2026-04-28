namespace Simply.JobApplication.Tests.M1;

// M1-11: ConfirmDeleteModal component tests.
public class ConfirmDeleteModalTests : BunitContext
{
    [Fact]
    public void ConfirmDeleteModal_RendersConfiguredTitle()
    {
        var cut = Render<ConfirmDeleteModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Title, "Delete Account"));

        Assert.Contains("Delete Account", cut.Find(".modal-title").TextContent);
    }

    [Fact]
    public void ConfirmDeleteModal_RendersConfiguredMessage()
    {
        var cut = Render<ConfirmDeleteModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.Message, "This will remove everything permanently."));

        Assert.Contains("This will remove everything permanently.", cut.Find(".modal-body").TextContent);
    }

    [Fact]
    public async Task ConfirmDeleteModal_ClickDelete_InvokesOnConfirmCallback()
    {
        var confirmed = false;
        var cut = Render<ConfirmDeleteModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OnConfirm, EventCallback.Factory.Create(this, () => confirmed = true)));

        await cut.Find(".btn-danger").ClickAsync(new());

        Assert.True(confirmed);
    }

    [Fact]
    public async Task ConfirmDeleteModal_ClickCancel_InvokesOnCancelCallback()
    {
        var cancelled = false;
        var cut = Render<ConfirmDeleteModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OnCancel, EventCallback.Factory.Create(this, () => cancelled = true)));

        await cut.Find(".btn-secondary").ClickAsync(new());

        Assert.True(cancelled);
    }

    [Fact]
    public async Task ConfirmDeleteModal_ClickCancel_DoesNotInvokeOnConfirm()
    {
        var confirmed = false;
        var cut = Render<ConfirmDeleteModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OnConfirm, EventCallback.Factory.Create(this, () => confirmed = true))
            .Add(x => x.OnCancel, EventCallback.Empty));

        await cut.Find(".btn-secondary").ClickAsync(new());

        Assert.False(confirmed);
    }

    [Fact]
    public void ConfirmDeleteModal_DeleteButtonLabel_IsConfigurable()
    {
        var cut = Render<ConfirmDeleteModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.DeleteLabel, "Remove Forever"));

        Assert.Contains("Remove Forever", cut.Find(".btn-danger").TextContent);
    }

    [Fact]
    public void ConfirmDeleteModal_WhenShowFalse_DoesNotRender()
    {
        var cut = Render<ConfirmDeleteModal>(p => p.Add(x => x.Show, false));
        Assert.Empty(cut.FindAll(".modal"));
    }
}
