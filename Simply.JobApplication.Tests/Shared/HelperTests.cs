namespace Simply.JobApplication.Tests.Shared;

// M0-3: Tests for shared test helpers.
public class HelperTests
{
    [Fact]
    public async Task TestIndexedDbBuilder_WithOrganizations_MockReturnsConfiguredList()
    {
        var org = new Organization { Name = "Acme" };
        var db  = new TestIndexedDbBuilder().WithOrganizations(org).Build();

        var result = await db.GetAllOrganizationsAsync();

        Assert.Single(result);
        Assert.Equal("Acme", result[0].Name);
    }

    [Fact]
    public void DataSyncFake_Raise_InvokesAllSubscribedHandlers()
    {
        var fake   = new DataSyncFake();
        string? receivedId = null;
        string? receivedEvent = null;

        fake.OnOrganizationChanged += (id, ev) => { receivedId = id; receivedEvent = ev; };
        fake.Raise("organization", "org-1", "updated");

        Assert.Equal("org-1", receivedId);
        Assert.Equal("updated", receivedEvent);
    }

    [Fact]
    public void DataSyncFake_Raise_SessionCleared_InvokesOnSessionsCleared()
    {
        var fake = new DataSyncFake();
        var raised = false;
        fake.OnSessionsCleared += () => raised = true;

        fake.Raise("session", null, "cleared");

        Assert.True(raised);
    }

    [Fact]
    public void DataSyncFake_Raise_UnknownEntity_DoesNotThrow()
    {
        var fake = new DataSyncFake();
        var ex = Record.Exception(() => fake.Raise("unknown", null, "created"));
        Assert.Null(ex);
    }
}
