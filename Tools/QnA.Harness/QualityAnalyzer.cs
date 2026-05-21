using System.Text.Json.Nodes;
using Simply.JobApplication.Models;
using Simply.JobApplication.Services.QnA;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Shared analysis core for both `analyze-run` (per-run report) and
// `compare-runs` (two-run diff). Reads every session JSON in a run
// directory, applies the QualityRules library, and returns a typed
// RunAnalysis. Caller decides how to format / persist the result.
//
// Lives separately from AnalyzeRun.cs so CompareRuns can call it twice
// without duplicating the rule-dispatch logic or risking drift between
// the two subcommands' interpretation of the rule library.

internal sealed record SessionHit(string RuleId, string MatchedText, string Context);

internal sealed record SessionAnalysis(
    string                    SessionId,
    string                    AnswerText,
    IReadOnlyList<SessionHit> Hits)
{
    // Distinct rule ids that fired on this session. Computed lazily; cheap.
    public IReadOnlySet<string> RuleIds()
        => Hits.Select(h => h.RuleId).ToHashSet(StringComparer.Ordinal);
}

internal sealed class RunAnalysis
{
    public required string RunId           { get; init; }
    public required string RunDirectory    { get; init; }
    public required int    TotalSessions   { get; init; }
    public required int    Analyzed        { get; init; }
    public required int    Skipped         { get; init; }
    public required int    LibrarySize     { get; init; }

    // Count of distinct sessions in which each rule fired (one increment per session,
    // even if the rule matched multiple times within the same answer).
    public required IReadOnlyDictionary<string, int> SessionsPerRule { get; init; }

    // Total raw match count across all sessions for each rule (multiple matches
    // within one session count separately). Higher than SessionsPerRule for
    // rules that repeat within answers.
    public required IReadOnlyDictionary<string, int> RawMatchesPerRule { get; init; }

    // Every analyzed session, keyed by sessionId. Sessions with no rule hits
    // are present with an empty Hits list — this matters for compare-runs,
    // which must know "this session was analyzed and clean" vs "this session
    // wasn't analyzed at all".
    public required IReadOnlyDictionary<string, SessionAnalysis> SessionsById { get; init; }

    public int SessionsWithAnyHit => SessionsById.Values.Count(s => s.Hits.Count > 0);
}

internal static class QualityAnalyzer
{
    public static async Task<RunAnalysis> AnalyzeAsync(string runDir)
    {
        var sessionsDir = Path.Combine(runDir, "sessions");
        if (!Directory.Exists(sessionsDir))
            throw new InvalidOperationException($"No 'sessions' subdirectory under {runDir}");

        var sessionFiles = Directory.GetFiles(sessionsDir, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        var sessionsById      = new Dictionary<string, SessionAnalysis>(StringComparer.Ordinal);
        var sessionsPerRule   = new Dictionary<string, int>(StringComparer.Ordinal);
        var rawMatchesPerRule = new Dictionary<string, int>(StringComparer.Ordinal);
        var skipped           = 0;

        foreach (var path in sessionFiles)
        {
            var (view, answerText) = await TryReadSessionAsync(path);
            if (view is null) { skipped++; continue; }

            var hits = new List<SessionHit>();
            var rulesHitInThisSession = new HashSet<string>(StringComparer.Ordinal);

            foreach (var rule in QualityRules.All)
            {
                var matches = rule.Detect(view).ToList();
                if (matches.Count == 0) continue;

                rulesHitInThisSession.Add(rule.Id);
                rawMatchesPerRule[rule.Id] = rawMatchesPerRule.GetValueOrDefault(rule.Id) + matches.Count;
                foreach (var m in matches)
                    hits.Add(new SessionHit(rule.Id, m.MatchedText, m.Context));
            }

            foreach (var ruleId in rulesHitInThisSession)
                sessionsPerRule[ruleId] = sessionsPerRule.GetValueOrDefault(ruleId) + 1;

            sessionsById[view.SessionId] = new SessionAnalysis(view.SessionId, answerText, hits);
        }

        return new RunAnalysis
        {
            RunId             = Path.GetFileName(runDir),
            RunDirectory      = runDir,
            TotalSessions     = sessionFiles.Length,
            Analyzed          = sessionFiles.Length - skipped,
            Skipped           = skipped,
            LibrarySize       = QualityRules.LibrarySize,
            SessionsPerRule   = sessionsPerRule,
            RawMatchesPerRule = rawMatchesPerRule,
            SessionsById      = sessionsById,
        };
    }

    // Tuple return: the SessionView the detectors need, plus the answer text
    // separately so callers (e.g. compare-runs) can display it without re-reading
    // the file.
    private static async Task<(SessionView? view, string answerText)> TryReadSessionAsync(string path)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path);
            if (JsonNode.Parse(text) is not JsonObject session) return (null, "");

            var sessionId  = TryGetString(session, "sessionId") ?? Path.GetFileNameWithoutExtension(path);
            var answerText = (session["stage2"] as JsonObject) is { } stage2
                ? TryGetString(stage2, "answerText") ?? ""
                : "";

            var focus      = (session["stage1"] as JsonObject)?["focus"] as JsonObject;
            var gapAck     = TryGetString(focus, "gapAcknowledgment") ?? "";
            var boundaries = ToStringList(focus?["boundaries"] as JsonArray);
            var ignore     = ToStringList(focus?["ignore"]     as JsonArray);

            // input.lengthValue / input.lengthUnit are the Stage 2 LENGTH directive
            // inputs. Sessions logged before the length-compliance rule existed do
            // not have these fields — the SessionView record leaves them null and
            // the rule no-ops on null.
            var input        = session["input"] as JsonObject;
            int? lengthValue = input?["lengthValue"] is JsonValue lv && lv.TryGetValue<int>(out var i) ? i : null;
            var lengthUnitStr = TryGetString(input, "lengthUnit");
            QuestionLengthUnit? lengthUnit = Enum.TryParse<QuestionLengthUnit>(lengthUnitStr, ignoreCase: true, out var u) ? u : null;

            // stage1.selectedPriorities is the array Stage 2 actually consumed.
            // Each element has a `resumeEvidence` string; the metric-strip rule
            // walks those for numeric figures. Pre-rule logs simply omit this
            // field, in which case the rule no-ops (null).
            IReadOnlyList<string>? selectedEvidence = null;
            if ((session["stage1"] as JsonObject)?["selectedPriorities"] is JsonArray priorities)
            {
                var list = new List<string>(priorities.Count);
                foreach (var p in priorities)
                {
                    if (p is JsonObject po && TryGetString(po, "resumeEvidence") is { Length: > 0 } e)
                        list.Add(e);
                }
                selectedEvidence = list;
            }

            return (new SessionView(sessionId, answerText, gapAck, boundaries, ignore, lengthValue, lengthUnit, selectedEvidence), answerText);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"warn: skipping {path}: {ex.GetType().Name}: {ex.Message}");
            return (null, "");
        }
    }

    private static string? TryGetString(JsonObject? obj, string key)
        => obj?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static IReadOnlyList<string> ToStringList(JsonArray? arr)
    {
        if (arr is null) return Array.Empty<string>();
        var list = new List<string>(arr.Count);
        foreach (var n in arr)
            if (n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0)
                list.Add(s);
        return list;
    }
}
