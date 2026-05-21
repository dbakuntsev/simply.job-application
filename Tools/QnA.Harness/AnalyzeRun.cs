using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Simply.JobApplication.Services.QnA;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Subcommand: applies the QualityRules library (via QualityAnalyzer) to every
// session in a single run and emits:
//   - <run>/quality.json — machine-readable; per-rule incidence + per-session
//     hit lists + worst-offenders roll-up.
//   - <run>/quality.md   — human/agent-readable headline table + worst-offender
//     pointer list. Also written to stdout.
//
// Pure-formatter: all data acquisition is in QualityAnalyzer. Adding a rule
// to QualityRules.cs requires zero changes here — the formatter iterates
// QualityRules.All to render the incidence table in stable order.
internal static class AnalyzeRun
{
    private const int WorstOffendersThreshold  = 3;   // sessions with ≥ N distinct rules
    private const int WorstOffendersListLength = 15;  // cap on count shown in markdown

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<int> RunAsync(string[] args, string? repoRoot)
    {
        string? rootOverride = null;
        string? runIdArg     = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("error: --root requires a value");
                        return 2;
                    }
                    rootOverride = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
                default:
                    if (runIdArg is not null)
                    {
                        Console.Error.WriteLine($"error: unexpected argument: {args[i]}");
                        return 2;
                    }
                    runIdArg = args[i];
                    break;
            }
        }

        if (runIdArg is null)
        {
            Console.Error.WriteLine("error: missing run id (use \"latest\" or a timestamp like 20260515-202123)");
            Console.Error.WriteLine("Run `analyze-run --help` for usage.");
            return 2;
        }

        var runsRoot = ResolveRunsRoot(rootOverride, repoRoot);
        var runDir   = RunDirResolver.Resolve(runsRoot, runIdArg);
        if (runDir is null)
        {
            Console.Error.WriteLine($"error: run not found: '{runIdArg}' under '{runsRoot}'");
            return 2;
        }

        var analysis = await QualityAnalyzer.AnalyzeAsync(runDir);

        var worstOffenders = analysis.SessionsById.Values
            .Where(s => s.Hits.Count > 0)
            .Select(s => (s.SessionId, DistinctRules: s.RuleIds().OrderBy(r => r, StringComparer.Ordinal).ToList()))
            .Where(o => o.DistinctRules.Count >= WorstOffendersThreshold)
            .OrderByDescending(o => o.DistinctRules.Count)
            .ThenBy(o => o.SessionId, StringComparer.Ordinal)
            .Take(WorstOffendersListLength)
            .ToList();

        var qualityJson = BuildQualityJson(analysis, worstOffenders);
        var qualityMd   = BuildQualityMarkdown(analysis, worstOffenders);

        await File.WriteAllTextAsync(Path.Combine(runDir, "quality.json"), qualityJson);
        await File.WriteAllTextAsync(Path.Combine(runDir, "quality.md"),   qualityMd);

        Console.Write(qualityMd);
        Console.WriteLine();
        Console.WriteLine($"Wrote: {Path.Combine(runDir, "quality.json")}");
        Console.WriteLine($"Wrote: {Path.Combine(runDir, "quality.md")}");
        return 0;
    }

    private static string ResolveRunsRoot(string? rootOverride, string? repoRoot)
        => rootOverride
           ?? (repoRoot is not null
               ? Path.Combine(repoRoot, "Tools", "QnA.Harness", "runs")
               : Path.Combine(AppContext.BaseDirectory, "runs"));

    private static string BuildQualityJson(
        RunAnalysis analysis,
        IReadOnlyList<(string SessionId, List<string> DistinctRules)> worstOffenders)
    {
        var ruleIncidence = new JsonArray();
        foreach (var rule in QualityRules.All)
        {
            var sessions = analysis.SessionsPerRule.GetValueOrDefault(rule.Id);
            var raw      = analysis.RawMatchesPerRule.GetValueOrDefault(rule.Id);
            var rate     = analysis.Analyzed > 0 ? (double)sessions / analysis.Analyzed : 0.0;
            ruleIncidence.Add(new JsonObject
            {
                ["ruleId"]          = rule.Id,
                ["sourceFix"]       = rule.SourceFix,
                ["kind"]            = rule.Kind.ToString(),
                ["description"]     = rule.Description,
                ["sessionsWithHit"] = sessions,
                ["rawMatchCount"]   = raw,
                ["rate"]            = Math.Round(rate, 4),
            });
        }

        var perSession = new JsonObject();
        foreach (var (sessionId, sa) in analysis.SessionsById
                     .Where(kvp => kvp.Value.Hits.Count > 0)
                     .OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            var hitsArr = new JsonArray();
            foreach (var h in sa.Hits)
                hitsArr.Add(new JsonObject
                {
                    ["ruleId"]      = h.RuleId,
                    ["matchedText"] = h.MatchedText,
                    ["context"]     = h.Context,
                });
            perSession[sessionId] = new JsonObject { ["hits"] = hitsArr };
        }

        var offendersArr = new JsonArray();
        foreach (var o in worstOffenders)
        {
            var ids = new JsonArray();
            foreach (var id in o.DistinctRules) ids.Add(id);
            offendersArr.Add(new JsonObject
            {
                ["sessionId"]         = o.SessionId,
                ["ruleIds"]           = ids,
                ["distinctRuleCount"] = o.DistinctRules.Count,
            });
        }

        return new JsonObject
        {
            ["runId"]              = analysis.RunId,
            ["librarySize"]        = analysis.LibrarySize,
            ["totalSessions"]      = analysis.TotalSessions,
            ["sessionsAnalyzed"]   = analysis.Analyzed,
            ["sessionsSkipped"]    = analysis.Skipped,
            ["sessionsWithAnyHit"] = analysis.SessionsWithAnyHit,
            ["ruleIncidence"]      = ruleIncidence,
            ["perSession"]         = perSession,
            ["worstOffenders"]     = offendersArr,
        }.ToJsonString(new JsonSerializerOptions(_json));
    }

    private static string BuildQualityMarkdown(
        RunAnalysis analysis,
        IReadOnlyList<(string SessionId, List<string> DistinctRules)> worstOffenders)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Quality Analysis: {analysis.RunId}");
        sb.AppendLine();
        sb.AppendLine($"- Sessions analyzed: **{analysis.Analyzed}** of {analysis.TotalSessions}"
            + (analysis.Skipped > 0 ? $" ({analysis.Skipped} skipped due to parse errors)" : ""));
        sb.AppendLine($"- Rule library: **{analysis.LibrarySize}** rules");
        var anyHitPct = analysis.Analyzed > 0 ? 100.0 * analysis.SessionsWithAnyHit / analysis.Analyzed : 0.0;
        sb.AppendLine($"- Sessions with ≥1 rule hit: **{analysis.SessionsWithAnyHit}** ({anyHitPct:0.0}%)");
        sb.AppendLine();

        sb.AppendLine("## Rule incidence");
        sb.AppendLine();
        sb.AppendLine("| Rule | Source | Kind | Sessions hit | Rate | Raw matches |");
        sb.AppendLine("|---|---|---|---:|---:|---:|");

        var ordered = QualityRules.All
            .OrderByDescending(r => analysis.SessionsPerRule.GetValueOrDefault(r.Id))
            .ThenBy(r => r.Id, StringComparer.Ordinal);
        foreach (var rule in ordered)
        {
            var sessions = analysis.SessionsPerRule.GetValueOrDefault(rule.Id);
            var raw      = analysis.RawMatchesPerRule.GetValueOrDefault(rule.Id);
            var rate     = analysis.Analyzed > 0 ? 100.0 * sessions / analysis.Analyzed : 0.0;
            sb.AppendLine($"| `{rule.Id}` | {rule.SourceFix} | {rule.Kind} | {sessions} | {rate:0.0}% | {raw} |");
        }
        sb.AppendLine();

        if (worstOffenders.Count > 0)
        {
            sb.AppendLine($"## Worst offenders (sessions hitting ≥{WorstOffendersThreshold} distinct rules)");
            sb.AppendLine();
            foreach (var o in worstOffenders)
            {
                var ids = string.Join(", ", o.DistinctRules.Select(s => $"`{s}`"));
                sb.AppendLine($"- `{o.SessionId}` — {o.DistinctRules.Count} rules: {ids}");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"_No sessions hit ≥{WorstOffendersThreshold} distinct rules._");
            sb.AppendLine();
        }

        sb.AppendLine("## Where to drill down");
        sb.AppendLine();
        sb.AppendLine("- Full per-session hit detail (with matched text + context): `quality.json` → `perSession.<sessionId>.hits`");
        sb.AppendLine("- Answer text for a tagged session: `sessions/<sessionId>.json` → `stage2.answerText`");
        sb.AppendLine("- Stage 1 focus (gap, boundaries, ignore, selectedPriorities): `sessions/<sessionId>.json` → `stage1.focus` / `stage1.selectedPriorities`");
        sb.AppendLine();

        return sb.ToString();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Q&A Harness — analyze-run subcommand

            Applies the QualityRules library (Services/QnA/QualityRules.cs)
            to every session in a single run. Emits two artifacts into the run
            directory:
              - quality.json — machine-readable: rule incidence, per-session
                hits with matched text and surrounding context, worst-offender
                list (sessions with ≥3 distinct rules tripped).
              - quality.md   — human-readable headline report. Also written to
                stdout for direct capture.

            Usage:
              dotnet run --project Tools/QnA.Harness -- analyze-run <run-id> [options]
              dotnet run --project Tools/QnA.Harness -- analyze-run latest

            Arguments:
              <run-id>      A directory name under Tools/QnA.Harness/runs/
                            (e.g. 20260515-202123), or the literal "latest".

            Options:
              --root <dir>  Override the runs root (default: Tools/QnA.Harness/runs)
              --help        Show this help
            """);
    }
}

// Resolves run-id arguments ("latest" or a timestamp) to absolute directories,
// shared with CompareRuns. Run directory names are UTC timestamps and sort
// lexicographically the same as chronologically, so picking "latest" reduces
// to OrderByDescending(name).First().
internal static class RunDirResolver
{
    public static string? Resolve(string runsRoot, string runId)
    {
        if (!Directory.Exists(runsRoot)) return null;
        if (runId == "latest")
        {
            var dirs = Directory.GetDirectories(runsRoot);
            if (dirs.Length == 0) return null;
            return dirs.OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal).First();
        }
        var direct = Path.Combine(runsRoot, runId);
        return Directory.Exists(direct) ? direct : null;
    }
}
