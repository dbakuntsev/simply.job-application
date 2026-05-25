using AngleSharp.Dom;
using Bunit.TestDoubles;

namespace Simply.JobApplication.Tests.Opportunities;

// Tests for Pages/WeeklyReportPage.razor — resume-submission weekly report,
// week navigation, URL state, cross-tab sync, and copy-to-clipboard buttons.
public class WeeklyReportPageTests : BunitContext
{
    private async Task<(IRenderedComponent<WeeklyReportPage> cut, AppServiceMocks mocks)>
        RenderPage(IIndexedDbService? db = null)
    {
        var mocks = this.AddAppServices(db);
        var cut   = Render<WeeklyReportPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    private static DateTime WeekStartSunday(DateTime localDate)
        => localDate.Date.AddDays(-(int)localDate.DayOfWeek);

    private static Correspondence Submission(string id, string oppId, DateTime occurredAt) =>
        new()
        {
            Id           = id,
            OpportunityId = oppId,
            Type         = CorrespondenceType.ResumeSubmitted,
            OccurredAt   = occurredAt,
        };

    private static IElement FindButton(IRenderedComponent<WeeklyReportPage> cut, string text)
        => cut.FindAll("button").First(b => b.TextContent.Contains(text));

    // ── #1: empty state ─────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyState_WhenNoSubmissions_ShowsMessage()
    {
        var (cut, _) = await RenderPage();
        Assert.Contains("No resume submissions for this week", cut.Markup);
    }

    // ── #3: out-of-week filtering ───────────────────────────────────────────

    [Fact]
    public async Task SubmissionInPreviousWeek_DoesNotAppear()
    {
        // 10 days ago is always in a previous week regardless of today.
        var sub = Submission("c1", "op1", DateTime.Now.AddDays(-10));
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Old Role" };
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub).WithOpportunities(opp).WithOrganizations(org).Build();

        var (cut, _) = await RenderPage(db);

        Assert.DoesNotContain("Old Role", cut.Markup);
        Assert.Contains("No resume submissions for this week", cut.Markup);
    }

    // ── #6: same opportunity in two weeks → one row per week ───────────────

    [Fact]
    public async Task SameOpportunityWithTwoSubmissions_AppearsInBothWeeks()
    {
        var thisWeek = DateTime.Now.AddHours(-1);
        var lastWeek = DateTime.Now.AddDays(-8); // always in W-1 for Sun-start

        var sub1 = Submission("c1", "op1", thisWeek);
        var sub2 = Submission("c2", "op1", lastWeek);
        var opp  = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Same Role" };
        var org  = new Organization { Id = "o1", Name = "Acme" };

        var db = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub1, sub2).WithOpportunities(opp).WithOrganizations(org).Build();
        var (cut, _) = await RenderPage(db);

        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Same Role", rows[0].TextContent);

        await FindButton(cut, "Prev").ClickAsync(new());

        rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Same Role", rows[0].TextContent);
    }

    // ── #7: locale-driven first-day-of-week (Monday-start) ─────────────────

    [Fact]
    public async Task WeekBoundary_RespectsLocaleFirstDayOfWeek_Monday()
    {
        JSInterop.Setup<int>("sjaGetFirstDayOfWeek").SetResult(1); // Monday

        // Compute "last Sunday" relative to today; under Monday-start that
        // Sunday is the *end* of the previous week, not part of current week.
        var today                = DateTime.Today;
        var daysSinceMonday      = ((int)today.DayOfWeek + 6) % 7;
        var thisWeekMondayStart  = today.AddDays(-daysSinceMonday);
        var lastSundayLocal      = thisWeekMondayStart.AddDays(-1);

        var sub = Submission("c1", "op1", lastSundayLocal.AddHours(15));
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Sun Role" };
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub).WithOpportunities(opp).WithOrganizations(org).Build();
        var (cut, _) = await RenderPage(db);

        // Initial render = current Monday-Sunday week → Sunday submission is hidden.
        Assert.DoesNotContain("Sun Role", cut.Markup);

        await FindButton(cut, "Prev").ClickAsync(new());

        Assert.Contains("Sun Role", cut.Markup);
    }

    // ── #11: empty Web Site cell when no website is set ─────────────────────

    [Fact]
    public async Task WebSiteCell_EmptyWhenWebsiteMissing()
    {
        var sub = Submission("c1", "op1", DateTime.Now.AddHours(-1));
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Role" };
        var org = new Organization { Id = "o1", Name = "Acme" /* Website = "" */ };
        var db  = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub).WithOpportunities(opp).WithOrganizations(org).Build();
        var (cut, _) = await RenderPage(db);

        var row   = cut.Find("tbody tr");
        var cells = row.QuerySelectorAll("td");
        // Cells: [submitted, role, org, website, stage]
        Assert.Empty(cells[3].QuerySelectorAll("a"));
        Assert.True(string.IsNullOrWhiteSpace(cells[3].TextContent));
    }

    // ── #13: Prev disabled when no older submissions exist ──────────────────

    [Fact]
    public async Task PrevButton_DisabledWhenNoOlderSubmissions()
    {
        var (cut, _) = await RenderPage();
        Assert.True(FindButton(cut, "Prev").HasAttribute("disabled"));
    }

    // ── #15: Next disabled on current week ──────────────────────────────────

    [Fact]
    public async Task NextButton_DisabledOnCurrentWeek()
    {
        var (cut, _) = await RenderPage();
        Assert.True(FindButton(cut, "Next").HasAttribute("disabled"));
    }

    // ── #19: ?week=YYYY-MM-DD loads that week ───────────────────────────────

    [Fact]
    public async Task UrlWeekParameter_LoadsThatWeek()
    {
        // Pick a Sunday well in the past so the param won't be clamped to "now".
        var pastWeekStart        = WeekStartSunday(DateTime.Today.AddDays(-30));
        var submissionInThatWeek = pastWeekStart.AddDays(2).AddHours(10);

        var sub = Submission("c1", "op1", submissionInThatWeek);
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Past Role" };
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub).WithOpportunities(opp).WithOrganizations(org).Build();

        this.AddAppServices(db);
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo($"opportunities/weekly-report?week={pastWeekStart:yyyy-MM-dd}");

        var cut = Render<WeeklyReportPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        Assert.Contains("Past Role", cut.Markup);
    }

    // ── #23: Prev click updates URL with replace (no history pile-up) ──────

    [Fact]
    public async Task PrevClick_UpdatesUrlWithReplace()
    {
        var lastWeek = DateTime.Now.AddDays(-8);
        var sub = Submission("c1", "op1", lastWeek);
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Role" };
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub).WithOpportunities(opp).WithOrganizations(org).Build();
        var (cut, _) = await RenderPage(db);

        var nav    = Services.GetRequiredService<NavigationManager>();
        var fake   = (BunitNavigationManager)nav;
        var before = fake.History.Count;

        await FindButton(cut, "Prev").ClickAsync(new());

        Assert.True(fake.History.Count > before);
        var last = fake.History.Last();
        Assert.True(last.Options.ReplaceHistoryEntry,
            "Prev/Next must use NavigateTo(..., replace: true) so each click does not push a history entry.");
        Assert.Contains("week=", last.Uri);
    }

    // ── #24: cross-tab refresh when correspondence changes ─────────────────

    [Fact]
    public async Task OnCorrespondenceChanged_RefreshesView()
    {
        var db    = new TestIndexedDbBuilder().Build();
        var mocks = this.AddAppServices(db);
        var cut   = Render<WeeklyReportPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("No resume submissions for this week", cut.Markup);

        var sub = Submission("c1", "op1", DateTime.Now.AddHours(-1));
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Refreshed Role" };
        var org = new Organization { Id = "o1", Name = "Acme" };
        db.GetAllResumeSubmissionsAsync().Returns(Task.FromResult(new List<Correspondence> { sub }));
        db.GetAllOpportunitiesAsync().Returns(Task.FromResult(new List<Opportunity> { opp }));
        db.GetAllOrganizationsAsync().Returns(Task.FromResult(new List<Organization> { org }));

        mocks.DataSync.Raise("correspondence", "c1", "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Refreshed Role"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("Refreshed Role", cut.Markup);
    }

    // ── #28: copy button not rendered when website is missing ──────────────

    [Fact]
    public async Task CopyButton_NotRenderedWhenWebsiteMissing()
    {
        var sub = Submission("c1", "op1", DateTime.Now.AddHours(-1));
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "Role" };
        var org = new Organization { Id = "o1", Name = "Acme" /* Website = "" */ };
        var db  = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub).WithOpportunities(opp).WithOrganizations(org).Build();
        var (cut, _) = await RenderPage(db);

        // Row has 5 cells: only Role + Org carry copy buttons; Website cell is empty.
        var row = cut.Find("tbody tr");
        Assert.Equal(2, row.QuerySelectorAll(".sja-copy-btn").Length);
    }

    // ── #29: copy button click invokes navigator.clipboard.writeText ───────

    [Fact]
    public async Task CopyButton_OnClick_InvokesClipboardWriteText()
    {
        var sub = Submission("c1", "op1", DateTime.Now.AddHours(-1));
        var opp = new Opportunity { Id = "op1", OrganizationId = "o1", Role = "My Role" };
        var org = new Organization { Id = "o1", Name = "Acme" };
        var db  = new TestIndexedDbBuilder()
            .WithResumeSubmissions(sub).WithOpportunities(opp).WithOrganizations(org).Build();
        var (cut, _) = await RenderPage(db);

        // First .sja-copy-btn in the row is the Role copy button.
        var roleCopyBtn = cut.Find("tbody tr .sja-copy-btn");

        // The handler ends with `await Task.Delay(1200)` for the icon revert;
        // ClickAsync awaits the full handler, so this test takes ~1.2s.
        await roleCopyBtn.ClickAsync(new());

        Assert.Contains(JSInterop.Invocations, i =>
            i.Identifier == "navigator.clipboard.writeText" &&
            i.Arguments.Count > 0 &&
            (i.Arguments[0] as string) == "My Role");
    }
}
