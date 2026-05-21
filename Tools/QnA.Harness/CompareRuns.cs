using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Simply.JobApplication.Services.QnA;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Subcommand: diffs two runs' quality analyses and emits the two artifacts
// that close the prompt-iteration loop:
//
//   - Rule-delta table: per-rule, baseline % vs candidate % vs Δ. This is
//     the headline answer to "did the fix work?".
//   - Per-session classification: which sessions improved, regressed, mixed,
//     or stayed identical. The regressions list shows baseline answer
//     alongside candidate answer so the reader can see what got worse and
//     why, without opening per-session JSONs by hand.
//
// Both runs are analyzed with the **current** QualityRules library — this
// removes the "was this rule even being detected when the baseline ran?"
// ambiguity. If you add a Fix-N to the prompt and a detector to
// QualityRules.cs simultaneously (the discipline this whole pipeline
// supports), then re-analyze both runs and compare them, the table tells
// you whether Fix-N moved its target rate.
//
// Outputs:
//   - Tools/QnA.Harness/comparisons/<baseline>__vs__<candidate>/compare.md
//   - Tools/QnA.Harness/comparisons/<baseline>__vs__<candidate>/compare.json
//   - Markdown also echoed to stdout for single-shot skill capture.
internal static class CompareRuns
{
    // Caps to keep the markdown skim-friendly. The full picture is always in
    // compare.json — these limits only shape the human-readable view.
    private const int MaxRegressionsShown        = 15;
    private const int MaxImprovementsShown       = 15;
    private const int MaxMixedShown              = 10;
    private const int AnswerTextPreviewLength    = 320;

    // TypeInfoResolver is explicit so JsonNode.ToJsonString can serialize
    // value types we attach to JsonObjects (doubles, ints) in .NET 8's
    // stricter serialization model.
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver     = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    public static async Task<int> RunAsync(string[] args, string? repoRoot)
    {
        string? rootOverride = null;
        var positional       = new List<string>();

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
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count != 2)
        {
            Console.Error.WriteLine("error: compare-runs requires two positional arguments: <baseline> <candidate>");
            Console.Error.WriteLine("Run `compare-runs --help` for usage.");
            return 2;
        }

        var runsRoot = ResolveRunsRoot(rootOverride, repoRoot);
        var baselineDir  = RunDirResolver.Resolve(runsRoot, positional[0]);
        var candidateDir = RunDirResolver.Resolve(runsRoot, positional[1]);

        if (baselineDir is null)
        {
            Console.Error.WriteLine($"error: baseline run not found: '{positional[0]}' under '{runsRoot}'");
            return 2;
        }
        if (candidateDir is null)
        {
            Console.Error.WriteLine($"error: candidate run not found: '{positional[1]}' under '{runsRoot}'");
            return 2;
        }
        if (string.Equals(baselineDir, candidateDir, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("error: baseline and candidate resolve to the same run directory");
            return 2;
        }

        var baseline  = await QualityAnalyzer.AnalyzeAsync(baselineDir);
        var candidate = await QualityAnalyzer.AnalyzeAsync(candidateDir);

        var classification = ClassifySessions(baseline, candidate);

        var outDir = ResolveComparisonsRoot(repoRoot, runsRoot);
        var pairDir = Path.Combine(outDir, $"{baseline.RunId}__vs__{candidate.RunId}");
        Directory.CreateDirectory(pairDir);

        var compareJson = BuildCompareJson(baseline, candidate, classification);
        var compareMd   = BuildCompareMarkdown(baseline, candidate, classification);

        await File.WriteAllTextAsync(Path.Combine(pairDir, "compare.json"), compareJson);
        await File.WriteAllTextAsync(Path.Combine(pairDir, "compare.md"),   compareMd);

        Console.Write(compareMd);
        Console.WriteLine();
        Console.WriteLine($"Wrote: {Path.Combine(pairDir, "compare.json")}");
        Console.WriteLine($"Wrote: {Path.Combine(pairDir, "compare.md")}");
        return 0;
    }

