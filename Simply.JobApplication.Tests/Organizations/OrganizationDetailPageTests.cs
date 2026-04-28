namespace Simply.JobApplication.Tests.Organizations;

// M3-3 through M3-7: OrganizationDetailPage — details, contacts, opportunities, sessions, live updates.
public class OrganizationDetailPageTests : BunitContext
{
    private static Organization MakeOrg(string id = "o1", string name = "Acme Corp") =>
        new() { Id = id, Name = name, Industry = "Tech", Size = "100–500",
                Website = "https://acme.com", LinkedIn = "https://linkedin.com/company/acme",
                Description = "A test org", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private async Task<(IRenderedComponent<OrganizationDetailPage> cut, AppServiceMocks mocks)>
        Render(Organization org, IIndexedDbService? db = null)
    {
        db ??= new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OrganizationDetailPage>(p => p.Add(x => x.Id, org.Id));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    private static async Task EnterEditMode(IRenderedComponent<OrganizationDetailPage> cut)
    {
        var editBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "Edit");
        await editBtn.ClickAsync(new());
    }

    // ── M3-3: Details card ────────────────────────────────────────────────────

    [Fact]
    public async Task DetailsCard_DisplaysAllOrgFields()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);

        Assert.Contains("Tech", cut.Markup);
        Assert.Contains("100–500", cut.Markup);
        Assert.Contains("https://acme.com", cut.Markup);
        Assert.Contains("A test org", cut.Markup);
    }

    [Fact]
    public async Task EditMode_NameValidation_ShowsErrorWhenConflictWithOtherOrg()
    {
        var org1 = MakeOrg("o1", "Acme Corp");
        var org2 = MakeOrg("o2", "Beta LLC");
        var db   = new TestIndexedDbBuilder()
            .WithOrganization(org1)
            .WithOrganizations(org1, org2)
            .Build();
        var (cut, _) = await Render(org1, db);
        await EnterEditMode(cut);

        // Name input in edit mode is the first input inside the card-body
        var nameInput = cut.FindAll("input.form-control").First();
        nameInput.Input("Beta LLC");

        cut.WaitForAssertion(() =>
            Assert.Contains("already exists", cut.Markup));
    }

    [Fact]
    public async Task EditMode_NameValidation_IsCaseInsensitive()
    {
        var org1 = MakeOrg("o1", "Acme Corp");
        var org2 = MakeOrg("o2", "Beta LLC");
        var db   = new TestIndexedDbBuilder()
            .WithOrganization(org1)
            .WithOrganizations(org1, org2)
            .Build();
        var (cut, _) = await Render(org1, db);
        await EnterEditMode(cut);

        cut.FindAll("input.form-control").First().Input("beta llc");

        cut.WaitForAssertion(() => Assert.Contains("already exists", cut.Markup));
    }

    [Fact]
    public async Task EditMode_NameValidation_AllowsSameOrgName()
    {
        var org = MakeOrg("o1", "Acme Corp");
        var (cut, _) = await Render(org);
        await EnterEditMode(cut);

        // Type the org's own name — no conflict
        cut.FindAll("input.form-control").First().Input("Acme Corp");

        cut.WaitForAssertion(() => Assert.DoesNotContain("already exists", cut.Markup));
    }

    [Fact]
    public async Task EditMode_Save_PersistsChanges()
    {
        var org = MakeOrg();
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        var (cut, _) = await Render(org, db);
        await EnterEditMode(cut);

        cut.FindAll("input.form-control").First().Input("Updated Name");

        var saveBtn = cut.FindAll("button").First(b =>
            b.TextContent.Trim() == "Save" || b.TextContent.Contains("Saving"));
        await saveBtn.ClickAsync(new());

        await db.Received(1).VersionedWriteAsync(
            "organizations", Arg.Any<Organization>(), Arg.Any<string[]?>());
    }

    [Fact]
    public async Task EditMode_Cancel_RevertsChanges()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);
        await EnterEditMode(cut);

        cut.FindAll("input.form-control").First().Input("Changed Name");
        var cancelBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "Cancel");
        await cancelBtn.ClickAsync(new());

        // Back to view mode — original name shown
        Assert.Contains("Acme Corp", cut.Markup);
        Assert.DoesNotContain("Changed Name", cut.Markup);
    }

    [Fact]
    public async Task EditMode_DisablesContactsCardActions()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);
        await EnterEditMode(cut);

        // The "+ Add Contact" button should not be visible during edit mode
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Add Contact"));
    }

    [Fact]
    public async Task EditMode_DisablesOpportunitiesCardActions()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);
        await EnterEditMode(cut);

        // The "+ Add Opportunity" link should not be visible during edit mode
        Assert.DoesNotContain(cut.FindAll("a"), a => a.TextContent.Contains("Add Opportunity"));
    }

    [Fact]
    public async Task EditMode_NavigationGuardActive_WhenNameChanged()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);
        await EnterEditMode(cut);

        cut.FindAll("input.form-control").First().Input("Changed Name");

        var nav = Services.GetRequiredService<NavigationManager>();
        _ = cut.InvokeAsync(() => nav.NavigateTo("/organizations"));
        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Any(m =>
            m.TextContent.Contains("Unsaved Changes")), TimeSpan.FromSeconds(2));

        Assert.Contains(cut.FindAll(".modal.d-block"),
            m => m.TextContent.Contains("Unsaved Changes"));
    }

    // ── M3-4: Contacts card ───────────────────────────────────────────────────

    [Fact]
    public async Task ContactsCard_EmptyState_ShowsMessage()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);

        Assert.Contains("No contacts yet", cut.Markup);
    }

    [Fact]
    public async Task ContactsCard_WithContacts_SortedByNameAscending()
    {
        var org = MakeOrg();
        var c1  = new Contact { Id = "c1", OrganizationId = "o1", FullName = "Zebra Jones" };
        var c2  = new Contact { Id = "c2", OrganizationId = "o1", FullName = "Alpha Smith" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithContacts("o1", c1, c2)
            .Build();
        var (cut, _) = await Render(org, db);

        var rows = cut.FindAll("tbody tr").ToList();
        Assert.True(rows.Count >= 2);
        Assert.Contains("Alpha Smith", rows[0].TextContent);
    }

    [Fact]
    public async Task AddContactModal_DuplicateNameInOrg_ShowsNonBlockingWarning()
    {
        var org = MakeOrg();
        var c1  = new Contact { Id = "c1", OrganizationId = "o1", FullName = "John Doe" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithContacts("o1", c1)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button.btn-outline-primary").ClickAsync(new()); // Add Contact
        cut.WaitForAssertion(() => Assert.Contains("Add Contact", cut.Markup));

        // Type a duplicate name — warning appears immediately via oninput
        cut.Find(".modal input.form-control").Input("John Doe");

        cut.WaitForAssertion(() => Assert.Contains("already exists in this organization", cut.Markup));
    }

    [Fact]
    public async Task AddContactModal_DuplicateNameInOrg_SaveStillProceeds()
    {
        var org = MakeOrg();
        var c1  = new Contact { Id = "c1", OrganizationId = "o1", FullName = "John Doe" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithContacts("o1", c1)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button.btn-outline-primary").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Add Contact", cut.Markup));

        cut.Find(".modal input.form-control").Input("John Doe");
        await cut.Find(".modal .btn-primary").ClickAsync(new());

        await db.Received(1).SaveContactAsync(Arg.Any<Contact>());
    }

    [Fact]
    public async Task AddContactModal_Save_SetsOrganizationId()
    {
        var org = MakeOrg();
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button.btn-outline-primary").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Add Contact", cut.Markup));

        cut.Find(".modal input.form-control").Input("Jane Smith");
        await cut.Find(".modal .btn-primary").ClickAsync(new());

        await db.Received(1).SaveContactAsync(
            Arg.Is<Contact>(c => c.OrganizationId == "o1" && c.FullName == "Jane Smith"));
    }

    [Fact]
    public async Task EditContactModal_PreFillsExistingFields()
    {
        var org = MakeOrg();
        var c1  = new Contact { Id = "c1", OrganizationId = "o1", FullName = "Jane Smith",
                                Title = "Manager", Email = "jane@example.com" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithContacts("o1", c1)
            .Build();
        var (cut, _) = await Render(org, db);

        // Click on the contact row to open edit modal
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Edit Contact", cut.Markup));

        Assert.Contains("Jane Smith", cut.Markup);
        Assert.Contains("Manager", cut.Markup);
    }

    [Fact]
    public async Task DeleteContactModal_ShowsConfirmDialog()
    {
        var org = MakeOrg();
        var c1  = new Contact { Id = "c1", OrganizationId = "o1", FullName = "John Doe" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithContacts("o1", c1)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button[title='Delete John Doe']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Delete Contact", cut.Markup));
    }

    [Fact]
    public async Task DeleteContact_Confirmed_DeletesContactOpportunityRoles()
    {
        var org = MakeOrg();
        var c1  = new Contact { Id = "c1", OrganizationId = "o1", FullName = "John Doe" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithContacts("o1", c1)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button[title='Delete John Doe']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Contact", cut.Markup));
        await cut.Find(".btn-danger").ClickAsync(new());

        await db.Received(1).DeleteContactCascadeAsync("c1");
    }

    [Fact]
    public async Task DeleteContact_Confirmed_LeavesCorrespondenceUntouched()
    {
        var org = MakeOrg();
        var c1  = new Contact { Id = "c1", OrganizationId = "o1", FullName = "John Doe" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithContacts("o1", c1)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button[title='Delete John Doe']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Contact", cut.Markup));
        await cut.Find(".btn-danger").ClickAsync(new());

        await db.DidNotReceive().DeleteCorrespondenceAsync(Arg.Any<string>());
        await db.DidNotReceive().DeleteCorrespondenceCascadeAsync(Arg.Any<string>());
    }

    // ── M3-5: Opportunities card ──────────────────────────────────────────────

    [Fact]
    public async Task OpportunitiesCard_EmptyState_ShowsMessage()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);

        Assert.Contains("No opportunities yet", cut.Markup);
    }

    [Fact]
    public async Task OpportunitiesCard_WithOpportunities_SortedByCreatedAtDescending()
    {
        var org  = MakeOrg();
        var opp1 = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Older Role",
                       CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var opp2 = new Opportunity { Id = "op2", OrganizationId = "o1", Role = "Newer Role",
                       CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var db   = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithOpportunities(opp1, opp2)
            .Build();
        db.GetOpportunitiesByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Opportunity> { opp1, opp2 }));
        var (cut, _) = await Render(org, db);

        var rows = cut.FindAll("tbody tr").ToList();
        Assert.True(rows.Count >= 2);
        Assert.Contains("Newer Role", rows[0].TextContent);
    }

    [Fact]
    public async Task OpportunitiesCard_RowClick_NavigatesToOpportunityDetail()
    {
        var org = MakeOrg();
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Dev" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        db.GetOpportunitiesByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Opportunity> { opp }));
        var (cut, _) = await Render(org, db);

        await cut.FindAll("tbody tr").First().ClickAsync(new());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/opportunities/op1", nav.Uri);
    }

    [Fact]
    public async Task DeleteOpportunityModal_ShowsConfirmDialog()
    {
        var org = MakeOrg();
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Dev Role" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        db.GetOpportunitiesByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Opportunity> { opp }));
        var (cut, _) = await Render(org, db);

        await cut.Find("button[title='Delete Dev Role']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Delete Opportunity", cut.Markup));
    }

    [Fact]
    public async Task DeleteOpportunity_Confirmed_CascadesCorrespondenceAndSessions()
    {
        var org = MakeOrg();
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Dev Role" };
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        db.GetOpportunitiesByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Opportunity> { opp }));
        var (cut, _) = await Render(org, db);

        await cut.Find("button[title='Delete Dev Role']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Opportunity", cut.Markup));
        await cut.Find(".btn-danger").ClickAsync(new());

        await db.Received(1).DeleteOpportunityCascadeAsync("op1");
    }

    // ── M3-6: E&G Sessions card ───────────────────────────────────────────────

    [Fact]
    public async Task EGSessionsCard_EmptyState_ShowsMessage()
    {
        var org = MakeOrg();
        var (cut, _) = await Render(org);

        Assert.Contains("No E&amp;G sessions yet", cut.Markup);
    }

    [Fact]
    public async Task EGSessionsCard_ArtifactsGeneratedColumn_ShowsCheckmarkWhenTrue()
    {
        var org = MakeOrg();
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1",
            ArtifactsGenerated = true,
            CreatedAt = DateTime.UtcNow,
        };
        var db = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithSessions(session)
            .Build();
        var (cut, _) = await Render(org, db);

        // CheckIcon has aria-label="Yes" and class="text-success"
        Assert.Contains("text-success", cut.Markup);
    }

    [Fact]
    public async Task EGSessionsCard_FieldMatchColumn_ShowsCheckmarkWhenSessionRoleMatchesOpportunity()
    {
        var org = MakeOrg();
        var opp = new Opportunity
        {
            Id = "op1", OrganizationId = "o1",
            Role = "Senior Dev", RoleDescription = "Build things",
        };
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OpportunityId = "op1",
            Role = "Senior Dev", RoleDescription = "Build things",
            ArtifactsGenerated = false, CreatedAt = DateTime.UtcNow,
        };
        var db = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithSessions(session)
            .Build();
        db.GetOpportunitiesByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Opportunity> { opp }));
        var (cut, _) = await Render(org, db);

        // Field match checkmark appears (text-success from CheckIcon)
        Assert.Contains("text-success", cut.Markup);
    }

    [Fact]
    public async Task EGSessionsCard_FieldMatchColumn_NoCheckmarkWhenFieldsDiffer()
    {
        var org = MakeOrg();
        var opp = new Opportunity
        {
            Id = "op1", OrganizationId = "o1",
            Role = "Senior Dev", RoleDescription = "Current description",
        };
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OpportunityId = "op1",
            Role = "Junior Dev", RoleDescription = "Old description",
            ArtifactsGenerated = false, CreatedAt = DateTime.UtcNow,
        };
        var db = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithSessions(session)
            .Build();
        db.GetOpportunitiesByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Opportunity> { opp }));
        var (cut, _) = await Render(org, db);

        // No checkmark — no text-success in the markup (ArtifactsGenerated is false too)
        Assert.DoesNotContain("text-success", cut.Markup);
    }

    [Fact]
    public async Task DeleteSessionModal_ShowsConfirmDialog()
    {
        var org     = MakeOrg();
        var session = new SessionRecord { Id = "s1", OrganizationId = "o1", CreatedAt = DateTime.UtcNow };
        var db      = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithSessions(session)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button[title='Delete session']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Session", cut.Markup));
    }

    [Fact]
    public async Task DeleteSession_Confirmed_DeletesSessionAndFiles()
    {
        var org     = MakeOrg();
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", CreatedAt = DateTime.UtcNow,
            TailoredResumeFileId = "f1", CoverLetterFileId = "f2",
        };
        var db = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithSessions(session)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.Find("button[title='Delete session']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Session", cut.Markup));
        await cut.Find(".btn-danger").ClickAsync(new());

        await db.Received(1).DeleteSessionAsync("s1");
        await db.Received(1).DeleteFileAsync("f1");
        await db.Received(1).DeleteFileAsync("f2");
    }

    [Fact]
    public async Task EGSessionsCard_RowClick_NavigatesToHistoryDetail()
    {
        var org     = MakeOrg();
        var session = new SessionRecord { Id = "s1", OrganizationId = "o1", CreatedAt = DateTime.UtcNow };
        var db      = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .WithSessions(session)
            .Build();
        var (cut, _) = await Render(org, db);

        await cut.FindAll("tbody tr").First().ClickAsync(new());

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/history/s1", nav.Uri);
    }

    // ── M3-7: BroadcastChannel live updates ──────────────────────────────────

    [Fact]
    public async Task OrgDetailPage_OnRemoteOrgUpdate_RefreshesDetailsCard()
    {
        var org = MakeOrg("o1", "Old Name");
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OrganizationDetailPage>(p => p.Add(x => x.Id, "o1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("Old Name", cut.Markup);

        // Simulate remote update: return updated org
        var updated = MakeOrg("o1", "New Name");
        db.GetOrganizationAsync("o1").Returns(Task.FromResult<Organization?>(updated));

        mocks.DataSync.Raise("organization", "o1", "updated");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("New Name"), TimeSpan.FromSeconds(2));
        Assert.Contains("New Name", cut.Markup);
    }

    [Fact]
    public async Task OrgDetailPage_OnRemoteOrgUpdate_WhileEditing_ShowsConflictAlert()
    {
        var org = MakeOrg();
        var (cut, mocks) = await Render(org);
        await EnterEditMode(cut);

        mocks.DataSync.Raise("organization", "o1", "updated");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Record Changed"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Record Changed", cut.Markup);
    }

    [Fact]
    public async Task OrgDetailPage_OnRemoteOrgDelete_ShowsDeletionAlert()
    {
        var org  = MakeOrg();
        var (cut, mocks) = await Render(org);

        mocks.DataSync.Raise("organization", "o1", "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("deleted in another tab"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("deleted in another tab", cut.Markup);
    }

    [Fact]
    public async Task OrgDetailPage_OnContactChanged_RefreshesContactsCard()
    {
        var org = MakeOrg();
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OrganizationDetailPage>(p => p.Add(x => x.Id, "o1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        var newContact = new Contact { Id = "c1", OrganizationId = "o1", FullName = "Jane Smith" };
        db.GetContactsByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Contact> { newContact }));

        mocks.DataSync.Raise("contact", "c1", "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Jane Smith"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Jane Smith", cut.Markup);
    }

    [Fact]
    public async Task OrgDetailPage_OnOpportunityChanged_RefreshesOpportunitiesCard()
    {
        var org = MakeOrg();
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OrganizationDetailPage>(p => p.Add(x => x.Id, "o1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        var newOpp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Dev Lead" };
        db.GetOpportunitiesByOrganizationAsync("o1").Returns(
            Task.FromResult(new List<Opportunity> { newOpp }));

        mocks.DataSync.Raise("opportunity", "op1", "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Dev Lead"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Dev Lead", cut.Markup);
    }

    [Fact]
    public async Task OrgDetailPage_OnSessionChanged_RefreshesEGSessionsCard()
    {
        var org = MakeOrg();
        var db  = new TestIndexedDbBuilder()
            .WithOrganization(org)
            .WithOrganizations(org)
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OrganizationDetailPage>(p => p.Add(x => x.Id, "o1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1",
            MatchScore = "92%", CreatedAt = DateTime.UtcNow,
        };
        db.GetAllSessionsAsync().Returns(Task.FromResult(new List<SessionRecord> { session }));

        mocks.DataSync.Raise("session", "s1", "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("92%"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("92%", cut.Markup);
    }
}
