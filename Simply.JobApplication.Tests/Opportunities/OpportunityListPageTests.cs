namespace Simply.JobApplication.Tests.Opportunities;

// M4-2: OpportunityListPage — list display, filter, navigation, no add/delete, live updates.
public class OpportunityListPageTests : BunitContext
{
    private async Task<(IRenderedComponent<OpportunityListPage> cut, AppServiceMocks mocks)>
        Render(IIndexedDbService? db = null)
    {
        var mocks = this.AddAppServices(db);
        var cut   = Render<OpportunityListPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    [Fact]
    public async Task OpportunityListPage_EmptyState_ShowsMessage()
    {
        var (cut, _) = await Render();
        Assert.Contains("No opportunities yet", cut.Markup);
    }

    [Fact]
    public async Task OpportunityListPage_WithOpps_SortedByCreatedAtDescending()
    {
        var older = new Opportunity { Id = "op1", Role = "Older Role",
                        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var newer = new Opportunity { Id = "op2", Role = "Newer Role",
                        CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var db = new TestIndexedDbBuilder().WithOpportunities(older, newer).Build();
        var (cut, _) = await Render(db);

        var rows = cut.FindAll("tbody tr").ToList();
        Assert.True(rows.Count >= 2);
        Assert.Contains("Newer Role", rows[0].TextContent);
    }

    [Fact]
    public async Task OpportunityListPage_ShowsOrganizationLinkColumn()
    {
        var org = new Organization { Id = "o1", Name = "Acme Corp" };
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Dev",
                      CreatedAt = DateTime.UtcNow };
        var db  = new TestIndexedDbBuilder()
            .WithOpportunities(opp)
            .WithOrganizations(org)
            .Build();
        var (cut, _) = await Render(db);

        // Organization name shown as a link to org detail
        Assert.Contains(cut.FindAll("a"), a =>
            a.TextContent.Contains("Acme Corp") &&
            (a.GetAttribute("href") ?? "").Contains("/organizations/o1"));
    }

    [Fact]
    public async Task OpportunityListPage_NoAddOrDeleteActions()
    {
        var opp = new Opportunity { Id = "op1", Role = "Dev", CreatedAt = DateTime.UtcNow };
        var db  = new TestIndexedDbBuilder().WithOpportunities(opp).Build();
        var (cut, _) = await Render(db);

        // No "Add" buttons and no delete buttons
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Add"));
        Assert.DoesNotContain(cut.FindAll("button"), b =>
            (b.ClassName ?? "").Contains("btn-danger") ||
            (b.ClassName ?? "").Contains("btn-outline-danger"));
    }

    [Fact]
    public async Task OpportunityListPage_FilterInput_ShowsOnlyMatchingRows()
    {
        var opp1 = new Opportunity { Id = "op1", Role = "Frontend Dev",
                       CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var opp2 = new Opportunity { Id = "op2", Role = "Backend Dev",
                       CreatedAt = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc) };
        var db   = new TestIndexedDbBuilder().WithOpportunities(opp1, opp2).Build();
        var (cut, _) = await Render(db);

        cut.Find("input.form-control").Input("Frontend");

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            Assert.Single(rows);
            Assert.Contains("Frontend Dev", rows[0].TextContent);
        });
    }

    [Fact]
    public async Task OpportunityListPage_FilterInput_HighlightsMatchedText()
    {
        var opp = new Opportunity { Id = "op1", Role = "Frontend Dev",
                      CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var db  = new TestIndexedDbBuilder().WithOpportunities(opp).Build();
        var (cut, _) = await Render(db);

        cut.Find("input.form-control").Input("Front");

        cut.WaitForAssertion(() => Assert.Contains("<mark", cut.Markup));
    }

    [Fact]
    public async Task OpportunityListPage_RowClick_NavigatesToOpportunityDetail()
    {
        var opp = new Opportunity { Id = "op1", Role = "Dev", CreatedAt = DateTime.UtcNow };
        var db  = new TestIndexedDbBuilder().WithOpportunities(opp).Build();
        var (cut, _) = await Render(db);

        await cut.Find("tbody tr").ClickAsync(new());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/opportunities/op1", nav.Uri);
    }

    [Fact]
    public async Task OpportunityListPage_OnOppChanged_RefreshesList()
    {
        var db    = new TestIndexedDbBuilder().Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OpportunityListPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("No opportunities yet", cut.Markup);

        var newOpp = new Opportunity { Id = "op1", Role = "New Role", CreatedAt = DateTime.UtcNow };
        db.GetAllOpportunitiesAsync().Returns(Task.FromResult(new List<Opportunity> { newOpp }));

        mocks.DataSync.Raise("opportunity", "op1", "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("New Role"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("New Role", cut.Markup);
    }
}