    // ── Classification ─────────────────────────────────────────────────

    private sealed record SessionDelta(
        string                SessionId,
        DeltaKind             Kind,
        IReadOnlyList<string> Cleared,      // ruleIds in baseline ∖ candidate (improvements)
        IReadOnlyList<string> Introduced,   // ruleIds in candidate ∖ baseline (regressions)
        IReadOnlyList<string> Persistent,   // ruleIds in both
        string                BaselineAnswer,
        string                CandidateAnswer);

    private enum DeltaKind { Identical, Improved, Regressed, Mixed }

    private sealed record Classification(
        IReadOnlyList<SessionDelta> All,             // every session present in both runs
        IReadOnlyList<string>       OnlyInBaseline,  // session ids missing from candidate
        IReadOnlyList<string>       OnlyInCandidate);

    private static Classification ClassifySessions(RunAnalysis baseline, RunAnalysis candidate)
    {
        var baseIds = new HashSet<string>(baseline.SessionsById.Keys, StringComparer.Ordinal);
        var candIds = new HashSet<string>(candidate.SessionsById.Keys, StringComparer.Ordinal);

        var common = baseIds.Intersect(candIds, StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var all = new List<SessionDelta>(common.Count);
        foreach (var sessionId in common)
        {
            var b = baseline.SessionsById[sessionId];
            var c = candidate.SessionsById[sessionId];

            var bRules = b.RuleIds();
            var cRules = c.RuleIds();

            var cleared    = bRules.Except(cRules, StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList();
            var introduced = cRules.Except(bRules, StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList();
            var persistent = bRules.Intersect(cRules, StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList();

            DeltaKind kind = (cleared.Count, introduced.Count) switch
            {
                (0, 0) => DeltaKind.Identical,
                (_, 0) => DeltaKind.Improved,
                (0, _) => DeltaKind.Regressed,
                _      => DeltaKind.Mixed,
            };

            all.Add(new SessionDelta(sessionId, kind, cleared, introduced, persistent, b.AnswerText, c.AnswerText));
        }

        return new Classification(
            All:             all,
            OnlyInBaseline:  baseIds.Except(candIds, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList(),
            OnlyInCandidate: candIds.Except(baseIds, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList());
    }

    // ── Formatting: JSON ───────────────────────────────────────────────

    private static string BuildCompareJson(RunAnalysis baseline, RunAnalysis candidate, Classification cls)
    {
        var ruleDeltas = new JsonArray();
        foreach (var rule in QualityRules.All)
        {
            var b = baseline.SessionsPerRule.GetValueOrDefault(rule.Id);
            var c = candidate.SessionsPerRule.GetValueOrDefault(rule.Id);
            var bRate = baseline.Analyzed  > 0 ? (double)b / baseline.Analyzed  : 0.0;
            var cRate = candidate.Analyzed > 0 ? (double)c / candidate.Analyzed : 0.0;
            ruleDeltas.Add(new JsonObject
            {
                ["ruleId"]   = rule.Id,
                ["baseline"] = new JsonObject { ["sessions"] = b, ["rate"] = Math.Round(bRate, 4) },
                ["candidate"]= new JsonObject { ["sessions"] = c, ["rate"] = Math.Round(cRate, 4) },
                ["deltaPp"]  = Math.Round((cRate - bRate) * 100.0, 2),
            });
        }

        var counts = new JsonObject
        {
            ["identical"] = cls.All.Count(s => s.Kind == DeltaKind.Identical),
            ["improved"]  = cls.All.Count(s => s.Kind == DeltaKind.Improved),
            ["regressed"] = cls.All.Count(s => s.Kind == DeltaKind.Regressed),
            ["mixed"]     = cls.All.Count(s => s.Kind == DeltaKind.Mixed),
        };

        JsonObject DeltaToObj(SessionDelta d) => new()
        {
            ["sessionId"]       = d.SessionId,
            ["kind"]            = d.Kind.ToString(),
            ["cleared"]         = JsonArrayOfStrings(d.Cleared),
            ["introduced"]      = JsonArrayOfStrings(d.Introduced),
            ["persistent"]      = JsonArrayOfStrings(d.Persistent),
            ["baselineAnswer"]  = d.BaselineAnswer,
            ["candidateAnswer"] = d.CandidateAnswer,
        };

        var sessions = new JsonArray();
        foreach (var d in cls.All) sessions.Add(DeltaToObj(d));

        return new JsonObject
        {
            ["baseline"]  = MakeRunSummary(baseline),
            ["candidate"] = MakeRunSummary(candidate),
            ["librarySize"]      = QualityRules.LibrarySize,
            ["ruleDeltas"]       = ruleDeltas,
            ["sessionCounts"]    = counts,
            ["onlyInBaseline"]   = JsonArrayOfStrings(cls.OnlyInBaseline),
            ["onlyInCandidate"]  = JsonArrayOfStrings(cls.OnlyInCandidate),
            ["sessions"]         = sessions,
        }.ToJsonString(new JsonSerializerOptions(_json));
    }

    private static JsonObject MakeRunSummary(RunAnalysis a) => new()
    {
        ["runId"]             = a.RunId,
        ["totalSessions"]     = a.TotalSessions,
        ["sessionsAnalyzed"]  = a.Analyzed,
        ["sessionsWithAnyHit"] = a.SessionsWithAnyHit,
    };

    private static JsonArray JsonArrayOfStrings(IEnumerable<string> items)
    {
        var arr = new JsonArray();
        foreach (var s in items) arr.Add(s);
        return arr;
    }

    // ── Formatting: Markdown ───────────────────────────────────────────

    private static string BuildCompareMarkdown(RunAnalysis baseline, RunAnalysis candidate, Classification cls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Quality Comparison");
        sb.AppendLine();
        sb.AppendLine($"- **Baseline**: `{baseline.RunId}` — {baseline.Analyzed} analyzed, {baseline.SessionsWithAnyHit} with hits");
        sb.AppendLine($"- **Candidate**: `{candidate.RunId}` — {candidate.Analyzed} analyzed, {candidate.SessionsWithAnyHit} with hits");
        sb.AppendLine($"- Rule library: **{QualityRules.LibrarySize}** rules (both runs analyzed against the current library)");
        sb.AppendLine();

        if (cls.OnlyInBaseline.Count > 0 || cls.OnlyInCandidate.Count > 0)
        {
            sb.AppendLine("> ⚠ Session-set mismatch detected.");
            if (cls.OnlyInBaseline.Count > 0)
                sb.AppendLine($"> Only in baseline ({cls.OnlyInBaseline.Count}): " + string.Join(", ", cls.OnlyInBaseline.Take(8).Select(s => $"`{s}`")) + (cls.OnlyInBaseline.Count > 8 ? ", …" : ""));
            if (cls.OnlyInCandidate.Count > 0)
                sb.AppendLine($"> Only in candidate ({cls.OnlyInCandidate.Count}): " + string.Join(", ", cls.OnlyInCandidate.Take(8).Select(s => $"`{s}`")) + (cls.OnlyInCandidate.Count > 8 ? ", …" : ""));
            sb.AppendLine();
        }

        // ── Rule delta table ───────────────────────────────────────────
        sb.AppendLine("## Rule deltas");
        sb.AppendLine();
        sb.AppendLine("| Rule | Baseline | Candidate | Δ (pp) | Direction |");
        sb.AppendLine("|---|---:|---:|---:|---|");

        // Order rules by |delta| descending — biggest moves first, regardless of sign.
        var ruleRows = QualityRules.All
            .Select(rule =>
            {
                var b = baseline.SessionsPerRule.GetValueOrDefault(rule.Id);
                var c = candidate.SessionsPerRule.GetValueOrDefault(rule.Id);
                var bRate = baseline.Analyzed  > 0 ? 100.0 * b / baseline.Analyzed  : 0.0;
                var cRate = candidate.Analyzed > 0 ? 100.0 * c / candidate.Analyzed : 0.0;
                return (rule, bRate, cRate, delta: cRate - bRate);
            })
            .OrderByDescending(r => Math.Abs(r.delta))
            .ThenBy(r => r.rule.Id, StringComparer.Ordinal);

        foreach (var (rule, bRate, cRate, delta) in ruleRows)
        {
            var arrow = delta switch
            {
                < -0.05 => "✓ improved",
                >  0.05 => "⚠ regressed",
                _       => "—",
            };
            sb.AppendLine($"| `{rule.Id}` | {bRate:0.0}% | {cRate:0.0}% | {(delta >= 0 ? "+" : "")}{delta:0.0} | {arrow} |");
        }
        sb.AppendLine();

        // ── Session classification counts ──────────────────────────────
        var nIdentical = cls.All.Count(s => s.Kind == DeltaKind.Identical);
        var nImproved  = cls.All.Count(s => s.Kind == DeltaKind.Improved);
        var nRegressed = cls.All.Count(s => s.Kind == DeltaKind.Regressed);
        var nMixed     = cls.All.Count(s => s.Kind == DeltaKind.Mixed);

        sb.AppendLine("## Session classification");
        sb.AppendLine();
        sb.AppendLine($"- Identical (no rule-hit change): **{nIdentical}**");
        sb.AppendLine($"- Improved (cleared rules, none introduced): **{nImproved}**");
        sb.AppendLine($"- Regressed (introduced rules, none cleared): **{nRegressed}**");
        sb.AppendLine($"- Mixed (both cleared and introduced): **{nMixed}**");
        sb.AppendLine();

        // ── Regressions (with side-by-side answer text) ────────────────
        var regressed = cls.All.Where(s => s.Kind == DeltaKind.Regressed).ToList();
        sb.AppendLine($"## Regressions ({regressed.Count})");
        sb.AppendLine();
        if (regressed.Count == 0)
        {
            sb.AppendLine("_None._");
            sb.AppendLine();
        }
        else
        {
            foreach (var d in regressed.Take(MaxRegressionsShown))
            {
                sb.AppendLine($"### `{d.SessionId}` — +{FormatRuleList(d.Introduced)}");
                sb.AppendLine();
                sb.AppendLine("**Baseline:**");
                sb.AppendLine();
                sb.AppendLine("> " + EscapeQuote(Truncate(d.BaselineAnswer, AnswerTextPreviewLength)));
                sb.AppendLine();
                sb.AppendLine("**Candidate:**");
                sb.AppendLine();
                sb.AppendLine("> " + EscapeQuote(Truncate(d.CandidateAnswer, AnswerTextPreviewLength)));
                sb.AppendLine();
            }
            if (regressed.Count > MaxRegressionsShown)
            {
                sb.AppendLine($"_…and {regressed.Count - MaxRegressionsShown} more — see `compare.json` → `sessions` (filter `kind == \"Regressed\"`)._");
                sb.AppendLine();
            }
        }

        // ── Mixed (clear and introduce — usually most interesting) ─────
        var mixed = cls.All.Where(s => s.Kind == DeltaKind.Mixed).ToList();
        sb.AppendLine($"## Mixed ({mixed.Count})");
        sb.AppendLine();
        if (mixed.Count == 0)
        {
            sb.AppendLine("_None._");
            sb.AppendLine();
        }
        else
        {
            foreach (var d in mixed.Take(MaxMixedShown))
            {
                sb.AppendLine($"### `{d.SessionId}` — cleared {FormatRuleList(d.Cleared)}, introduced {FormatRuleList(d.Introduced)}");
                sb.AppendLine();
                sb.AppendLine("**Baseline:** " + EscapeQuote(Truncate(d.BaselineAnswer, AnswerTextPreviewLength)));
                sb.AppendLine();
                sb.AppendLine("**Candidate:** " + EscapeQuote(Truncate(d.CandidateAnswer, AnswerTextPreviewLength)));
                sb.AppendLine();
            }
            if (mixed.Count > MaxMixedShown)
            {
                sb.AppendLine($"_…and {mixed.Count - MaxMixedShown} more — see `compare.json`._");
                sb.AppendLine();
            }
        }

        // ── Improvements (collapsed list, just ids + cleared rules) ────
        var improved = cls.All.Where(s => s.Kind == DeltaKind.Improved).ToList();
        sb.AppendLine($"## Improvements ({improved.Count})");
        sb.AppendLine();
        if (improved.Count == 0)
        {
            sb.AppendLine("_None._");
            sb.AppendLine();
        }
        else
        {
            foreach (var d in improved.Take(MaxImprovementsShown))
            {
                sb.AppendLine($"- `{d.SessionId}` — cleared {FormatRuleList(d.Cleared)}");
            }
            if (improved.Count > MaxImprovementsShown)
                sb.AppendLine($"- _…and {improved.Count - MaxImprovementsShown} more — see `compare.json`._");
            sb.AppendLine();
        }

        sb.AppendLine("## Where to drill down");
        sb.AppendLine();
        sb.AppendLine("- Full per-session detail (every classification, full answer text, hit lists): `compare.json` → `sessions`");
        sb.AppendLine("- Per-session quality detail for either run: `runs/<run-id>/quality.json`");
        sb.AppendLine("- Raw answer + Stage 1 focus for any session: `runs/<run-id>/sessions/<sessionId>.json`");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string FormatRuleList(IReadOnlyList<string> ids)
        => ids.Count == 0 ? "_none_" : string.Join(", ", ids.Select(s => $"`{s}`"));

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "_(no answer)_";
        var single = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (single.Length == 0) return "_(no answer)_";
        return single.Length <= max ? single : single[..max] + "…";
    }

    private static string EscapeQuote(string s)
        // Inside a Markdown blockquote, multi-line and embedded > markers can
        // confuse renderers; we already collapsed newlines, so this is light-touch.
        => s.Replace("\\", "\\\\");

    // ── Helpers ────────────────────────────────────────────────────────

    private static string ResolveRunsRoot(string? rootOverride, string? repoRoot)
        => rootOverride
           ?? (repoRoot is not null
               ? Path.Combine(repoRoot, "Tools", "QnA.Harness", "runs")
               : Path.Combine(AppContext.BaseDirectory, "runs"));

    private static string ResolveComparisonsRoot(string? repoRoot, string runsRoot)
        => repoRoot is not null
            ? Path.Combine(repoRoot, "Tools", "QnA.Harness", "comparisons")
            : Path.Combine(Path.GetDirectoryName(runsRoot) ?? AppContext.BaseDirectory, "comparisons");

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Q&A Harness — compare-runs subcommand

            Analyzes two runs against the current QualityRules library, diffs
            them, and emits:
              - Tools/QnA.Harness/comparisons/<baseline>__vs__<candidate>/compare.md
              - Tools/QnA.Harness/comparisons/<baseline>__vs__<candidate>/compare.json
            Markdown is also echoed to stdout for one-shot capture.

            Usage:
              dotnet run --project Tools/QnA.Harness -- compare-runs <baseline> <candidate> [options]
              dotnet run --project Tools/QnA.Harness -- compare-runs 20260514-212139 latest

            Arguments:
              <baseline>    Run id (timestamp) or "latest" — earlier of the two.
              <candidate>   Run id (timestamp) or "latest" — usually the one
                            you just produced and want to evaluate.

            Options:
              --root <dir>  Override the runs root (default: Tools/QnA.Harness/runs)
              --help        Show this help

            Both runs are analyzed with the current QualityRules library, not
            with the library that was in effect when each run was produced.
            This is intentional: rule changes between runs would otherwise
            create spurious "deltas" that don't reflect real prompt-quality
            movement.
            """);
    }
}
