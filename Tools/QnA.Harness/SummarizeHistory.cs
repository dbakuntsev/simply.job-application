using System.Text;
using System.Text.Json.Nodes;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Subcommand: emits a Markdown report summarizing every run found under
// Tools/QnA.Harness/runs/. Partitions runs by (model, stage1Model) so that
// split-model experiments don't get charted alongside full-stack baselines,
// orders chronologically by run start time, and reports per-run failure
// rates (mismatch / insufficient / error) plus total cost.
//
// The skill `.claude/skills/summarize-history/SKILL.md` wraps this subcommand;
// keeping the computation in compiled C# means there is exactly one source of
// truth for the field names being read out of index.json / run-meta.json —
// agents writing ad-hoc PowerShell against the wrong field names is no longer
// a silent failure mode.
internal static class SummarizeHistory
{
    private const int SubjectMaxLength = 60;

    public static async Task<int> RunAsync(string[] args, string? repoRoot)
    {
        string? rootOverride = null;
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
                    Console.Error.WriteLine($"error: unknown option: {args[i]}");
                    Console.Error.WriteLine("Run `summarize-history --help` for usage.");
                    return 2;
            }
        }

        var runsRoot = rootOverride
                       ?? (repoRoot is not null
                           ? Path.Combine(repoRoot, "Tools", "QnA.Harness", "runs")
                           : Path.Combine(AppContext.BaseDirectory, "runs"));

        if (!Directory.Exists(runsRoot))
        {
            Console.WriteLine("# Q&A Harness Run History");
            Console.WriteLine();
            Console.WriteLine($"_No runs directory found at `{runsRoot}`._");
            return 0;
        }

        var runDirs = Directory.GetDirectories(runsRoot);
        if (runDirs.Length == 0)
        {
            Console.WriteLine("# Q&A Harness Run History");
            Console.WriteLine();
            Console.WriteLine("_No runs to summarize. Run the harness first._");
            return 0;
        }

        var rows = new List<HistoryRow>();
        foreach (var dir in runDirs)
        {
            var row = await TryReadRunAsync(dir);
            if (row is not null) rows.Add(row);
        }

        if (rows.Count == 0)
        {
            Console.WriteLine("# Q&A Harness Run History");
            Console.WriteLine();
            Console.WriteLine($"_No readable runs found under `{runsRoot}`._");
            return 0;
        }

        // Chronological ordering anchored to run start time. This is more
        // robust to rebases than commit-author-date and reflects the order
        // in which observations actually accumulated.
        rows.Sort((a, b) => a.StartedUtc.CompareTo(b.StartedUtc));

        // Partition by configuration. Tuple equality is value-based.
        var partitions = rows
            .GroupBy(r => (r.Model, r.Stage1Model))
            .OrderBy(g => g.Key.Model, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Stage1Model, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Q&A Harness Run History");
        sb.AppendLine();
        sb.Append($"Found {rows.Count} run{(rows.Count == 1 ? "" : "s")} across ");
        sb.AppendLine($"{partitions.Count} configuration{(partitions.Count == 1 ? "" : "s")}.");
        sb.AppendLine();

        foreach (var p in partitions)
        {
            EmitPartition(sb, p.Key.Model, p.Key.Stage1Model, p.OrderBy(r => r.StartedUtc).ToList());
        }

        if (rows.Any(r => r.HasGit && r.IsDirty))
        {
            sb.AppendLine("> † Working tree was dirty at run time — these runs do not correspond to any committed state and should not anchor trend conclusions.");
            sb.AppendLine();
        }
        if (rows.Any(r => !r.HasGit))
        {
            sb.AppendLine("> `_N/A_` in the Commit / Branch / Subject columns indicates a run that predates the git-anchor capture feature and cannot be tied to a commit.");
            sb.AppendLine();
        }

        Console.Write(sb.ToString());
        return 0;
    }

    private static async Task<HistoryRow?> TryReadRunAsync(string dir)
    {
        try
        {
            var metaPath  = Path.Combine(dir, "run-meta.json");
            var indexPath = Path.Combine(dir, "index.json");
            if (!File.Exists(metaPath) || !File.Exists(indexPath)) return null;

            var metaText  = await File.ReadAllTextAsync(metaPath);
            var indexText = await File.ReadAllTextAsync(indexPath);

            if (JsonNode.Parse(metaText)  is not JsonObject meta)  return null;
            if (JsonNode.Parse(indexText) is not JsonObject index) return null;

            var (total, errored, mismatched, insufficient) = CountSessions(index);

            var git = meta["git"] as JsonObject;
            return new HistoryRow
            {
                Dir            = dir,
                StartedUtc     = TryGetDateTimeOffset(meta, "startedUtc"),
                Model          = TryGetString(meta, "model") ?? "?",
                Stage1Model    = TryGetString(meta, "stage1Model"),
                CostUsd        = TryGetDecimal(meta["usageTotals"] as JsonObject, "costUsd") ?? 0m,
                HasGit         = git is not null,
                ShortSha       = TryGetString(git, "shortSha"),
                Branch         = TryGetString(git, "branch"),
                IsDirty        = TryGetBool(git, "isDirty") ?? false,
                CommitSubject  = TryGetString(git, "commitSubject") ?? "",
                Total          = total,
                Errored        = errored,
                Mismatched     = mismatched,
                Insufficient   = insufficient,
            };
        }
        catch (Exception ex)
        {
            // Don't fail the whole report for one corrupt run — flag and skip.
            Console.Error.WriteLine($"warn: skipping {dir}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // Reads index.json's sessions array and tallies the four counts the
    // report needs. `error` is treated as present iff the JSON value is a
    // (non-null) object — null and missing both mean "no error".
    private static (int total, int errored, int mismatched, int insufficient) CountSessions(JsonObject index)
    {
        var total = 0;
        var errored = 0;
        var mismatched = 0;
        var insufficient = 0;

        if (index["sessions"] is not JsonArray sessions) return (0, 0, 0, 0);

        foreach (var node in sessions)
        {
            if (node is not JsonObject session) continue;
            total++;

            if (session["error"] is JsonObject)
            {
                errored++;
                continue;
            }
            // Non-errored: classify against the two failure modes the report tracks.
            if (session["strategyMatchedExpected"] is JsonValue smeV
                && smeV.TryGetValue<bool>(out var matched)
                && !matched)
            {
                mismatched++;
            }
            if (session["wasInsufficient"] is JsonValue wiV
                && wiV.TryGetValue<bool>(out var insuff)
                && insuff)
            {
                insufficient++;
            }
        }

        return (total, errored, mismatched, insufficient);
    }

    private static void EmitPartition(StringBuilder sb, string model, string? stage1Model, List<HistoryRow> rows)
    {
        var heading = stage1Model is null
            ? $"## Model: `{model}` (both stages)"
            : $"## Model: `{model}` (Stage 2) + `{stage1Model}` (Stage 1)";
        sb.AppendLine(heading);
        sb.AppendLine();
        sb.AppendLine("| Commit | Branch | Date (UTC) | Subject | Sessions | Mismatch | Insufficient | Errors | Cost (USD) |");
        sb.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|");
        foreach (var r in rows) sb.AppendLine(FormatRow(r));
        sb.AppendLine();
    }

    private static string FormatRow(HistoryRow r)
    {
        var dirtyMark = r.HasGit && r.IsDirty ? "†" : "";
        var commit    = r.HasGit ? $"`{r.ShortSha ?? "?"}`{dirtyMark}" : "_N/A_";
        var branch    = r.HasGit ? r.Branch ?? "_N/A_" : "_N/A_";
        var date      = r.StartedUtc == default
            ? "_unknown_"
            : r.StartedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm");
        var subject   = r.HasGit ? Truncate(EscapeCell(r.CommitSubject), SubjectMaxLength) : "_N/A_";
        var total     = r.Total;
        // Percentages computed against the full session count. The skill's
        // earlier guidance to optionally divide by (total - errored) lives in
        // the writeup — readers can do that math when errors are large; here
        // we keep a single denominator for cross-row comparability.
        var mmPct     = total == 0 ? 0.0 : 100.0 * r.Mismatched   / total;
        var inPct     = total == 0 ? 0.0 : 100.0 * r.Insufficient / total;
        var errPct    = total == 0 ? 0.0 : 100.0 * r.Errored      / total;
        var cost      = $"${r.CostUsd:0.00}";

        return $"| {commit} | {branch} | {date} | {subject} | {total} | {mmPct:0.0}% | {inPct:0.0}% | {errPct:0.0}% | {cost} |";
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..max] + "…";
    }

    private static string EscapeCell(string s)
        => s.Replace("|", "\\|").Replace('\r', ' ').Replace('\n', ' ');

    private static string? TryGetString(JsonObject? obj, string key)
        => obj?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool? TryGetBool(JsonObject? obj, string key)
        => obj?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static decimal? TryGetDecimal(JsonObject? obj, string key)
        => obj?[key] is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : null;

    private static DateTimeOffset TryGetDateTimeOffset(JsonObject obj, string key)
        => obj[key] is JsonValue v && v.TryGetValue<DateTimeOffset>(out var dto) ? dto : default;

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Q&A Harness — summarize-history subcommand

            Walks Tools/QnA.Harness/runs/*/run-meta.json + index.json, computes
            per-run failure rates (mismatch / insufficient / error) and cost,
            partitions by (model, stage1Model), and emits a Markdown report
            to stdout.

            Usage:
              dotnet run --project Tools/QnA.Harness -- summarize-history [options]

            Options:
              --root <dir>   Override the runs root (default: Tools/QnA.Harness/runs)
              --help         Show this help
            """);
    }

    private sealed class HistoryRow
    {
        public required string         Dir            { get; init; }
        public required DateTimeOffset StartedUtc     { get; init; }
        public required string         Model          { get; init; }
        public          string?        Stage1Model    { get; init; }
        public required decimal        CostUsd        { get; init; }
        public required bool           HasGit         { get; init; }
        public          string?        ShortSha       { get; init; }
        public          string?        Branch         { get; init; }
        public required bool           IsDirty        { get; init; }
        public required string         CommitSubject  { get; init; }
        public required int            Total          { get; init; }
        public required int            Errored        { get; init; }
        public required int            Mismatched     { get; init; }
        public required int            Insufficient   { get; init; }
    }
}
