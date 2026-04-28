namespace Simply.JobApplication.Tests.Opportunities;

// M4-1: AddOpportunityPage — save, read-only org display, stage default, navigation guard.
public class AddOpportunityPageTests : BunitContext
{
    private async Task<(IRenderedComponent<AddOpportunityPage> cut, AppServiceMocks mocks)>
        Render(string orgId = "o1", IIndexedDbService? db = null)
    {
        var org = new Organization { Id = orgId, Name = "Acme Corp" };
        db ??= new TestIndexedDbBuilder().WithOrganization(org).Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<AddOpportunityPage>(p => p.Add(x => x.OrgId, orgId));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    [Fact]
    public async Task AddOpportunityPage_OrgNameShownAsReadOnly()
    {
        var (cut, _) = await Render();
        Assert.Contains("Acme Corp", cut.Markup);
        // The org name is in a .form-control-plaintext div (not an editable input)
        Assert.Contains(cut.FindAll(".form-control-plaintext"), e =>
            e.TextContent.Contains("Acme Corp"));
    }

    [Fact]
    public async Task AddOpportunityPage_StageDefaultsToOpen()
    {
        var (cut, _) = await Render();
        Assert.Contains("Open", cut.Find("select").TextContent);
    }

    [Fact]
    public async Task AddOpportunityPage_OnSave_NavigatesToOpportunityDetail()
    {
        var (cut, db) = await Render();

        // Fill the required Role field
        cut.Find("input.form-control").Input("Senior Developer");
        var saveBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        await saveBtn.ClickAsync(new());

        await db.Db.Received(1).SaveOpportunityAsync(
            Arg.Is<Opportunity>(o => o.Role == "Senior Developer" && o.OrganizationId == "o1"));

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("/opportunities/", nav.Uri);
    }

    [Fact]
    public async Task AddOpportunityPage_WhenUserFillsField_NavigationGuardActive()
    {
        var (cut, _) = await Render();

        cut.Find("input.form-control").Input("Some Role");

        cut.WaitForAssertion(() =>
        {
            var inv = JSInterop.Invocations
                .Where(i => i.Identifier == "sjaSetBeforeUnload").ToList();
            Assert.Contains(inv, i => i.Arguments.Any(a => a is true));
        });
    }

    [Fact]
    public async Task AddOpportunityPage_WhenNoFieldsFilled_NavigationGuardNotActive()
    {
        var (cut, _) = await Render();

        // No fields filled — IsDirty is false, no beforeunload(true) should be set
        cut.WaitForAssertion(() =>
        {
            var inv = JSInterop.Invocations
                .Where(i => i.Identifier == "sjaSetBeforeUnload").ToList();
            if (inv.Any())
                Assert.Contains(inv.Last().Arguments, a => a is false);
        });
    }

    [Fact]
    public async Task AddOpportunityPage_OnRemoteOrgDelete_ShowsAlertAndNavigatesToOrgList()
    {
        var (cut, mocks) = await Render();

        mocks.DataSync.Raise("organization", "o1", "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("deleted in another tab"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("deleted in another tab", cut.Markup);

        // Dismiss → navigate to org list
        await cut.Find(".btn-primary").ClickAsync(new()); // Dismiss button
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/organizations", nav.Uri);
    }
}
