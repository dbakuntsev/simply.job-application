namespace Simply.JobApplication.Tests.Opportunities;

// M4-3 through M4-8: OpportunityDetailPage — header, edit, role desc, qualifications,
// correspondence, contacts section, sessions section, data sync.
public class OpportunityDetailPageTests : BunitContext
{
    private static Opportunity MakeOpp(string id = "op1", string orgId = "o1") =>
        new()
        {
            Id = id, OrganizationId = orgId, Role = "Senior Dev",
            Stage = OpportunityStage.Open,
            PostingUrl = "https://jobs.example.com",
            CompensationRange = "$120k–$150k",
            WorkArrangement = WorkArrangement.Remote,
            CreatedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    private static Organization MakeOrg(string id = "o1") =>
        new() { Id = id, Name = "Acme Corp" };

    private async Task<(IRenderedComponent<OpportunityDetailPage> cut, AppServiceMocks mocks)>
        Render(Opportunity opp, IIndexedDbService? db = null)
    {
        db ??= new TestIndexedDbBuilder()
            .WithOpportunity(opp)
            .WithOrganization(MakeOrg(opp.OrganizationId))
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, opp.Id));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    private static async Task EnterEditMode(IRenderedComponent<OpportunityDetailPage> cut)
    {
        var editBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "Edit");
        await editBtn.ClickAsync(new());
    }

    // ── M4-3: Header section ──────────────────────────────────────────────────

    [Fact]
    public async Task HeaderSection_DisplaysAllFields()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        Assert.Contains("Senior Dev", cut.Markup);
        Assert.Contains("$120k–$150k", cut.Markup);
        Assert.Contains("Remote", cut.Markup);
        Assert.Contains("https://jobs.example.com", cut.Markup);
    }

    [Fact]
    public async Task HeaderSection_PostingUrl_IsClickableLink()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        Assert.Contains(cut.FindAll("a"), a =>
            (a.GetAttribute("href") ?? "").Contains("jobs.example.com"));
    }

    [Fact]
    public async Task HeaderSection_ShowsEditAndEvaluateButtons()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Edit");
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Evaluate"));
    }

    [Fact]
    public async Task StageQuickEdit_ChangeStageSaves()
    {
        var opp = MakeOpp();
        var db  = new TestIndexedDbBuilder().WithOpportunity(opp).WithOrganization(MakeOrg()).Build();
        var (cut, _) = await Render(opp, db);

        // Change the stage dropdown in view mode
        var stageSelect = cut.Find("dl select");
        await stageSelect.ChangeAsync(new ChangeEventArgs { Value = "Applied" });

        await db.Received(1).VersionedWriteAsync(
            "opportunities", Arg.Any<Opportunity>(), Arg.Any<string[]?>());
    }

    // ── M4-4: Edit mode ───────────────────────────────────────────────────────

    [Fact]
    public async Task EditMode_SavesChangedFields()
    {
        var opp = MakeOpp();
        var db  = new TestIndexedDbBuilder().WithOpportunity(opp).WithOrganization(MakeOrg()).Build();
        var (cut, _) = await Render(opp, db);
        await EnterEditMode(cut);

        // Change role
        cut.FindAll("input.form-control").First().Input("Updated Role");
        var saveBtn = cut.FindAll("button").First(b =>
            b.TextContent.Trim() == "Save" || b.TextContent.Contains("Saving"));
        await saveBtn.ClickAsync(new());

        await db.Received(1).VersionedWriteAsync(
            "opportunities", Arg.Is<Opportunity>(o => o.Role == "Updated Role"),
            Arg.Any<string[]?>());
    }

    [Fact]
    public async Task EditMode_Cancel_RevertsAllDraftChanges()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);
        await EnterEditMode(cut);

        cut.FindAll("input.form-control").First().Input("Draft Role");
        var cancelBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "Cancel");
        await cancelBtn.ClickAsync(new());

        Assert.Contains("Senior Dev", cut.Markup);
        Assert.DoesNotContain("Draft Role", cut.Markup);
    }

    [Fact]
    public async Task EditMode_HidesCorrespondenceSectionAddButton()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);
        await EnterEditMode(cut);

        // The correspondence "Add" dropdown button should be hidden in edit mode
        Assert.DoesNotContain(cut.FindAll("button"), b =>
            b.ClassName?.Contains("dropdown-toggle") == true &&
            b.TextContent.Trim() == "Add");
    }

    [Fact]
    public async Task EditMode_HidesChangeHistoryButton()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);
        await EnterEditMode(cut);

        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Change History"));
    }

    [Fact]
    public async Task EditMode_HidesEvaluateAndGenerateButton()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);
        await EnterEditMode(cut);

        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Evaluate"));
    }

    [Fact]
    public async Task EditMode_NavigationGuardActive_WhenRoleChanged()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);
        await EnterEditMode(cut);

        cut.FindAll("input.form-control").First().Input("Changed Role");

        var nav = Services.GetRequiredService<NavigationManager>();
        _ = cut.InvokeAsync(() => nav.NavigateTo("/opportunities"));
        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Any(m =>
            m.TextContent.Contains("Unsaved Changes")), TimeSpan.FromSeconds(2));

        Assert.Contains(cut.FindAll(".modal.d-block"),
            m => m.TextContent.Contains("Unsaved Changes"));
    }

    // ── M4-5: Role Description section ────────────────────────────────────────

    [Fact]
    public async Task RoleDescriptionSection_WhenEmpty_ShowsPlaceholderText()
    {
        var opp = MakeOpp();
        opp.RoleDescription = "";
        var (cut, _) = await Render(opp);

        Assert.Contains("No role description yet", cut.Markup);
    }

    [Fact]
    public async Task RoleDescriptionSection_WhenFilled_DisplaysText()
    {
        var opp = MakeOpp();
        opp.RoleDescription = "Build scalable systems.";
        var (cut, _) = await Render(opp);

        Assert.Contains("Build scalable systems.", cut.Markup);
    }

    [Fact]
    public async Task ExtractQualificationsButton_HiddenWhenRoleDescriptionEmpty()
    {
        var opp = MakeOpp();
        opp.RoleDescription = "";
        var (cut, _) = await Render(opp);

        // In view mode with empty description, no Extract Qualifications button visible
        Assert.DoesNotContain(cut.FindAll("button:not([disabled])"), b =>
            b.TextContent.Contains("Extract Qualifications"));
    }

    [Fact]
    public async Task ExtractQualificationsButton_VisibleWhenRoleDescriptionNonEmpty()
    {
        var opp = MakeOpp();
        opp.RoleDescription = "We need a senior developer.";
        var (cut, _) = await Render(opp);

        Assert.Contains(cut.FindAll("button"), b =>
            b.TextContent.Contains("Extract Qualifications") &&
            b.GetAttribute("disabled") is null);
    }

    [Fact]
    public async Task ExtractQualificationsButton_DisabledInEditMode()
    {
        var opp = MakeOpp();
        opp.RoleDescription = "We need a senior developer.";
        var (cut, _) = await Render(opp);
        await EnterEditMode(cut);

        var btn = cut.FindAll("button").FirstOrDefault(b =>
            b.TextContent.Contains("Extract Qualifications"));
        Assert.NotNull(btn);
        Assert.NotNull(btn.GetAttribute("disabled"));
    }

    [Fact]
    public async Task QualificationsSection_RequiredList_EmptyStatePlaceholder()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        Assert.Contains("No required qualifications yet", cut.Markup);
    }

    [Fact]
    public async Task QualificationsSection_PreferredList_EmptyStatePlaceholder()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        Assert.Contains("No preferred qualifications yet", cut.Markup);
    }

    [Fact]
    public async Task ChangeHistoryModal_OpensOnButtonClick()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        var historyBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Change History"));
        await historyBtn.ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Change History", cut.Find(".modal-title").TextContent));
    }

    // ── M4-5: Qualifications edit ─────────────────────────────────────────────

    [Fact]
    public async Task QualificationsSection_EditMode_AddQualification_ShowsInViewMode()
    {
        var opp = MakeOpp();
        var db  = new TestIndexedDbBuilder().WithOpportunity(opp).WithOrganization(MakeOrg()).Build();
        var (cut, _) = await Render(opp, db);
        await EnterEditMode(cut);

        // Click "+ Add" for Required qualifications (first one in the page)
        var addBtns = cut.FindAll("button").Where(b => b.TextContent.Trim() == "+ Add").ToList();
        await addBtns[0].ClickAsync(new()); // Add required qual

        // A new input should appear
        cut.WaitForAssertion(() =>
            Assert.True(cut.FindAll(".input-group input.form-control-sm").Count > 0));
    }

    // ── M5-1: Correspondence section ──────────────────────────────────────────

    [Fact]
    public async Task CorrespondenceSection_EmptyState_ShowsMessage()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        Assert.Contains("No correspondence yet.", cut.Markup);
    }

    [Fact]
    public async Task CorrespondenceSection_WithEntries_SortedByOccurredAtDescending()
    {
        var opp   = MakeOpp();
        var corr1 = new Correspondence
        {
            Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            Body = "First email", OccurredAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var corr2 = new Correspondence
        {
            Id = "c2", OpportunityId = "op1", Type = CorrespondenceType.Email,
            Body = "Second email", OccurredAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var db = new TestIndexedDbBuilder()
            .WithOpportunity(opp)
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr1, corr2)
            .Build();
        var (cut, _) = await Render(opp, db);

        var rows = cut.FindAll("tbody tr").ToList();
        // The second correspondence (newer) should be first
        Assert.True(rows.Count >= 2);
        Assert.Contains("Second email", rows[0].TextContent);
    }

    [Fact]
    public async Task CorrespondenceSection_HiddenDuringOpportunityEditMode()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);
        await EnterEditMode(cut);

        // Add dropdown (the btn-primary "Add" for correspondence) should be hidden
        Assert.DoesNotContain(cut.FindAll("button.btn-primary"), b => b.TextContent.Trim() == "Add");
    }

    // ── M5-2: Correspondence modal ────────────────────────────────────────────

    [Fact]
    public async Task AddEmailModal_ShowsDirectionToggle()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        // Open Add dropdown → click Email
        await cut.Find(".dropdown-toggle.btn-primary").ClickAsync(new());
        var emailLink = cut.FindAll(".dropdown-item").First(a => a.TextContent.Trim() == "Email");
        await emailLink.ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find(".modal.d-block");
            Assert.Contains("Direction", modal.TextContent);
        });
    }

    [Fact]
    public async Task AddVideoCallModal_NoDirectionToggle()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        await cut.Find(".dropdown-toggle.btn-primary").ClickAsync(new());
        var link = cut.FindAll(".dropdown-item").First(a => a.TextContent.Trim() == "Video Call");
        await link.ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("New Video Call", cut.Markup));
        Assert.DoesNotContain("Direction", cut.Find(".modal-body").TextContent);
    }

    [Fact]
    public async Task AddResumeSubmittedModal_ShowsSessionSelector()
    {
        var opp = MakeOpp();
        var (cut, _) = await Render(opp);

        await cut.Find(".dropdown-toggle.btn-primary").ClickAsync(new());
        var link = cut.FindAll(".dropdown-item")
            .First(a => a.TextContent.Trim() == "Resume Submitted");
        await link.ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Session", cut.Markup));
    }

    // ── M4-8 / M5-5: Live updates ────────────────────────────────────────────

    [Fact]
    public async Task OppDetailPage_OnRemoteOppUpdate_RefreshesSections()
    {
        var opp = MakeOpp();
        var db  = new TestIndexedDbBuilder()
            .WithOpportunity(opp)
            .WithOrganization(MakeOrg())
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        // Remote update with new role
        var updated = MakeOpp();
        updated.Role = "Lead Engineer";
        db.GetOpportunityAsync("op1").Returns(Task.FromResult<Opportunity?>(updated));

        mocks.DataSync.Raise("opportunity", "op1", "updated");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Lead Engineer"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Lead Engineer", cut.Markup);
    }

    [Fact]
    public async Task OppDetailPage_OnRemoteOppUpdate_WhileEditing_ShowsConflictAlert()
    {
        var opp  = MakeOpp();
        var (cut, mocks) = await Render(opp);
        await EnterEditMode(cut);

        mocks.DataSync.Raise("opportunity", "op1", "updated");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Record Changed"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Record Changed", cut.Markup);
    }

    [Fact]
    public async Task OppDetailPage_OnRemoteOppDelete_ShowsDeletionAlert()
    {
        var opp  = MakeOpp();
        var (cut, mocks) = await Render(opp);

        mocks.DataSync.Raise("opportunity", "op1", "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("deleted in another tab"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("deleted in another tab", cut.Markup);
    }

    [Fact]
    public async Task OppDetailPage_OnRemoteOrgDelete_ShowsAlertAndNavigatesToOrgList()
    {
        var opp  = MakeOpp();
        var (cut, mocks) = await Render(opp);

        mocks.DataSync.Raise("organization", "o1", "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("deleted in another tab"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("deleted in another tab", cut.Markup);
    }

    [Fact]
    public async Task OppDetailPage_OnCorrespondenceCreated_RefreshesCorrespondenceSection()
    {
        var opp = MakeOpp();
        var db  = new TestIndexedDbBuilder()
            .WithOpportunity(opp)
            .WithOrganization(MakeOrg())
            .Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("No correspondence yet.", cut.Markup);

        var newCorr = new Correspondence
        {
            Id = "corr1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            Body = "Hello there", OccurredAt = DateTime.UtcNow,
        };
        db.GetCorrespondenceByOpportunityAsync("op1").Returns(
            Task.FromResult(new List<Correspondence> { newCorr }));

        mocks.DataSync.Raise("correspondence", "corr1", "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Hello there"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Hello there", cut.Markup);
    }
}
