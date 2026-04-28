namespace Simply.JobApplication.Tests.M1;

// M1-7: DataSyncService — message routing and broadcasting.
public class DataSyncTests
{
    // ── BroadcastAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task BroadcastAsync_PostsCorrectJsonPayload()
    {
        var js     = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));
        var svc = new DataSyncService(js);

        await svc.BroadcastAsync("organization", "org-1", "created");

        await module.Received(1).InvokeVoidAsync(
            "broadcast",
            Arg.Is<object[]?>(a => a != null
                && a[0].ToString() == "organization"
                && a[1].ToString() == "org-1"
                && a[2].ToString() == "created"));
    }

    // ── OnMessageReceived routing ─────────────────────────────────────────────

    [Fact]
    public void OnMessageReceived_Organization_RaisesOnOrganizationChangedEvent()
    {
        var svc = new DataSyncService(Substitute.For<IJSRuntime>());
        string? gotId = null; string? gotEv = null;
        svc.OnOrganizationChanged += (id, ev) => { gotId = id; gotEv = ev; };

        svc.OnMessageReceived("organization", "org-1", "updated");

        Assert.Equal("org-1",   gotId);
        Assert.Equal("updated", gotEv);
    }

    [Fact]
    public void OnMessageReceived_Opportunity_RaisesOnOpportunityChangedEvent()
    {
        var svc = new DataSyncService(Substitute.For<IJSRuntime>());
        string? gotId = null;
        svc.OnOpportunityChanged += (id, _) => gotId = id;

        svc.OnMessageReceived("opportunity", "opp-2", "created");

        Assert.Equal("opp-2", gotId);
    }

    [Fact]
    public void OnMessageReceived_BulkClearSession_RaisesSessionClearedEvent()
    {
        var svc = new DataSyncService(Substitute.For<IJSRuntime>());
        var raised = false;
        svc.OnSessionsCleared += () => raised = true;

        svc.OnMessageReceived("session", null, "cleared");

        Assert.True(raised);
    }

    [Fact]
    public void OnMessageReceived_UnknownEntity_DoesNotThrow()
    {
        var svc = new DataSyncService(Substitute.For<IJSRuntime>());
        var ex = Record.Exception(() => svc.OnMessageReceived("unknownEntity", null, "created"));
        Assert.Null(ex);
    }
}
