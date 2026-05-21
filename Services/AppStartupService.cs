using System.Text.Json;
using Microsoft.JSInterop;
using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services;

// Runs schema migration and lookup seeding once per app session.
// Called from App.razor OnInitializedAsync before any navigation renders.
public class AppStartupService
{
    private readonly IIndexedDbService _db;
    private readonly IDocxService _docx;
    private bool _initialized;

    public AppStartupService(IIndexedDbService db, IDocxService docx)
    {
        _db   = db;
        _docx = docx;
    }

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

        if (version < 2)
        {
            await _db.ClearSessionsAsync();
            await _db.ClearFilesAsync();
            version = 2;
            await _db.SetSchemaVersionAsync(version);
        }

        if (version < 3)
        {
            await MigrateTailoredResumeMarkdownAsync();
            await _db.SetSchemaVersionAsync(3);
        }
    }

    // Backfills TailoredResumeMarkdown on sessions that have a tailored resume DOCX
    // but were saved before the field was introduced.
    private async Task MigrateTailoredResumeMarkdownAsync()
    {
        var sessions = await _db.GetAllSessionsAsync();
        foreach (var session in sessions)
        {
            if (session.TailoredResumeFileId is null || !string.IsNullOrEmpty(session.TailoredResumeMarkdown))
                continue;

            var file = await _db.GetFileAsync(session.TailoredResumeFileId);
            if (file is null) continue;

            try
            {
                var bytes = Convert.FromBase64String(file.DataBase64);
                session.TailoredResumeMarkdown = _docx.ExtractTextAsMarkdown(bytes);
                await _db.SaveSessionAsync(session);
            }
            catch
            {
                // Skip sessions whose DOCX cannot be parsed; they simply won't have markdown context.
            }
        }
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
