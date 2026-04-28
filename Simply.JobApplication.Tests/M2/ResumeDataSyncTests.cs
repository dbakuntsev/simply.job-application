namespace Simply.JobApplication.Tests.M2;

// M2-8: Live update tests — OnBaseResumeChanged / OnBaseResumeVersionChanged refresh the list.
public class ResumeDataSyncTests : BunitContext
{
    [Fact]
    public async Task OnBaseResumeChanged_TriggersReload()
    {
        // Start with no resumes, then add one and fire the event
        var r1 = new BaseResume { Id = "r1", Name = "New Resume" };
        var db = new TestIndexedDbBuilder().Build();
        var mocks = this.AddAppServices(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("No resumes yet", cut.Markup);

        // Update mock to return the new resume
        db.GetAllBaseResumesAsync().Returns(Task.FromResult(new List<BaseResume> { r1 }));

        mocks.DataSync.Raise("baseResume", r1.Id, "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("New Resume"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("New Resume", cut.Markup);
    }

    [Fact]
    public async Task OnBaseResumeChanged_Updated_ReloadsData()
    {
        var r1 = new BaseResume { Id = "r1", Name = "Old Name" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var mocks = this.AddAppServices(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("Old Name", cut.Markup);

        // Simulate rename applied externally
        db.GetAllBaseResumesAsync().Returns(
            Task.FromResult(new List<BaseResume> { new BaseResume { Id = "r1", Name = "New Name" } }));

        mocks.DataSync.Raise("baseResume", r1.Id, "updated");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("New Name"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("New Name", cut.Markup);
    }

    [Fact]
    public async Task OnBaseResumeChanged_Deleted_ReloadsData()
    {
        var r1 = new BaseResume { Id = "r1", Name = "Resume To Delete" };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var mocks = this.AddAppServices(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        Assert.Contains("Resume To Delete", cut.Markup);

        // Remove from mock
        db.GetAllBaseResumesAsync().Returns(Task.FromResult(new List<BaseResume>()));

        mocks.DataSync.Raise("baseResume", r1.Id, "deleted");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("No resumes yet"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("No resumes yet", cut.Markup);
    }

    [Fact]
    public async Task OnBaseResumeVersionChanged_Created_ReloadsData()
    {
        var r1 = new BaseResume { Id = "r1", Name = "My Resume" };
        var v1 = new BaseResumeVersion
        {
            Id = "v1", ResumeId = "r1", VersionNumber = 1,
            UploadedAt = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        var db = new TestIndexedDbBuilder().WithBaseResumes(r1).Build();
        var mocks = this.AddAppServices(db);

        var cut = Render<MyResumesPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());

        // Add a version via broadcast
        db.GetVersionsByResumeAsync("r1").Returns(Task.FromResult(new List<BaseResumeVersion> { v1 }));

        mocks.DataSync.Raise("baseResumeVersion", v1.Id, "created");

        // After reload, the version date should appear
        await cut.WaitForStateAsync(() => cut.Markup.Contains("2024"),
            TimeSpan.FromSeconds(2));
        Assert.Contains("2024", cut.Markup);
    }
}
