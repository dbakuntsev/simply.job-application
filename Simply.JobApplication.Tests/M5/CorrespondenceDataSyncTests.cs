namespace Simply.JobApplication.Tests.M5;

// M5-5: Correspondence live updates — remote delete/update of open entry, remote opp/org delete.
public class CorrespondenceDataSyncTests : BunitContext
{
    private static Opportunity MakeOpp() => new()
    {
        Id = "op1", OrganizationId = "o1", Role = "Dev",
        Stage = OpportunityStage.Open,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static Organization MakeOrg() => new() { Id = "o1", Name = "Acme Corp" };

    private static Correspondence MakeCorr() => new()
    {
        Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
        OccurredAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    /// Renders with one correspondence entry and opens its edit modal.
    private async Task<(IRenderedComponent<OpportunityDetailPage> cut, AppServiceMocks mocks)>
        RenderWithEditModalOpen(IIndexedDbService? db = null)
    {
        var corr = MakeCorr();
        db ??= new TestIndexedDbBuilder()
            .WithOpportunity(MakeOpp())
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr)
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Edit Email", cut.Markup));

        return (cut, mocks);
    }

    [Fact]
    public async Task CorrespondenceEditModal_OnRemoteDeleteOfSameEntry_ShowsDeletionAlert_ClosesModal()
    {
        var (cut, mocks) = await RenderWithEditModalOpen();

        mocks.DataSync.Raise("correspondence", "c1", "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("deleted in another tab"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("deleted in another tab", cut.Markup);
        // The correspondence edit modal should be closed
        Assert.DoesNotContain(cut.FindAll(".modal.d-block"),
            m => m.TextContent.Contains("Edit Email"));
    }

    [Fact]
    public async Task CorrespondenceEditModal_OnRemoteUpdateOfSameEntry_ShowsConflictAlert()
    {
        var (cut, mocks) = await RenderWithEditModalOpen();

        mocks.DataSync.Raise("correspondence", "c1", "updated");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Record Changed"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Record Changed", cut.Markup);
    }

    [Fact]
    public async Task CorrespondenceEditModal_OnRemoteOppDelete_ShowsDeletionAlert()
    {
        var (cut, mocks) = await RenderWithEditModalOpen();

        mocks.DataSync.Raise("opportunity", "op1", "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("deleted in another tab"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("deleted in another tab", cut.Markup);
    }

    [Fact]
    public async Task CorrespondenceEditModal_OnRemoteOrgDelete_ShowsDeletionAlert()
    {
        var (cut, mocks) = await RenderWithEditModalOpen();

        mocks.DataSync.Raise("organization", "o1", "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("deleted in another tab"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("deleted in another tab", cut.Markup);
    }

    [Fact]
    public async Task CorrespondenceList_OnRemoteDeleteDifferentEntry_RefreshesSection()
    {
        var corr1 = new Correspondence
        {
            Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            Body = "First email body",
            OccurredAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var corr2 = new Correspondence
        {
            Id = "c2", OpportunityId = "op1", Type = CorrespondenceType.Email,
            Body = "Second email body",
            OccurredAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var db = new TestIndexedDbBuilder()
            .WithOpportunity(MakeOpp())
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr1, corr2)
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        Assert.Contains("First email body", cut.Markup);
        Assert.Contains("Second email body", cut.Markup);

        // Simulate remote delete of corr2; update the mock to return only corr1
        db.GetCorrespondenceByOpportunityAsync("op1")
            .Returns(Task.FromResult(new List<Correspondence> { corr1 }));

        mocks.DataSync.Raise("correspondence", "c2", "deleted");

        await cut.WaitForStateAsync(
            () => !cut.Markup.Contains("Second email body"),
            TimeSpan.FromSeconds(2));
        Assert.DoesNotContain("Second email body", cut.Markup);
        Assert.Contains("First email body", cut.Markup);
    }
}
