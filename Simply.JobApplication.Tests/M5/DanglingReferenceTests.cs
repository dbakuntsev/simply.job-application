namespace Simply.JobApplication.Tests.M5;

// M5-4: Dangling references — deleted contact shown in correspondence list and edit modal.
public class DanglingReferenceTests : BunitContext
{
    private static Opportunity MakeOpp() => new()
    {
        Id = "op1", OrganizationId = "o1", Role = "Dev",
        Stage = OpportunityStage.Open,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static Organization MakeOrg() => new() { Id = "o1", Name = "Acme Corp" };

    [Fact]
    public async Task CorrespondenceList_DeletedContact_ShowsDanglingLabel()
    {
        // Correspondence references a contact ID that no longer exists in the org
        var corr = new Correspondence
        {
            Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            ContactId = "deleted-contact-id",
            OccurredAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var db = new TestIndexedDbBuilder()
            .WithOpportunity(MakeOpp())
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr)
            // No contacts for this org — the referenced contact is effectively deleted
            .Build();
        this.AddAppServices(db);
        var cut = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        // The contact column should display "(deleted)"
        var row = cut.Find("tbody tr");
        Assert.Contains("(deleted)", row.TextContent);
    }

    [Fact]
    public async Task CorrespondenceEditModal_DeletedContact_ShowsDanglingOption()
    {
        // Correspondence references a contact that no longer belongs to the org
        var corr = new Correspondence
        {
            Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            ContactId = "gone-contact-id",
            OccurredAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var db = new TestIndexedDbBuilder()
            .WithOpportunity(MakeOpp())
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr)
            // No contacts registered — dangling reference
            .Build();
        this.AddAppServices(db);
        var cut = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        // Open the edit modal by clicking the correspondence row
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Edit Email", cut.Markup));

        // The contact <select> should show a "(deleted)" option for the dangling contact
        var contactSelect = cut.FindAll("select")
            .First(s => s.TextContent.Contains("— none —"));
        Assert.Contains("(deleted)", contactSelect.TextContent);
    }
}
