namespace Simply.JobApplication.Tests.Resumes;

// M2-7: Restore and Compare version actions on MyResumesPage.
public class VersionActionsTests : BunitContext
{
    public VersionActionsTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private async Task<(IRenderedComponent<MyResumesPage> cut, AppServiceMocks mocks)>
        RenderExpanded(IIndexedDbService db)
    {
        var mocks = this.AddAppServices(db);
        var cut   = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        // Expand the first row to reveal version details
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Earlier Versions", cut.Markup));
        return (cut, mocks);
    }

    // ── Restore modal ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RestoreModal_ShowsAfterClickingRestore()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2,
                     UploadedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc) };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1,
                     UploadedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)
            .Build();
        var (cut, _) = await RenderExpanded(db);

        await cut.Find("button[title='Restore version 1']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Restore Version", cut.Markup));
    }

    [Fact]
    public async Task RestoreModal_NotesField_PrePopulatedWithDate()
    {
        var uploadedAt = new DateTime(2024, 1, 1, 9, 30, 0, DateTimeKind.Utc);
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2,
                     UploadedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc) };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1,
                     UploadedAt = uploadedAt };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)
            .Build();
        var (cut, _) = await RenderExpanded(db);

        await cut.Find("button[title='Restore version 1']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Restore Version", cut.Markup));

        var textarea = cut.Find("textarea");
        var notes    = textarea.GetAttribute("value") ?? textarea.TextContent.Trim();
        Assert.Contains("Restored from", notes);
    }

    [Fact]
    public async Task RestoreModal_Confirm_CallsSaveBaseResumeVersionAsync()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2,
                     UploadedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc),
                     FileDataBase64 = Convert.ToBase64String("latest"u8.ToArray()) };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1,
                     UploadedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                     FileDataBase64 = Convert.ToBase64String("original"u8.ToArray()) };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)
            .Build();
        var (cut, _) = await RenderExpanded(db);

        await cut.Find("button[title='Restore version 1']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Restore Version", cut.Markup));

        // Click the Restore confirm button — use modal-scoped selector to avoid
        // matching the page header "Upload Resume" btn-primary button
        await cut.Find(".modal .btn-primary").ClickAsync(new());

        await db.Received(1).SaveBaseResumeVersionAsync(
            Arg.Is<BaseResumeVersion>(v =>
                v.ResumeId == "r1" &&
                v.VersionNumber == 3 &&
                v.FileDataBase64 == v1.FileDataBase64));
    }

    [Fact]
    public async Task RestoreModal_Cancel_DoesNotCallSave()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2,
                     UploadedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc) };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1,
                     UploadedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)
            .Build();
        var (cut, _) = await RenderExpanded(db);

        await cut.Find("button[title='Restore version 1']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Restore Version", cut.Markup));

        await cut.Find(".btn-secondary").ClickAsync(new());

        await db.DidNotReceive().SaveBaseResumeVersionAsync(Arg.Any<BaseResumeVersion>());
    }

    // ── Compare modal ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CompareModal_OpensResumeDiffModal()
    {
        var v2Bytes = "latest content"u8.ToArray();
        var v1Bytes = "prior content"u8.ToArray();
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2,
                     UploadedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc),
                     FileDataBase64 = Convert.ToBase64String(v2Bytes) };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1,
                     UploadedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                     FileDataBase64 = Convert.ToBase64String(v1Bytes) };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)
            .Build();
        var (cut, mocks) = await RenderExpanded(db);
        mocks.Docx.ExtractTextAsMarkdown(Arg.Any<byte[]>()).Returns("markdown text");

        await cut.Find("button[title='Compare to latest']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".modal")));
    }

    [Fact]
    public async Task CompareModal_ExtractsMarkdownFromBothVersions()
    {
        var v2Bytes = "latest content"u8.ToArray();
        var v1Bytes = "prior content"u8.ToArray();
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v2 = new BaseResumeVersion { Id = "v2", ResumeId = "r1", VersionNumber = 2,
                     UploadedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc),
                     FileDataBase64 = Convert.ToBase64String(v2Bytes) };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1,
                     UploadedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                     FileDataBase64 = Convert.ToBase64String(v1Bytes) };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v2, v1)
            .Build();
        var (cut, mocks) = await RenderExpanded(db);
        mocks.Docx.ExtractTextAsMarkdown(Arg.Any<byte[]>()).Returns("text");

        await cut.Find("button[title='Compare to latest']").ClickAsync(new());

        // ExtractTextAsMarkdown should be called twice: once for prior, once for latest
        mocks.Docx.Received(2).ExtractTextAsMarkdown(Arg.Any<byte[]>());
    }
}
