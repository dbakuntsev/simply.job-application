namespace Simply.JobApplication.Tests.Resumes;

// M2-4: Upload Resume flow — modal step transitions and confirm/cancel.
public class UploadResumeFlowTests : BunitContext
{
    public UploadResumeFlowTests() { }

    private static IBrowserFile MakeFile(string name, string content = "DOCXDATA")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var file  = Substitute.For<IBrowserFile>();
        file.Name.Returns(name);
        file.Size.Returns((long)bytes.Length);
        file.ContentType.Returns("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(bytes));
        return file;
    }

    private async Task<IRenderedComponent<MyResumesPage>> RenderAndTriggerUpload(
        string fileName, IIndexedDbService? db = null)
    {
        this.AddAppServices(db);
        var cut  = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        var mockFile = MakeFile(fileName);
        var args     = new InputFileChangeEventArgs(new[] { mockFile });
        var inputFile = cut.FindComponent<InputFile>();
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(args));
        return cut;
    }

    [Fact]
    public async Task UploadResumeFlow_Step1_DrivesNameFromFilename()
    {
        var cut = await RenderAndTriggerUpload("my-awesome-resume.docx");
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));

        // The name field should be pre-filled without extension
        var input = cut.Find("input.form-control");
        Assert.Equal("my-awesome-resume", input.GetAttribute("value") ?? input.TextContent.Trim());
    }

    [Fact]
    public async Task UploadResumeFlow_Step2_WhenNameConflict_OffersNewVersionOrNewResume()
    {
        var existing = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(existing).Build();
        var cut = await RenderAndTriggerUpload("My Resume.docx", db);
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));

        // Click Next button inside the modal to move past step 1
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Next").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Resume Already Exists", cut.Markup));

        Assert.Contains("Create new version", cut.Markup);
        Assert.Contains("new resume", cut.Markup);
    }

    [Fact]
    public async Task UploadResumeFlow_Step2_NewResume_SuggestsDeduplicatedName()
    {
        var existing = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(existing).Build();
        var cut = await RenderAndTriggerUpload("My Resume.docx", db);
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Next").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Resume Already Exists", cut.Markup));

        Assert.Contains("My Resume (2)", cut.Markup);
    }

    [Fact]
    public async Task UploadResumeFlow_Step2_WhenNoConflict_SkipsToStep3()
    {
        var cut = await RenderAndTriggerUpload("brand-new.docx");
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));

        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Next").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Notes", cut.Markup));

        Assert.DoesNotContain("Resume Already Exists", cut.Markup);
    }

    [Fact]
    public async Task UploadResumeFlow_Step3_NotesFieldIsOptional()
    {
        var cut = await RenderAndTriggerUpload("new-cv.docx");
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Next").ClickAsync(new());

        // Step 3 — Upload button should be enabled even without notes
        cut.WaitForAssertion(() => Assert.Contains("Notes", cut.Markup));
        var uploadBtn = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Upload"));
        Assert.NotNull(uploadBtn);
        Assert.Null(uploadBtn.GetAttribute("disabled"));
    }

    [Fact]
    public async Task UploadResumeFlow_CancelAtStep1_NoRecordCreated()
    {
        var db = new TestIndexedDbBuilder().Build();
        var cut = await RenderAndTriggerUpload("resume.docx", db);
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));

        await cut.Find(".btn-secondary").ClickAsync(new());

        await db.DidNotReceive().SaveBaseResumeAsync(Arg.Any<BaseResume>());
    }

    [Fact]
    public async Task UploadResumeFlow_CancelAtStep3_NoRecordCreated()
    {
        var db = new TestIndexedDbBuilder().Build();
        var cut = await RenderAndTriggerUpload("resume.docx", db);
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Next").ClickAsync(new());  // step 1 → 3
        cut.WaitForAssertion(() => Assert.Contains("Notes", cut.Markup));

        await cut.Find(".btn-secondary").ClickAsync(new());

        await db.DidNotReceive().SaveBaseResumeAsync(Arg.Any<BaseResume>());
    }

    [Fact]
    public async Task UploadResumeFlow_EachModal_BlocksInAppNavigation()
    {
        // When upload is in progress (step > 0), IsDirty should be true
        var cut = await RenderAndTriggerUpload("resume.docx");
        cut.WaitForAssertion(() => Assert.Contains("Upload Resume", cut.Markup));

        // Attempt navigation
        var nav = Services.GetRequiredService<NavigationManager>();
        _ = cut.InvokeAsync(() => nav.NavigateTo("/organizations"));
        await cut.WaitForStateAsync(() => cut.FindAll(".modal.d-block").Any(m =>
            m.TextContent.Contains("Unsaved Changes")), TimeSpan.FromSeconds(2));

        Assert.Contains(cut.FindAll(".modal.d-block"), m => m.TextContent.Contains("Unsaved Changes"));
    }
}
