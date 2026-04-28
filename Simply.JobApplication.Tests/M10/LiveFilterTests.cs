namespace Simply.JobApplication.Tests.M10;

// M10-3: Cross-cutting live text filter with match highlighting.
// Organizations and Opportunities list filter tests are in M3/M4 respectively.
// This file covers the History list filter and cross-cutting invariants.
public class LiveFilterTests : BunitContext
{
    private async Task<IRenderedComponent<HistoryPage>> RenderHistory(params SessionRecord[] sessions)
    {
        var db = new TestIndexedDbBuilder().WithSessions(sessions).Build();
        this.AddAppServices(db);
        var cut = Render<HistoryPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return cut;
    }

    [Fact]
    public async Task LiveFilter_HistoryList_ShowsOnlyMatchingRows()
    {
        var s1 = new SessionRecord
        {
            Id = "s1", Role = "Alpha Role", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var s2 = new SessionRecord
        {
            Id = "s2", Role = "Beta Role", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var cut = await RenderHistory(s1, s2);

        cut.Find("input[type='search']").Input("Alpha");

        // HighlightMatch wraps matches in <mark> tags — use row TextContent to find role name.
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            Assert.Single(rows);
            Assert.Contains("Alpha Role", rows[0].TextContent);
        });
    }

    [Fact]
    public async Task LiveFilter_CaseInsensitive()
    {
        var session = new SessionRecord
        {
            Id = "s1", Role = "Frontend Developer", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = DateTime.UtcNow
        };
        var cut = await RenderHistory(session);

        cut.Find("input[type='search']").Input("frontend");

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            Assert.Single(rows);
            Assert.Contains("Frontend Developer", rows[0].TextContent);
        });
    }

    [Fact]
    public async Task LiveFilter_ClearFilter_ReturnsAllRows()
    {
        var s1 = new SessionRecord
        {
            Id = "s1", Role = "Alpha Role", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var s2 = new SessionRecord
        {
            Id = "s2", Role = "Beta Role", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var cut = await RenderHistory(s1, s2);

        var input = cut.Find("input[type='search']");
        input.Input("Alpha");
        cut.WaitForAssertion(() => Assert.DoesNotContain("Beta Role", cut.Markup));

        // Clear the filter
        input.Input("");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Alpha Role", cut.Markup);
            Assert.Contains("Beta Role", cut.Markup);
        });
    }

    [Fact]
    public async Task LiveFilter_HistoryList_HighlightsMatchedText()
    {
        var session = new SessionRecord
        {
            Id = "s1", Role = "Golang Developer", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = DateTime.UtcNow
        };
        var cut = await RenderHistory(session);

        cut.Find("input[type='search']").Input("Golang");

        cut.WaitForAssertion(() => Assert.Contains("<mark>Golang</mark>", cut.Markup));
    }
}
