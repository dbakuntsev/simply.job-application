namespace Simply.JobApplication.Tests.M1;

// M1-6: IndexedDbService CRUD — null-guard and JS function routing.
public class IndexedDbCrudTests
{
    private static (IndexedDbService svc, IJSObjectReference module) MakeService()
    {
        var js     = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));
        // Default: return null string for all JSON-returning calls
        module.InvokeAsync<string?>(Arg.Any<string>(), Arg.Any<object[]?>())
              .Returns(new ValueTask<string?>((string?)null));
        return (new IndexedDbService(js), module);
    }

    // ── Organizations ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllOrganizationsAsync_WhenJsReturnsNull_ReturnsEmptyList()
    {
        var (svc, _) = MakeService();
        var result = await svc.GetAllOrganizationsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllOrganizationsAsync_WhenJsReturnsJson_DeserializesCorrectly()
    {
        var (svc, module) = MakeService();
        var json = "[{\"id\":\"org-1\",\"name\":\"ACME\",\"version\":1,\"industry\":\"\",\"size\":\"\",\"website\":\"\",\"linkedIn\":\"\",\"description\":\"\",\"createdAt\":\"2024-01-01T00:00:00Z\",\"updatedAt\":\"2024-01-01T00:00:00Z\"}]";
        module.InvokeAsync<string?>("getAllOrganizations", Arg.Any<object[]?>())
              .Returns(new ValueTask<string?>(json));

        var result = await svc.GetAllOrganizationsAsync();

        Assert.Single(result);
        Assert.Equal("ACME", result[0].Name);
    }

    [Fact]
    public async Task GetOpportunitiesByOrganizationAsync_PassesOrgIdToJs()
    {
        var (svc, module) = MakeService();
        await svc.GetOpportunitiesByOrganizationAsync("org-42");

        await module.Received(1).InvokeAsync<string?>(
            "getOpportunitiesByOrganization",
            Arg.Is<object[]?>(a => a != null && a.Length > 0 && a[0].ToString() == "org-42"));
    }

    // ── Correspondence ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCorrespondenceByOpportunityAsync_WhenJsReturnsNull_ReturnsEmptyList()
    {
        var (svc, _) = MakeService();
        var result = await svc.GetCorrespondenceByOpportunityAsync("opp-1");
        Assert.Empty(result);
    }

    // ── Organizations: save uses camelCase JSON ───────────────────────────────

    [Fact]
    public async Task SaveOrganizationAsync_SerializesRecordWithCamelCaseJson()
    {
        var (svc, module) = MakeService();
        var org = new Organization { Name = "Test Corp" };

        await svc.SaveOrganizationAsync(org);

        await module.Received(1).InvokeVoidAsync("saveOrganization",
            Arg.Is<object[]?>(args => args != null && args.Length > 0 &&
                args[0].ToString()!.Contains("\"name\"")));
    }

    // ── BaseResumes ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBaseResumesAsync_WhenJsReturnsJson_DeserializesCorrectly()
    {
        var (svc, module) = MakeService();
        var json = "[{\"id\":\"r-1\",\"name\":\"My CV\",\"version\":1,\"createdAt\":\"2024-01-01T00:00:00Z\",\"updatedAt\":\"2024-01-01T00:00:00Z\"}]";
        module.InvokeAsync<string?>("getAllBaseResumes", Arg.Any<object[]?>())
              .Returns(new ValueTask<string?>(json));

        var result = await svc.GetAllBaseResumesAsync();

        Assert.Single(result);
        Assert.Equal("My CV", result[0].Name);
    }

    [Fact]
    public async Task GetVersionsByResumeAsync_PassesResumeIdToJs()
    {
        var (svc, module) = MakeService();
        await svc.GetVersionsByResumeAsync("resume-99");

        await module.Received(1).InvokeAsync<string?>(
            "getVersionsByResume",
            Arg.Is<object[]?>(a => a != null && a.Length > 0 && a[0].ToString() == "resume-99"));
    }

    // ── ContactOpportunityRoles ───────────────────────────────────────────────

    [Fact]
    public async Task GetRolesByOpportunityAsync_WhenJsReturnsNull_ReturnsEmptyList()
    {
        var (svc, _) = MakeService();
        var result = await svc.GetRolesByOpportunityAsync("opp-1");
        Assert.Empty(result);
    }
}
