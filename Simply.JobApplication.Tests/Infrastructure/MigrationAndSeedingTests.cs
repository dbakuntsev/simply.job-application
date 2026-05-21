namespace Simply.JobApplication.Tests.Infrastructure;

// M1-4: AppStartupService migration tests.
// M1-5: AppStartupService lookup seeding tests.
// M1-6: AppStartupService migration v3 (TailoredResumeMarkdown backfill) tests.
public class MigrationAndSeedingTests
{
    private static AppStartupService Make(IIndexedDbService db) => new(db, Substitute.For<IDocxService>());
    private static AppStartupService Make(IIndexedDbService db, IDocxService docx) => new(db, docx);

    // ── M1-4: Migration ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunMigration_WhenSchemaVersionBelow2_ClearsSessionStore()
    {
        var db = new TestIndexedDbBuilder().WithSchemaVersion(0).Build();
        await Make(db).InitializeAsync();
        await db.Received(1).ClearSessionsAsync();
    }

    [Fact]
    public async Task RunMigration_WhenSchemaVersionBelow2_ClearsFilesStore()
    {
        var db = new TestIndexedDbBuilder().WithSchemaVersion(1).Build();
        await Make(db).InitializeAsync();
        await db.Received(1).ClearFilesAsync();
    }

    [Fact]
    public async Task RunMigration_WhenSchemaVersionBelow2_WritesSchemaVersion2()
    {
        var db = new TestIndexedDbBuilder().WithSchemaVersion(0).Build();
        await Make(db).InitializeAsync();
        await db.Received(1).SetSchemaVersionAsync(2);
    }

    [Fact]
    public async Task RunMigration_WhenSchemaVersionIs2_DoesNotClearAnyStore()
    {
        var db = new TestIndexedDbBuilder().WithSchemaVersion(2).Build();
        await Make(db).InitializeAsync();
        await db.DidNotReceive().ClearSessionsAsync();
        await db.DidNotReceive().ClearFilesAsync();
    }

    [Fact]
    public async Task RunMigration_WhenSchemaVersionIs2_IsIdempotent()
    {
        var db  = new TestIndexedDbBuilder().WithSchemaVersion(2).Build();
        var svc = Make(db);
        await svc.InitializeAsync();
        await svc.InitializeAsync();  // second call — guarded by _initialized flag
        await db.Received(1).GetSchemaVersionAsync();
    }

    // ── M1-5: Lookup seeding ─────────────────────────────────────────────────

    [Fact]
    public async Task SeedLookups_WhenIndustriesEmpty_InsertsAllNineDefaultValues()
    {
        var db = new TestIndexedDbBuilder()
            .WithLookupValues("lookupIndustries")          // empty
            .WithLookupValues("lookupContactRoles", new LookupValue { Value = "Recruiter" })
            .Build();
        await Make(db).InitializeAsync();

        await db.Received(9).AddLookupValueAsync(
            "lookupIndustries", Arg.Any<LookupValue>());
    }

    [Fact]
    public async Task SeedLookups_WhenIndustriesPopulated_DoesNotReseed()
    {
        var db = new TestIndexedDbBuilder()
            .WithLookupValues("lookupIndustries", new LookupValue { Value = "Technology" })
            .WithLookupValues("lookupContactRoles", new LookupValue { Value = "Recruiter" })
            .Build();
        await Make(db).InitializeAsync();

        await db.DidNotReceive().AddLookupValueAsync("lookupIndustries", Arg.Any<LookupValue>());
    }

    [Fact]
    public async Task SeedLookups_WhenContactRolesEmpty_InsertsAllFiveDefaultValues()
    {
        var db = new TestIndexedDbBuilder()
            .WithLookupValues("lookupIndustries", new LookupValue { Value = "Technology" })
            .WithLookupValues("lookupContactRoles")        // empty
            .Build();
        await Make(db).InitializeAsync();

        await db.Received(5).AddLookupValueAsync(
            "lookupContactRoles", Arg.Any<LookupValue>());
    }

    [Fact]
    public async Task SeedLookups_WhenContactRolesPopulated_DoesNotReseed()
    {
        var db = new TestIndexedDbBuilder()
            .WithLookupValues("lookupIndustries", new LookupValue { Value = "Technology" })
            .WithLookupValues("lookupContactRoles", new LookupValue { Value = "Recruiter" })
            .Build();
        await Make(db).InitializeAsync();

        await db.DidNotReceive().AddLookupValueAsync("lookupContactRoles", Arg.Any<LookupValue>());
    }

