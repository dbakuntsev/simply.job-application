using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Simply.JobApplication.Services.AI;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Emits the run artifacts:
//   <root>/index.json                          — run-level summary, one entry per session
//   <root>/sessions/<sessionId>.json           — full detail per session
//   <root>/summary.md                          — human/agent-readable, grouped by strategy
//   <root>/run-meta.json                       — model, timestamp, args, totals
internal static class OutputWriter
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void WriteSession(string runDir, SessionResult session)
    {
        var sessionsDir = Path.Combine(runDir, "sessions");
        Directory.CreateDirectory(sessionsDir);
        var path = Path.Combine(sessionsDir, $"{session.SessionId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(session, _json));
    }

    public static void WriteRunMeta(string runDir, RunMeta meta)
    {
        Directory.CreateDirectory(runDir);
        File.WriteAllText(Path.Combine(runDir, "run-meta.json"),
            JsonSerializer.Serialize(meta, _json));
    }

    public static void WriteIndex(string runDir, IReadOnlyList<SessionResult> sessions)
    {
        var entries = sessions
            .OrderBy(s => s.Fixture)
            .ThenBy(s => Array.IndexOf(Strategies.All, s.Input.ExpectedStrategy))
            .ThenBy(s => s.Input.Tone)
            .ThenBy(s => s.Input.LengthValue)
            .Select(s => new JsonObject
            {
                ["sessionId"]                = s.SessionId,
                ["path"]                     = $"sessions/{s.SessionId}.json",
                ["fixture"]                  = s.Fixture,
                ["domain"]                   = s.Domain,
                ["expectedStrategy"]         = s.Input.ExpectedStrategy,
                ["actualStrategy"]           = s.Result?.ActualStrategy,
                ["strategyMatchedExpected"]  = s.Result?.StrategyMatchedExpected ?? false,
                ["tone"]                     = s.Input.Tone,
                ["lengthValue"]              = s.Input.LengthValue,
                ["lengthUnit"]               = s.Input.LengthUnit,
                ["wasInsufficient"]          = s.Result?.WasInsufficient ?? false,
                ["stage2Skipped"]            = s.Result?.Stage2Skipped ?? false,
                ["stage1LatencyMs"]          = s.Stage1?.LatencyMs,
                ["stage2LatencyMs"]          = s.Stage2?.LatencyMs,
                ["stage1TotalTokens"]        = s.Stage1?.Usage?.TotalTokens,
                ["stage2TotalTokens"]        = s.Stage2?.Usage?.TotalTokens,
                ["totalTokens"]              = s.Totals?.TotalTokens,
                ["costUsd"]                  = s.Totals?.CostUsd,
                ["answerPreview"]            = Preview(s.Stage2?.AnswerText, 160),
                ["error"]                    = s.Error is null ? null : new JsonObject
                {
                    ["stage"]   = s.Error.Stage,
                    ["type"]    = s.Error.Type,
                    ["message"] = s.Error.Message,
                },
            })
            .ToList();

        var root = new JsonObject
        {
            ["count"]    = entries.Count,
            ["sessions"] = new JsonArray(entries.Cast<JsonNode?>().ToArray()),
        };
        File.WriteAllText(Path.Combine(runDir, "index.json"), root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void WriteSummary(string runDir, RunMeta meta, IReadOnlyList<SessionResult> sessions)
    {
        var sb = new StringBuilder();
        sb.Append("# Q&A Harness Run Summary\n\n");
        if (meta.Git is { } g)
        {
            var dirtyTag = g.IsDirty ? $"DIRTY ({g.DirtyFileCount} file{(g.DirtyFileCount == 1 ? "" : "s")})" : "clean";
            sb.Append($"- **Commit**: `{g.ShortSha}` on `{g.Branch}` — {dirtyTag}\n");
            if (!string.IsNullOrEmpty(g.CommitSubject))
                sb.Append($"- **Subject**: {g.CommitSubject}\n");
            if (g.IsDirty && g.DirtyFiles.Count > 0)
            {
                sb.Append("- **Dirty files (sample)**:\n");
                foreach (var f in g.DirtyFiles) sb.Append($"  - `{f}`\n");
            }
        }
        else
        {
            sb.Append("- **Commit**: _unknown — git unavailable or not a repo_\n");
        }
        sb.Append($"- **Run started**: {meta.StartedUtc:O}\n");
        sb.Append($"- **Finished**: {meta.FinishedUtc:O}\n");
        if (meta.Stage1Model is not null)
        {
            sb.Append($"- **Model (Stage 2)**: `{meta.Model}`\n");
            sb.Append($"- **Model (Stage 1)**: `{meta.Stage1Model}`\n");
        }
        else
        {
            sb.Append($"- **Model**: `{meta.Model}`\n");
        }
        sb.Append($"- **Sessions**: {sessions.Count}\n");
        sb.Append($"- **Errors**: {sessions.Count(s => s.Error is not null)}\n");
        sb.Append($"- **Strategy mismatches**: {sessions.Count(s => s.Result is { } r && !r.StrategyMatchedExpected && s.Error is null)}\n");
        sb.Append($"- **Insufficient outcomes**: {sessions.Count(s => s.Result?.WasInsufficient == true)}\n");
        if (meta.ObservedRateLimits.Count > 0)
        {
            foreach (var (model, snap) in meta.ObservedRateLimits.OrderBy(kvp => kvp.Key))
            {
                var lim   = snap.LimitTokens?.ToString("N0") ?? "?";
                var rem   = snap.RemainingTokens?.ToString("N0") ?? "?";
                var reset = snap.ResetTokens is { } rt ? $"{rt.TotalSeconds:0.0}s" : "?";
                sb.Append($"- **Observed rate limit (`{model}`)**: {lim} TPM · {rem} remaining · reset {reset}\n");
            }
        }
        sb.Append($"- **CLI args**: `{meta.CommandLine}`\n");

        // Usage & cost rollup, with per-step breakdown so the agent can quote
        // Stage 1 vs Stage 2 totals without re-aggregating per-session files.
        if (meta.UsageTotals is { } u)
        {
            sb.Append($"- **Total tokens**: {u.TotalTokens:N0}  (input {u.InputTokens:N0}, cached {u.CachedInputTokens:N0}, output {u.OutputTokens:N0}");
            if (u.ReasoningTokens > 0) sb.Append($", reasoning {u.ReasoningTokens:N0}");
            sb.Append(")\n");
            sb.Append($"- **Total cost**: ${u.CostUsd:0.0000} USD\n");
            if (meta.Pricing is { } p)
                sb.Append($"- **Rates ({p.ModelId})**: input ${p.InputPerMillion}/1M · cached ${p.CachedInputPerMillion}/1M · output ${p.OutputPerMillion}/1M\n");
            sb.Append('\n');

            if (u.ByStep.Count > 0)
            {
                sb.Append("### Usage by step\n\n");
                sb.Append("| Step | Calls | Input | Cached | Output | Reasoning | Total | Cost (USD) |\n");
                sb.Append("|---|---:|---:|---:|---:|---:|---:|---:|\n");
                foreach (var (stepName, stepTotals) in u.ByStep.OrderBy(kvp => kvp.Key))
                {
                    sb.Append($"| {stepName} | {stepTotals.Calls:N0} | {stepTotals.InputTokens:N0} | {stepTotals.CachedInputTokens:N0} | {stepTotals.OutputTokens:N0} | {stepTotals.ReasoningTokens:N0} | {stepTotals.TotalTokens:N0} | ${stepTotals.CostUsd:0.0000} |\n");
                }
                sb.Append('\n');
            }
        }
        else
        {
            sb.Append('\n');
        }

        // Group: strategy → table of (fixture, tone, length) → answer preview / error / classification.
        foreach (var strategy in Strategies.All)
        {
            var group = sessions
                .Where(s => s.Input.ExpectedStrategy == strategy)
                .OrderBy(s => s.Fixture)
                .ThenBy(s => s.Input.Tone)
                .ThenBy(s => s.Input.LengthValue)
                .ToList();

            if (group.Count == 0) continue;

            sb.Append($"## {strategy}\n\n");
            sb.Append("| Fixture | Tone | Len | Classified | Match | Insufficient | Answer / Error |\n");
            sb.Append("|---|---|---|---|---|---|---|\n");
            foreach (var s in group)
            {
                var classified   = s.Result?.ActualStrategy ?? "—";
                var match        = s.Result?.StrategyMatchedExpected == true ? "✓" : (s.Result is null ? "—" : "✗");
                var insufficient = s.Result?.WasInsufficient == true ? "yes" : "no";
                var body         = s.Error is not null
                    ? $"_error: {EscapeCell(s.Error.Message)}_"
                    : EscapeCell(Preview(s.Stage2?.AnswerText ?? "(insufficient)", 220));
                sb.Append($"| {s.Fixture} | {s.Input.Tone} | {s.Input.LengthValue}{s.Input.LengthUnit[..1].ToLowerInvariant()} | {classified} | {match} | {insufficient} | {body} |\n");
            }
            sb.Append('\n');
        }

        // Errors block: one section per error for easy reading.
        var errors = sessions.Where(s => s.Error is not null).ToList();
        if (errors.Count > 0)
        {
            sb.Append("## Errors\n\n");
            foreach (var s in errors)
            {
                sb.Append($"### {s.SessionId}\n\n");
                sb.Append($"- stage: `{s.Error!.Stage}`\n");
                sb.Append($"- type: `{s.Error.Type}`\n\n```\n{s.Error.Message}\n```\n\n");
            }
        }

        File.WriteAllText(Path.Combine(runDir, "summary.md"), sb.ToString());
    }

    private static string Preview(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var single = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return single.Length <= max ? single : single[..max] + "…";
    }

    private static string EscapeCell(string s) =>
        s.Replace("|", "\\|").Replace('\n', ' ').Replace('\r', ' ');
}

internal sealed record RunMeta(
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    string Model,                  // Stage 2 model id
    string? Stage1Model,           // Stage 1 override (null when same as Model)
    string CommandLine,
    int Concurrency,
    int SessionCount,
    ModelPricing? Pricing,         // Pricing for Model (Stage 2)
    ModelPricing? Stage1Pricing,   // Pricing for Stage1Model (null when same as Pricing)
    RunUsageTotals? UsageTotals,
    GitInfo? Git,
    // Last RateLimitSnapshot observed by the gate for each model, captured
    // from `x-ratelimit-*` response headers. Empty when no API calls have
    // landed yet (e.g. --dry-run, or every session failed before headers
    // came back). Useful in forensic analysis: it records the upstream
    // limits that were in effect for the run, anchoring throughput
    // comparisons across runs done on different account tiers.
    IReadOnlyDictionary<string, RateLimitSnapshot> ObservedRateLimits);

// Sum of all UsageRecords across all sessions in the run, plus a per-step
// breakdown (e.g. qa-stage1 vs qa-stage2). Emitted into run-meta.json so an
// agent can read totals without re-summing per-session files.
internal sealed record RunUsageTotals(
    int     InputTokens,
    int     CachedInputTokens,
    int     OutputTokens,
    int     ReasoningTokens,
    int     TotalTokens,
    decimal CostUsd,
    IReadOnlyDictionary<string, RunStepTotals> ByStep);

internal sealed record RunStepTotals(
    int     Calls,
    int     InputTokens,
    int     CachedInputTokens,
    int     OutputTokens,
    int     ReasoningTokens,
    int     TotalTokens,
    decimal CostUsd);
