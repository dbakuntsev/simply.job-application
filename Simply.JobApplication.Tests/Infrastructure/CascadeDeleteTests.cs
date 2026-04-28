namespace Simply.JobApplication.Tests.Infrastructure;

// M1-9: Cascade deletes — each method routes to the correct single JS function.
public class CascadeDeleteTests
{
    private static (IndexedDbService svc, IJSObjectReference module) MakeService()
    {
        var js     = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));
        return (new IndexedDbService(js), module);
    }

    [Fact]
    public async Task DeleteOrganizationCascadeAsync_DeletesContacts_ContactRoles_Opportunities_OrgLinkedSessions_ThenOrg()
    {
        var (svc, module) = MakeService();
        await svc.DeleteOrganizationCascadeAsync("org-1");

        await module.Received(1).InvokeVoidAsync(
            "deleteOrganizationCascade",
            Arg.Is<object[]?>(a => a != null && a[0].ToString() == "org-1"));
    }

    [Fact]
    public async Task DeleteOpportunityCascadeAsync_DeletesContactRoles_History_Correspondence_Files_OppLinkedSessions_ThenOpp()
    {
        var (svc, module) = MakeService();
        await svc.DeleteOpportunityCascadeAsync("opp-1");

        await module.Received(1).InvokeVoidAsync(
            "deleteOpportunityCascade",
            Arg.Is<object[]?>(a => a != null && a[0].ToString() == "opp-1"));
    }

    [Fact]
    public async Task DeleteContactCascadeAsync_DeletesContactRoles_LeavesCorrespondenceUntouched()
    {
        var (svc, module) = MakeService();
        await svc.DeleteContactCascadeAsync("c-1");

        await module.Received(1).InvokeVoidAsync(
            "deleteContactCascade",
            Arg.Is<object[]?>(a => a != null && a[0].ToString() == "c-1"));
        // Correspondence deletion NOT called separately — JS handles exclusion
        await module.DidNotReceive().InvokeVoidAsync("deleteCorrespondence", Arg.Any<object[]?>());
    }

    [Fact]
    public async Task DeleteCorrespondenceCascadeAsync_DeletesFiles_ThenCorrespondenceRecord()
    {
        var (svc, module) = MakeService();
        await svc.DeleteCorrespondenceCascadeAsync("corr-1");

        await module.Received(1).InvokeVoidAsync(
            "deleteCorrespondenceCascade",
            Arg.Is<object[]?>(a => a != null && a[0].ToString() == "corr-1"));
    }
}
