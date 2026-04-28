namespace Simply.JobApplication.Tests.M5;

// M5-3: File attachments in Correspondence modal — stage, size limit, edit load, remove, save.
public class CorrespondenceFileAttachmentTests : BunitContext
{
    private static Opportunity MakeOpp() => new()
    {
        Id = "op1", OrganizationId = "o1", Role = "Dev",
        Stage = OpportunityStage.Open,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static Organization MakeOrg() => new() { Id = "o1", Name = "Acme Corp" };

    private static IBrowserFile MakeFile(string name, long size = 100)
    {
        var bytes = new byte[] { 1, 2, 3 };
        var file  = Substitute.For<IBrowserFile>();
        file.Name.Returns(name);
        file.Size.Returns(size);
        file.ContentType.Returns("application/pdf");
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(bytes));
        return file;
    }

    /// Renders the page and opens an Add Correspondence modal (Email).
    private async Task<IRenderedComponent<OpportunityDetailPage>> RenderAndOpenAddModal(IIndexedDbService db)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAppServices(db);
        var cut = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        await cut.Find(".dropdown-toggle.btn-primary").ClickAsync(new());
        var emailItem = cut.FindAll(".dropdown-item").First(a => a.TextContent.Trim() == "Email");
        await emailItem.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("New Email", cut.Markup));
        return cut;
    }

    [Fact]
    public async Task FileAttachments_StageFile_ShowsInStagedList()
    {
        var db  = new TestIndexedDbBuilder().WithOpportunity(MakeOpp()).WithOrganization(MakeOrg()).Build();
        var cut = await RenderAndOpenAddModal(db);

        var inputFile = cut.FindComponent<InputFile>();
        var args      = new InputFileChangeEventArgs(new[] { MakeFile("cover.pdf") });
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(args));

        cut.WaitForAssertion(() => Assert.Contains("cover.pdf", cut.Markup));
    }

    [Fact]
    public async Task FileAttachments_OversizedFile_ShowsWarning_FileNotStaged()
    {
        const long overLimit = 5L * 1024 * 1024 + 1;
        var db  = new TestIndexedDbBuilder().WithOpportunity(MakeOpp()).WithOrganization(MakeOrg()).Build();
        var cut = await RenderAndOpenAddModal(db);

        var inputFile = cut.FindComponent<InputFile>();
        var args      = new InputFileChangeEventArgs(new[] { MakeFile("huge.pdf", overLimit) });
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(args));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("5 MB limit", cut.Markup);
            // File should not appear in the staged list
            Assert.DoesNotContain(cut.FindAll(".list-group-item"), li => li.TextContent.Contains("huge.pdf"));
        });
    }

    [Fact]
    public async Task FileAttachments_RemoveStagedFile_DisappearsFromList()
    {
        var db  = new TestIndexedDbBuilder().WithOpportunity(MakeOpp()).WithOrganization(MakeOrg()).Build();
        var cut = await RenderAndOpenAddModal(db);

        var inputFile = cut.FindComponent<InputFile>();
        var args      = new InputFileChangeEventArgs(new[] { MakeFile("report.docx") });
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(args));
        cut.WaitForAssertion(() => Assert.Contains("report.docx", cut.Markup));

        var removeBtn = cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "Remove" &&
                        (b.ClassName ?? "").Contains("btn-outline-danger"));
        await removeBtn.ClickAsync(new());

        cut.WaitForAssertion(() => Assert.DoesNotContain("report.docx", cut.Markup));
    }

    [Fact]
    public async Task FileAttachments_EditModal_LoadsExistingFiles()
    {
        var corr = new Correspondence
        {
            Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            OccurredAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var existingFile = new CorrespondenceFile
        {
            Id = "f1", CorrespondenceId = "c1", FileName = "existing.docx", FileSize = 500,
        };
        var db = new TestIndexedDbBuilder()
            .WithOpportunity(MakeOpp())
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr)
            .WithFiles("c1", existingFile)
            .Build();
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAppServices(db);
        var cut = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Edit Email", cut.Markup));

        Assert.Contains("existing.docx", cut.Markup);
    }

    [Fact]
    public async Task FileAttachments_RemoveExistingFile_DisappearsFromList()
    {
        var corr = new Correspondence
        {
            Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            OccurredAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var existingFile = new CorrespondenceFile
        {
            Id = "f1", CorrespondenceId = "c1", FileName = "attached.pdf", FileSize = 200,
        };
        var db = new TestIndexedDbBuilder()
            .WithOpportunity(MakeOpp())
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr)
            .WithFiles("c1", existingFile)
            .Build();
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAppServices(db);
        var cut = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Edit Email", cut.Markup));
        Assert.Contains("attached.pdf", cut.Markup);

        var removeBtn = cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "Remove" &&
                        (b.ClassName ?? "").Contains("btn-outline-danger"));
        await removeBtn.ClickAsync(new());

        cut.WaitForAssertion(() => Assert.DoesNotContain("attached.pdf", cut.Markup));
    }

    [Fact]
    public async Task FileAttachments_Save_PassesNewFilesAndRemovedIds_ToDb()
    {
        var corr = new Correspondence
        {
            Id = "c1", OpportunityId = "op1", Type = CorrespondenceType.Email,
            OccurredAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var existingFile = new CorrespondenceFile
        {
            Id = "f1", CorrespondenceId = "c1", FileName = "old.docx", FileSize = 100,
        };
        var db = new TestIndexedDbBuilder()
            .WithOpportunity(MakeOpp())
            .WithOrganization(MakeOrg())
            .WithCorrespondence("op1", corr)
            .WithFiles("c1", existingFile)
            .Build();
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAppServices(db);
        var cut = Render<OpportunityDetailPage>(p => p.Add(x => x.Id, "op1"));
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        // Open edit modal
        await cut.Find("tbody tr").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Edit Email", cut.Markup));

        // Remove existing file
        var removeBtn = cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "Remove" &&
                        (b.ClassName ?? "").Contains("btn-outline-danger"));
        await removeBtn.ClickAsync(new());

        // Stage a new file
        var inputFile    = cut.FindComponent<InputFile>();
        var newFileArgs  = new InputFileChangeEventArgs(new[] { MakeFile("new.pdf") });
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(newFileArgs));
        cut.WaitForAssertion(() => Assert.Contains("new.pdf", cut.Markup));

        // Save
        var saveBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        await saveBtn.ClickAsync(new());

        await db.Received(1).SaveCorrespondenceWithFilesAsync(
            Arg.Any<Correspondence>(),
            Arg.Is<List<CorrespondenceFile>>(l => l.Count == 1 && l[0].FileName == "new.pdf"),
            Arg.Is<List<string>>(ids => ids.Contains("f1")));
    }
}
