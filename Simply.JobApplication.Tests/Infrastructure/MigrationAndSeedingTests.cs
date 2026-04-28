namespace Simply.JobApplication.Tests.Infrastructure;

// M1-4: AppStartupService migration tests.
// M1-5: AppStartupService lookup seeding tests.
public class MigrationAndSeedingTests
{
    private static AppStartupService Make(IIndexedDbService db) => new(db);

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
}