    // ── M1-6: Migration v3 (TailoredResumeMarkdown backfill) ─────────────────

    [Fact]
    public async Task RunMigration_V3_WhenSessionHasFileButNoMarkdown_PopulatesMarkdown()
    {
        const string fileId = "file-1";
        var file    = new StoredFile { Id = fileId, DataBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }) };
        var session = new SessionRecord { TailoredResumeFileId = fileId, TailoredResumeMarkdown = "" };
        var docx    = Substitute.For<IDocxService>();
        docx.ExtractTextAsMarkdown(Arg.Any<byte[]>()).Returns("# Resume");

        var db = new TestIndexedDbBuilder()
            .WithSchemaVersion(2)
            .WithSessions(session)
            .Build();
        db.GetFileAsync(fileId).Returns(Task.FromResult<StoredFile?>(file));

        await Make(db, docx).InitializeAsync();

        await db.Received(1).SaveSessionAsync(
            Arg.Is<SessionRecord>(s => s.TailoredResumeMarkdown == "# Resume"));
    }

    [Fact]
    public async Task RunMigration_V3_WhenSessionAlreadyHasMarkdown_DoesNotSave()
    {
        const string fileId = "file-1";
        var session = new SessionRecord { TailoredResumeFileId = fileId, TailoredResumeMarkdown = "# Existing" };

        var db = new TestIndexedDbBuilder()
            .WithSchemaVersion(2)
            .WithSessions(session)
            .Build();

        await Make(db).InitializeAsync();

        await db.DidNotReceive().SaveSessionAsync(Arg.Any<SessionRecord>());
    }

    [Fact]
    public async Task RunMigration_V3_WhenSessionHasNoTailoredResumeFileId_SkipsSession()
    {
        var session = new SessionRecord { TailoredResumeFileId = null, TailoredResumeMarkdown = "" };

        var db = new TestIndexedDbBuilder()
            .WithSchemaVersion(2)
            .WithSessions(session)
            .Build();

        await Make(db).InitializeAsync();

        await db.DidNotReceive().SaveSessionAsync(Arg.Any<SessionRecord>());
    }

    [Fact]
    public async Task RunMigration_V3_WhenFileNotFound_SkipsSession()
    {
        var session = new SessionRecord { TailoredResumeFileId = "file-missing", TailoredResumeMarkdown = "" };

        var db = new TestIndexedDbBuilder()
            .WithSchemaVersion(2)
            .WithSessions(session)
            .Build();
        // GetFileAsync returns null by default for unconfigured NSubstitute mocks

        await Make(db).InitializeAsync();

        await db.DidNotReceive().SaveSessionAsync(Arg.Any<SessionRecord>());
    }

    [Fact]
    public async Task RunMigration_V3_WhenExtractionThrows_SkipsSessionAndDoesNotRethrow()
    {
        const string fileId = "file-1";
        var file    = new StoredFile { Id = fileId, DataBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }) };
        var session = new SessionRecord { TailoredResumeFileId = fileId, TailoredResumeMarkdown = "" };
        var docx    = Substitute.For<IDocxService>();
        docx.ExtractTextAsMarkdown(Arg.Any<byte[]>()).Throws(new InvalidOperationException("corrupt DOCX"));

        var db = new TestIndexedDbBuilder()
            .WithSchemaVersion(2)
            .WithSessions(session)
            .Build();
        db.GetFileAsync(fileId).Returns(Task.FromResult<StoredFile?>(file));

        await Make(db, docx).InitializeAsync();  // must not throw

        await db.DidNotReceive().SaveSessionAsync(Arg.Any<SessionRecord>());
    }

    [Fact]
    public async Task RunMigration_V3_SetsSchemaVersionTo3()
    {
        var db = new TestIndexedDbBuilder().WithSchemaVersion(2).Build();
        await Make(db).InitializeAsync();
        await db.Received(1).SetSchemaVersionAsync(3);
    }

    [Fact]
    public async Task RunMigration_WhenSchemaVersionIs3_SkipsV3Migration()
    {
        var db = new TestIndexedDbBuilder().WithSchemaVersion(3).Build();
        await Make(db).InitializeAsync();
        await db.DidNotReceive().GetAllSessionsAsync();
        await db.DidNotReceive().SetSchemaVersionAsync(Arg.Any<int>());
    }
}
