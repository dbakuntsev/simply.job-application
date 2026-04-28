namespace Simply.JobApplication.Tests.Resumes;

// M2-5: Upload New Version modal — notes default, confirm/cancel, previous version retained.
public class UploadNewVersionTests : BunitContext
{
    private static IBrowserFile MakeFile(string name, string content = "DOCXDATA")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var file  = Substitute.For<IBrowserFile>();
        file.Name.Returns(name);
        file.Size.Returns((long)bytes.Length);
        file.ContentType.Returns(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(bytes));
        return file;
    }

    private async Task<IRenderedComponent<MyResumesPage>> RenderWithResume(IIndexedDbService db)
    {
        this.AddAppServices(db);
        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return cut;
    }

    /// Opens the Upload New Version modal for the first resume in the list.
    private static async Task OpenVersionModal(IRenderedComponent<MyResumesPage> cut)
    {
        var btn = cut.Find("button[title='Upload New Version']");
        await btn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Upload New Version —", cut.Markup));
    }

    /// Selects a file via the second InputFile component (version upload input).
    private static async Task SelectVersionFile(IRenderedComponent<MyResumesPage> cut, string fileName)
    {
        var mockFile = MakeFile(fileName);
        var args     = new InputFileChangeEventArgs(new[] { mockFile });
        var inputs   = cut.FindComponents<InputFile>();
        // inputs[0] = upload-resume-input (always present), inputs[1] = upload-version-input
        await cut.InvokeAsync(() => inputs[1].Instance.OnChange.InvokeAsync(args));
    }

    [Fact]
    public async Task VersionUploadModal_ShowsWithResumeNameInTitle()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResume(db);

        await OpenVersionModal(cut);

        Assert.Contains("Upload New Version — My Resume", cut.Markup);
    }

    [Fact]
    public async Task VersionUploadModal_NotesField_IsEmptyByDefault()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResume(db);
        await OpenVersionModal(cut);

        var textarea = cut.Find("textarea");
        Assert.Equal("", textarea.TextContent.Trim());
    }

    [Fact]
    public async Task VersionUploadModal_UploadVersionButton_DisabledBeforeFileSelected()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResume(db);
        await OpenVersionModal(cut);

        var uploadBtn = cut.FindAll("button")
                          .FirstOrDefault(b => b.TextContent.Contains("Upload Version"));
        Assert.NotNull(uploadBtn);
        Assert.NotNull(uploadBtn.GetAttribute("disabled"));
    }

    [Fact]
    public async Task VersionUploadModal_UploadVersionButton_EnabledAfterFileSelected()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResume(db);
        await OpenVersionModal(cut);

        await SelectVersionFile(cut, "v2.docx");

        cut.WaitForAssertion(() =>
        {
            var btn = cut.FindAll("button")
                        .FirstOrDefault(b => b.TextContent.Contains("Upload Version"));
            Assert.NotNull(btn);
            Assert.Null(btn.GetAttribute("disabled"));
        });
    }

    [Fact]
    public async Task VersionUploadModal_Confirm_CallsSaveBaseResumeVersionAsync()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1 };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v1)
            .Build();
        var cut = await RenderWithResume(db);
        await OpenVersionModal(cut);
        await SelectVersionFile(cut, "v2.docx");

        var uploadBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Upload Version"));
        await uploadBtn.ClickAsync(new());

        await db.Received(1).SaveBaseResumeVersionAsync(
            Arg.Is<BaseResumeVersion>(v => v.ResumeId == "r1" && v.VersionNumber == 2));
    }

    [Fact]
    public async Task VersionUploadModal_Confirm_DoesNotDeletePreviousVersions()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v1 = new BaseResumeVersion { Id = "v1", ResumeId = "r1", VersionNumber = 1 };
        var db = new TestIndexedDbBuilder()
            .WithBaseResumes(r1)
            .WithResumeVersions("r1", v1)
            .Build();
        var cut = await RenderWithResume(db);
        await OpenVersionModal(cut);
        await SelectVersionFile(cut, "v2.docx");

        var uploadBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Upload Version"));
        await uploadBtn.ClickAsync(new());

        await db.DidNotReceive().DeleteVersionsByResumeAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task VersionUploadModal_Cancel_DoesNotCallSave()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResume(db);
        await OpenVersionModal(cut);

        // Cancel button is .btn-secondary inside the version modal
        await cut.Find(".btn-secondary").ClickAsync(new());

        await db.DidNotReceive().SaveBaseResumeVersionAsync(Arg.Any<BaseResumeVersion>());
    }
}
