namespace Simply.JobApplication.Tests.Organizations;

// M3-1: OrganizationListPage — list display, filter, delete, navigation, live updates.
public class OrganizationListPageTests : BunitContext
{
    private async Task<(IRenderedComponent<OrganizationListPage> cut, AppServiceMocks mocks)>
        Render(IIndexedDbService? db = null)
    {
        var mocks = this.AddAppServices(db);
        var cut   = Render<OrganizationListPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    [Fact]
    public async Task OrganizationListPage_WhenNoOrgs_ShowsEmptyState()
    {
        var (cut, _) = await Render();
        Assert.Contains("No organizations yet", cut.Markup);
    }

    [Fact]
    public async Task OrganizationListPage_WithOrgs_SortedByNameAscending()
    {
        var db = new TestIndexedDbBuilder()
            .WithOrganizations(
                new Organization { Id = "o1", Name = "Zebra Corp" },
                new Organization { Id = "o2", Name = "Alpha Inc" })
            .Build();
        var (cut, _) = await Render(db);

        var rows = cut.FindAll("tbody tr").ToList();
        Assert.True(rows.Count >= 2);
        Assert.Contains("Alpha Inc", rows[0].TextContent);
    }

    [Fact]
    public async Task OrganizationListPage_ShowsCorrectOpportunityCount()
    {
        var org = new Organization { Id = "o1", Name = "Acme" };
        var opp1 = new Opportunity { Id = "op1", OrganizationId = "o1" };
        var opp2 = new Opportunity { Id = "op2", OrganizationId = "o1" };
        var db = new TestIndexedDbBuilder()
            .WithOrganizations(org)
            .WithOpportunities(opp1, opp2)
            .Build();
        var (cut, _) = await Render(db);

        var row = cut.Find("tbody tr");
        Assert.Contains("2", row.TextContent);
    }

    [Fact]
    public async Task OrganizationListPage_ShowsCorrectContactCount()
    {
        var org = new Organization { Id = "o1", Name = "Acme" };
        var counts = new Dictionary<string, int> { ["o1"] = 3 };
        var db = new TestIndexedDbBuilder()
            .WithOrganizations(org)
            .WithContactCounts(counts)
            .Build();
        var (cut, _) = await Render(db);

        var row = cut.Find("tbody tr");
        Assert.Contains("3", row.TextContent);
    }

    [Fact]
    public async Task OrganizationListPage_FilterInput_ShowsOnlyMatchingRows()
    {
        var db = new TestIndexedDbBuilder()
            .WithOrganizations(
                new Organization { Id = "o1", Name = "Acme Corp" },
                new Organization { Id = "o2", Name = "Beta LLC" })
            .Build();
        var (cut, _) = await Render(db);

        cut.Find("input.form-control").Input("Acme");

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            Assert.Single(rows);
            Assert.Contains("Acme Corp", rows[0].TextContent);
        });
    }

    [Fact]
    public async Task OrganizationListPage_FilterInput_IsCaseInsensitive()
    {
        var db = new TestIndexedDbBuilder()
            .WithOrganizations(new Organization { Id = "o1", Name = "Acme Corp" })
            .Build();
        var (cut, _) = await Render(db);

        cut.Find("input.form-control").Input("acme");

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("tbody tr")));
    }

    [Fact]
    public async Task OrganizationListPage_FilterInput_HighlightsMatchedText()
    {
        var db = new TestIndexedDbBuilder()
            .WithOrganizations(new Organization { Id = "o1", Name = "Acme Corp" })
            .Build();
        var (cut, _) = await Render(db);

        cut.Find("input.form-control").Input("Acme");

        cut.WaitForAssertion(() =>
            Assert.Contains("<mark>", cut.Markup));
    }

    [Fact]
    public async Task OrganizationListPage_DeleteClick_ShowsConfirmDialog()
    {
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder().WithOrganizations(org).Build();
        var (cut, _) = await Render(db);

        await cut.Find("button[title='Delete Acme']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Delete Organization", cut.Markup));
    }

    [Fact]
    public async Task OrganizationListPage_DeleteConfirmed_CallsCascadeDelete()
    {
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder().WithOrganizations(org).Build();
        var (cut, _) = await Render(db);

        await cut.Find("button[title='Delete Acme']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Organization", cut.Markup));

        await cut.Find(".btn-danger").ClickAsync(new());

        await db.Received(1).DeleteOrganizationCascadeAsync("o1");
    }

    [Fact]
    public async Task OrganizationListPage_DeleteCancelled_DoesNotDelete()
    {
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder().WithOrganizations(org).Build();
        var (cut, _) = await Render(db);

        await cut.Find("button[title='Delete Acme']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Organization", cut.Markup));

        await cut.Find(".btn-secondary").ClickAsync(new());

        await db.DidNotReceive().DeleteOrganizationCascadeAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task OrganizationListPage_RowClick_NavigatesToOrgDetail()
    {
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder().WithOrganizations(org).Build();
        var (cut, _) = await Render(db);

        await cut.Find("tbody tr").ClickAsync(new());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/organizations/o1", nav.Uri);
    }

    [Fact]
    public async Task OrganizationListPage_OnOrgCreated_RefreshesList()
    {
        var db    = new TestIndexedDbBuilder().Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OrganizationListPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("No organizations yet", cut.Markup);

        db.GetAllBaseResumesAsync().Returns(Task.FromResult(new List<BaseResume>()));
        var newOrg = new Organization { Id = "o1", Name = "Acme" };
        db.GetAllOrganizationsAsync().Returns(Task.FromResult(new List<Organization> { newOrg }));

        mocks.DataSync.Raise("organization", newOrg.Id, "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Acme"), TimeSpan.FromSeconds(2));
        Assert.Contains("Acme", cut.Markup);
    }
}
