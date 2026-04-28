namespace Simply.JobApplication.Tests.M10;

// M10-4: SessionDisplayHelper — unit tests for display resolution of org, opp, and resume.
public class SessionDisplayHelperTests
{
    private static SessionRecord MakeSession(
        string? orgId = null, string orgSnap = "",
        string? oppId = null, string oppSnap = "",
        string? resumeName = null, int resumeVersion = 1) => new()
    {
        OrganizationId           = orgId,
        OrganizationNameSnapshot = orgSnap,
        OpportunityId            = oppId,
        OpportunityRoleSnapshot  = oppSnap,
        BaseResumeNameSnapshot   = resumeName ?? "",
        BaseResumeVersionNumberSnapshot = resumeVersion,
    };

    // ── Org resolution ────────────────────────────────────────────────────────

    [Fact]
    public void SessionDisplayHelper_OrgExists_NameUnchanged_ReturnsCurrentName()
    {
        var session = MakeSession(orgId: "o1", orgSnap: "Acme Corp");
        var orgMap  = new Dictionary<string, OrganizationProjection>
        {
            ["o1"] = new() { Id = "o1", Name = "Acme Corp" }
        };

        var result = SessionDisplayHelper.ResolveOrg(session, orgMap);

        Assert.Equal("Acme Corp", result.Text);
        Assert.Contains("/organizations/o1", result.Url);
    }

    [Fact]
    public void SessionDisplayHelper_OrgExists_NameChanged_ReturnsFormerlyDisplay()
    {
        var session = MakeSession(orgId: "o1", orgSnap: "Old Name Inc");
        var orgMap  = new Dictionary<string, OrganizationProjection>
        {
            ["o1"] = new() { Id = "o1", Name = "New Name LLC" }
        };

        var result = SessionDisplayHelper.ResolveOrg(session, orgMap);

        Assert.Contains("New Name LLC", result.Text);
        Assert.Contains("formerly", result.Text);
        Assert.Contains("Old Name Inc", result.Text);
        Assert.NotNull(result.Url);
    }

    [Fact]
    public void SessionDisplayHelper_OrgDeleted_ReturnsDanglingDisplay()
    {
        var session = MakeSession(orgId: "o1", orgSnap: "Deleted Corp");
        var orgMap  = new Dictionary<string, OrganizationProjection>(); // empty — org not found

        var result = SessionDisplayHelper.ResolveOrg(session, orgMap);

        Assert.Contains("deleted", result.Text);
        Assert.Contains("Deleted Corp", result.Text);
        Assert.Null(result.Url);
    }

    [Fact]
    public void SessionDisplayHelper_OrgIdNull_ReturnsSnapshotAsPlainText()
    {
        var session = MakeSession(orgId: null, orgSnap: "Ad-hoc Work");
        var orgMap  = new Dictionary<string, OrganizationProjection>();

        var result = SessionDisplayHelper.ResolveOrg(session, orgMap);

        Assert.Equal("Ad-hoc Work", result.Text);
        Assert.Null(result.Url);
    }

    // ── Opp resolution ────────────────────────────────────────────────────────

    [Fact]
    public void SessionDisplayHelper_OppExists_NameUnchanged_ReturnsCurrentRole()
    {
        var session = MakeSession(oppId: "op1", oppSnap: "Software Engineer");
        var oppMap  = new Dictionary<string, OpportunityProjection>
        {
            ["op1"] = new() { Id = "op1", Role = "Software Engineer" }
        };

        var result = SessionDisplayHelper.ResolveOpp(session, oppMap);

        Assert.Equal("Software Engineer", result.Text);
        Assert.Contains("/opportunities/op1", result.Url);
    }

    [Fact]
    public void SessionDisplayHelper_OppIdNull_ReturnsEmptyText()
    {
        var session = MakeSession(oppId: null);
        var oppMap  = new Dictionary<string, OpportunityProjection>();

        var result = SessionDisplayHelper.ResolveOpp(session, oppMap);

        Assert.Equal("", result.Text);
        Assert.Null(result.Url);
    }

    // ── Resume resolution ─────────────────────────────────────────────────────

    [Fact]
    public void SessionDisplayHelper_ResumeExists_NameUnchanged_ReturnsCurrentNameWithVersion()
    {
        var session = MakeSession(resumeName: "My Resume", resumeVersion: 2);

        var result = SessionDisplayHelper.ResolveResumeName(session, "My Resume");

        Assert.Contains("My Resume", result);
        Assert.Contains("(v2)", result);
    }

    [Fact]
    public void SessionDisplayHelper_ResumeExists_NameChanged_ReturnsFormerlyDisplayWithVersion()
    {
        var session = MakeSession(resumeName: "Old Resume", resumeVersion: 1);

        var result = SessionDisplayHelper.ResolveResumeName(session, "Renamed Resume");

        Assert.Contains("Renamed Resume", result);
        Assert.Contains("formerly", result);
        Assert.Contains("Old Resume", result);
        Assert.Contains("(v1)", result);
    }

    [Fact]
    public void SessionDisplayHelper_ResumeDeleted_ReturnsDanglingDisplayWithVersion()
    {
        var session = MakeSession(resumeName: "Deleted Resume", resumeVersion: 3);

        // null currentResumeName signals the resume was deleted
        var result = SessionDisplayHelper.ResolveResumeName(session, null);

        Assert.Contains("deleted", result);
        Assert.Contains("Deleted Resume", result);
        Assert.Contains("(v3)", result);
    }
}
