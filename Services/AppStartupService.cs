using System.Text.Json;
using Microsoft.JSInterop;
using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services;

// Runs schema migration and lookup seeding once per app session.
// Called from App.razor OnInitializedAsync before any navigation renders.
public class AppStartupService
{
    private readonly IIndexedDbService _db;
    private bool _initialized;

    public AppStartupService(IIndexedDbService db) => _db = db;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await RunMigrationAsync();
        await SeedLookupsAsync();
    }

    // ── Migration ─────────────────────────────────────────────────────────────

    private async Task RunMigrationAsync()
    {
        var version = await _db.GetSchemaVersionAsync();
        if (version >= 2) return;

        await _db.ClearSessionsAsync();
        await _db.ClearFilesAsync();
        await _db.SetSchemaVersionAsync(2);
    }

    // ── Lookup seeding ────────────────────────────────────────────────────────

    private static readonly string[] DefaultIndustries =
    [
        "Technology", "Finance", "Healthcare", "Retail", "Manufacturing",
        "Education", "Government", "Non-Profit", "Other",
    ];

    private static readonly string[] DefaultContactRoles =
    [
        "Recruiter", "Hiring Manager", "Interviewer", "Reference", "Other",
    ];

    private async Task SeedLookupsAsync()
    {
        var industries = await _db.GetLookupValuesAsync("lookupIndustries");
        if (industries.Count == 0)
            foreach (var v in DefaultIndustries)
                await _db.AddLookupValueAsync("lookupIndustries", new LookupValue { Value = v });

        var roles = await _db.GetLookupValuesAsync("lookupContactRoles");
        if (roles.Count == 0)
            foreach (var v in DefaultContactRoles)
                await _db.AddLookupValueAsync("lookupContactRoles", new LookupValue { Value = v });
    }
}
