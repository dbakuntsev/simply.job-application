namespace Simply.JobApplication.Tests.M2;

// M2-3: MyResumesPage list display tests.
public class MyResumesPageTests : BunitContext
{
    private AppServiceMocks Setup(IIndexedDbService? db = null)
    {
        var mocks = this.AddAppServices(db);
        return mocks;
    }

    [Fact]
    public async Task MyResumesPage_WhenNoResumes_ShowsEmptyStateMessage()
    {
        Setup();
        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        Assert.Contains("No resumes yet", cut.Markup);
    }

    [Fact]
    public async Task MyResumesPage_WhenNoResumes_ShowsUploadResumeButton()
    {
        Setup();
        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Upload Resume"));
    }

    [Fact]
    public async Task MyResumesPage_WithResumes_DisplaysSortedByNameAscending()
    {
        var r1 = new BaseResume { Id = "r1", Name = "Zebra CV" };
        var r2 = new BaseResume { Id = "r2", Name = "Alpha CV" };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1, r2)
            .Build();
        Setup(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        var rows = cut.FindAll("tbody tr").ToList();
        Assert.True(rows.Count >= 2);
        Assert.Contains("Alpha CV", rows[0].TextContent);
    }

    [Fact]
    public async Task MyResumesPage_WithResumes_ShowsLatestVersionUploadDate()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v1 = new BaseResumeVersion
        {
            Id           = "v1",
            ResumeId     = "r1",
            VersionNumber = 1,
            UploadedAt   = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v1)
            .Build();
        Setup(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        // Date is shown in the table row
        Assert.Contains("2024", cut.Markup);
    }

    [Fact]
    public async Task MyResumesPage_ExpandRow_ShowsLatestVersionDetails()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1, FileName = "my-resume.docx" };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v1)
            .Build();
        Setup(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        // Click the row to expand
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Latest Version", cut.Markup));
    }

    [Fact]
    public async Task MyResumesPage_ExpandRow_ListsPriorVersions()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2 };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1 };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)  // v2 is latest
            .Build();
        Setup(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Earlier Versions", cut.Markup));
    }

    [Fact]
    public async Task MyResumesPage_LatestVersionRow_DoesNotShowRestoreOrCompare()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1 };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v1)
            .Build();
        Setup(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Latest Version", cut.Markup));

        // Latest version section should NOT have Restore or Compare buttons
        var latestSection = cut.Find(".card-body, .bg-white.border.rounded");
        Assert.DoesNotContain("Restore", latestSection.TextContent);
        Assert.DoesNotContain("Compare", latestSection.TextContent);
    }

    [Fact]
    public async Task MyResumesPage_PriorVersionRow_ShowsRestoreAndCompare()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2 };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1 };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)
            .Build();
        Setup(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Earlier Versions", cut.Markup));

        Assert.Contains("Restore", cut.Markup);
        Assert.Contains("Compare", cut.Markup);
    }
}
