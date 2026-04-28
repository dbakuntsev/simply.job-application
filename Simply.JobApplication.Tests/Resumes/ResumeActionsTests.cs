namespace Simply.JobApplication.Tests.Resumes;

// M2-6: Rename and Delete modal tests for MyResumesPage.
public class ResumeActionsTests : BunitContext
{
    private async Task<IRenderedComponent<MyResumesPage>> RenderWithResumes(
        IIndexedDbService db)
    {
        this.AddAppServices(db);
        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return cut;
    }

    // ── Rename modal ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RenameModal_ShowsAfterClickingRename()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var renameBtn = cut.FindAll(".dropdown-item")
                          .First(b => b.TextContent.Trim() == "Rename");
        await renameBtn.ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Rename Resume", cut.Markup));
    }

    [Fact]
    public async Task RenameModal_InputPrefilledWithCurrentName()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var renameBtn = cut.FindAll(".dropdown-item").First(b => b.TextContent.Trim() == "Rename");
        await renameBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Rename Resume", cut.Markup));

        var input = cut.Find(".modal input.form-control");
        var value = input.GetAttribute("value") ?? input.TextContent.Trim();
        Assert.Equal("My Resume", value);
    }

    [Fact]
    public async Task RenameModal_ConflictWithOtherResume_ShowsValidationError()
    {
        var r1 = new BaseResume { Id = "r1", Name = "Resume A" };
        var r2 = new BaseResume { Id = "r2", Name = "Resume B" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1, r2).Build();
        var cut = await RenderWithResumes(db);

        // Click Rename for the first resume (Resume A, sorted alphabetically)
        var renameBtn = cut.FindAll(".dropdown-item").First(b => b.TextContent.Trim() == "Rename");
        await renameBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Rename Resume", cut.Markup));

        // Type a name that conflicts with r2
        var input = cut.Find(".modal input.form-control");
        input.Input("Resume B");

        cut.WaitForAssertion(() =>
            Assert.Contains("is-invalid", cut.Find(".modal input.form-control").ClassName ?? ""));
    }

    [Fact]
    public async Task RenameModal_SameNameAsSelf_NoConflict()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var renameBtn = cut.FindAll(".dropdown-item").First(b => b.TextContent.Trim() == "Rename");
        await renameBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Rename Resume", cut.Markup));

        // Rename button should be enabled when name is same as self
        var renameSubmitBtn = cut.FindAll("button")
                                .FirstOrDefault(b => b.TextContent.Trim() == "Rename");
        Assert.NotNull(renameSubmitBtn);
        Assert.Null(renameSubmitBtn.GetAttribute("disabled"));
    }

    [Fact]
    public async Task RenameModal_Confirm_CallsSaveBaseResumeAsync()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var renameBtn = cut.FindAll(".dropdown-item").First(b => b.TextContent.Trim() == "Rename");
        await renameBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Rename Resume", cut.Markup));

        var input = cut.Find(".modal input.form-control");
        input.Input("New Name");

        // Use modal-scoped selector to avoid matching the "Rename" dropdown item
        var confirmBtn = cut.Find(".modal .btn-primary");
        await confirmBtn.ClickAsync(new());

        await db.Received(1).SaveBaseResumeAsync(
            Arg.Is<BaseResume>(r => r.Id == "r1" && r.Name == "New Name"));
    }

    [Fact]
    public async Task RenameModal_Cancel_DoesNotCallSave()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var renameBtn = cut.FindAll(".dropdown-item").First(b => b.TextContent.Trim() == "Rename");
        await renameBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Rename Resume", cut.Markup));

        await cut.Find(".btn-secondary").ClickAsync(new());

        await db.DidNotReceive().SaveBaseResumeAsync(Arg.Any<BaseResume>());
    }

    // ── Delete modal ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteModal_MessageContainsResumeName()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Important Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var deleteBtn = cut.FindAll(".dropdown-item")
                          .First(b => b.TextContent.Trim() == "Delete");
        await deleteBtn.ClickAsync(new());

        cut.WaitForAssertion(() =>
            Assert.Contains("My Important Resume", cut.Markup));
    }

    [Fact]
    public async Task DeleteModal_Confirm_DeletesVersionsThenResume()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var deleteBtn = cut.FindAll(".dropdown-item").First(b => b.TextContent.Trim() == "Delete");
        await deleteBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Resume", cut.Markup));

        await cut.Find(".btn-danger").ClickAsync(new());

        await db.Received(1).DeleteVersionsByResumeAsync("r1");
        await db.Received(1).DeleteBaseResumeAsync("r1");
    }

    [Fact]
    public async Task DeleteModal_Confirm_DoesNotDeleteSessions()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var cut = await RenderWithResumes(db);

        var deleteBtn = cut.FindAll(".dropdown-item").First(b => b.TextContent.Trim() == "Delete");
        await deleteBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Resume", cut.Markup));

        await cut.Find(".btn-danger").ClickAsync(new());

        await db.DidNotReceive().DeleteSessionAsync(Arg.Any<string>());
        await db.DidNotReceive().ClearSessionsAsync();
    }
}
