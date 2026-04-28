namespace Simply.JobApplication.Tests.M7;

// M7-3, M7-4: HistoryDetailPage — session detail display and live updates.
public class HistoryDetailPageTests : BunitContext
{
    private async Task<(IRenderedComponent<HistoryDetailPage> cut, AppServiceMocks mocks)>
        RenderSession(SessionRecord session, IIndexedDbService? db = null)
    {
        if (db is null)
        {
            db = new TestIndexedDbBuilder().WithSessions(session).Build();
        }
        var mocks = this.AddAppServices(db);
        var cut   = Render<HistoryDetailPage>(p => p.Add(x => x.Id, session.Id));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    // ── M7-3: History Detail display ─────────────────────────────────────────

    [Fact]
    public async Task HistoryDetail_ShowsOrganizationLink_WhenOrgLinked()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OrganizationNameSnapshot = "Acme Corp",
            Role = "Dev", BaseResumeVersionId = "", BaseResumeNameSnapshot = "My Resume"
        };
        var db = new TestIndexedDbBuilder()
            .WithSessions(session)
            .WithOrganizationProjections(new OrganizationProjection { Id = "o1", Name = "Acme Corp" })
            .Build();

        var (cut, _) = await RenderSession(session, db);

        Assert.NotEmpty(cut.FindAll("a").Where(a =>
            (a.GetAttribute("href") ?? "").Contains("organizations/o1") &&
            a.TextContent.Contains("Acme Corp")));
    }

    [Fact]
    public async Task HistoryDetail_BaseResume_DisplaysNameAndVersionNumber()
    {
        var version = new BaseResumeVersion
        {
            Id = "v1", ResumeId = "r1", VersionNumber = 3,
            FileDataBase64 = Convert.ToBase64String("dummy"u8.ToArray())
        };
        var resume = new BaseResume { Id = "r1", Name = "My Resume" };
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            BaseResumeVersionId = "v1", BaseResumeNameSnapshot = "My Resume",
            BaseResumeVersionNumberSnapshot = 3
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        db.GetBaseResumeVersionAsync("v1").Returns(Task.FromResult<BaseResumeVersion?>(version));
        db.GetBaseResumeAsync("r1").Returns(Task.FromResult<BaseResume?>(resume));

        var (cut, _) = await RenderSession(session, db);

        // SessionDisplayHelper.ResolveResumeName returns "My Resume (v3)"
        Assert.Contains("My Resume", cut.Markup);
        Assert.Contains("(v3)", cut.Markup);
    }

    [Fact]
    public async Task HistoryDetail_WhyApply_RenderedInCard()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            WhyApplyText = "Great opportunity for growth and challenge.",
            ArtifactsGenerated = true
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderSession(session, db);

        Assert.Contains("Great opportunity for growth", cut.Markup);
        Assert.Contains("Why Apply", cut.Markup);
    }

    [Fact]
    public async Task HistoryDetail_CoverLetter_RenderedInCard()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            CoverLetterText = "Dear Hiring Manager, I am thrilled to apply.",
            ArtifactsGenerated = true
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderSession(session, db);

        Assert.Contains("Dear Hiring Manager", cut.Markup);
        Assert.Contains("Cover Letter", cut.Markup);
    }

    [Fact]
    public async Task HistoryDetail_DeleteButton_VisibleForAdHocSessions()
    {
        var adHoc = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X", Role = "Dev"
        };
        var db = new TestIndexedDbBuilder().WithSessions(adHoc).Build();

        var (cut, _) = await RenderSession(adHoc, db);

        Assert.NotEmpty(cut.FindAll("button.btn-outline-danger"));
    }

    [Fact]
    public async Task HistoryDetail_DeleteButton_HiddenForOrgLinkedSessions()
    {
        var orgLinked = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OrganizationNameSnapshot = "Acme", Role = "Dev"
        };
        var db = new TestIndexedDbBuilder().WithSessions(orgLinked).Build();

        var (cut, _) = await RenderSession(orgLinked, db);

        Assert.Empty(cut.FindAll("button.btn-outline-danger"));
    }

    // ── M7-4: Live updates for History Detail ────────────────────────────────

    [Fact]
    public async Task HistoryDetail_OnRemoteSessionDelete_ShowsDeletionAlert()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X", Role = "Dev"
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderSession(session, db);

        mocks.DataSync.Raise("session", "s1", "deleted");

        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Any(), TimeSpan.FromSeconds(2));
        Assert.NotEmpty(cut.FindAll(".modal.d-block"));
    }

    [Fact]
    public async Task HistoryDetail_OnRemoteSessionDelete_DismissNavigatesToHistoryList()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X", Role = "Dev"
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderSession(session, db);
        var nav = Services.GetRequiredService<NavigationManager>();

        mocks.DataSync.Raise("session", "s1", "deleted");
        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Any(), TimeSpan.FromSeconds(2));

        await cut.Find(".modal.d-block .btn-primary").ClickAsync(new());

        Assert.Contains("history", nav.Uri);
    }

    [Fact]
    public async Task HistoryDetail_OnOrgDeleted_WhenSessionBelongsToOrg_ShowsDeletionAlert()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OrganizationNameSnapshot = "Acme", Role = "Dev"
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderSession(session, db);

        mocks.DataSync.Raise("organization", "o1", "deleted");

        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Any(), TimeSpan.FromSeconds(2));
        Assert.NotEmpty(cut.FindAll(".modal.d-block"));
        Assert.Contains("Acme", cut.Find(".modal.d-block .modal-body").TextContent);
    }
}
