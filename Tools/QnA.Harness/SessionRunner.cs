using System.Text.Json.Nodes;
using Simply.JobApplication.Models;
using Simply.JobApplication.Services.AI;
using Simply.JobApplication.Services.AI.OpenAi;

namespace Simply.JobApplication.Tools.QnA.Harness;

internal sealed record SessionSpec(
    Fixture Fixture,
    QuestionSpec Question,
    QuestionTone Tone,
    int LengthValue,
    QuestionLengthUnit LengthUnit)
{
    // Stable id that's safe as a file name on Windows.
    public string SessionId =>
        $"{Fixture.Key}_{Question.ExpectedStrategy}_{Tone}_{LengthValue}{LengthUnit.ToString().ToLowerInvariant()[..1]}";
}

internal sealed class SessionResult
{
    public required string SessionId          { get; init; }
    public required DateTimeOffset Timestamp  { get; init; }
    public required string Fixture            { get; init; }
    public required string FixtureDisplayName { get; init; }
    public required string Domain             { get; init; }
    public required string Model              { get; init; }   // Stage 2 model
    public string? Stage1Model                { get; init; }   // null when same as Model

    public required SessionInput Input { get; init; }

    public Stage1Block? Stage1 { get; set; }
    public Stage2Block? Stage2 { get; set; }
    public ResultBlock? Result { get; set; }
    public ErrorBlock?  Error  { get; set; }
    public UsageTotals? Totals { get; set; }
    public IReadOnlyList<UsageRecord>? AllUsage { get; set; }
}

// Per-session sum across all Responses-API calls made on behalf of this session.
// Stage 1 and Stage 2 are the typical contributors; if either fails or is skipped,
// the corresponding fields in Stage1Block / Stage2Block are null and only the
// stages that ran contribute to the totals here.
internal sealed record UsageTotals(
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal CostUsd);

internal sealed record SessionInput(
    string QuestionText,
    string ExpectedStrategy,
    string Tone,
    int LengthValue,
    string LengthUnit,
    string OrgName,
    string OrgDescription,
    string RoleName,
    string RoleDescription,
    string TailoredResumeMarkdown);

internal sealed class Stage1Block
{
    public double LatencyMs           { get; init; }
    public JsonNode? Focus            { get; init; }
    public JsonNode? SelectedPriorities { get; init; }
    public UsageRecord? Usage         { get; init; }
}

internal sealed class Stage2Block
{
    public double LatencyMs    { get; init; }
    public string AnswerText   { get; init; } = "";
    public UsageRecord? Usage  { get; init; }
}

internal sealed class ResultBlock
{
    public string? ActualStrategy        { get; init; }
    public bool StrategyMatchedExpected  { get; init; }
    public bool WasInsufficient          { get; init; }
    public string? ConfidenceRaw         { get; init; }
    public bool Stage2Skipped            { get; init; }
}

internal sealed class ErrorBlock
{
    public string Stage   { get; init; } = "";   // "stage1" | "stage2" | "session"
    public string Type    { get; init; } = "";
    public string Message { get; init; } = "";
}

internal static class SessionRunner
{
    private const string InsufficientSentinel = "I am sorry, I do not have sufficient information to answer this question.";

    // Runs one session. Never throws — failures are captured into result.Error.
    // `stage1Model` overrides the model used for the Stage 1 (focus extraction)
    // call. When null, Stage 1 runs on `model` too. `rateLimit` is shared
    // across all concurrent sessions — it is the only thing that prevents
    // collective TPM exhaustion when --concurrency > 1.
    public static async Task<SessionResult> RunAsync(
        SessionSpec spec,
        string model,
        string? stage1Model,
        string apiKey,
        HttpClient httpClient,
        IRateLimitGate rateLimit,
        Action<string>? onProgress)
    {
        var result = new SessionResult
        {
            SessionId          = spec.SessionId,
            Timestamp          = DateTimeOffset.UtcNow,
            Fixture            = spec.Fixture.Key,
            FixtureDisplayName = spec.Fixture.DisplayName,
            Domain             = spec.Fixture.Domain,
            Model              = model,
            Stage1Model        = stage1Model is null || stage1Model == model ? null : stage1Model,
            Input = new SessionInput(
                QuestionText:           spec.Question.QuestionText,
                ExpectedStrategy:       spec.Question.ExpectedStrategy,
                Tone:                   spec.Tone.ToString(),
                LengthValue:            spec.LengthValue,
                LengthUnit:             spec.LengthUnit.ToString(),
                OrgName:                spec.Fixture.OrgName,
                OrgDescription:         spec.Fixture.OrgDescription,
                RoleName:               spec.Fixture.RoleName,
                RoleDescription:        spec.Fixture.RoleDescription,
                TailoredResumeMarkdown: spec.Fixture.TailoredResumeMarkdown),
        };

        // Fresh provider per session so the (in-memory) state inside the provider
        // is never shared across concurrent runs. HttpClient is shared.
        var logger        = new CapturingLogger();
        var env           = new WasmEnvironmentStub();
        var usageRecorder = new HarnessUsageRecorder();
        var provider      = new OpenAiProvider(httpClient, env, logger, usageRecorder, rateLimit);

        try
        {
            var captured = await CapturingLogger.CaptureAsync(() =>
                provider.AnswerQuestionAsync(
                    questionText:           spec.Question.QuestionText,
                    tone:                   spec.Tone,
                    lengthValue:            spec.LengthValue,
                    lengthUnit:             spec.LengthUnit,
                    orgName:                spec.Fixture.OrgName,
                    orgDescription:         spec.Fixture.OrgDescription,
                    roleName:               spec.Fixture.RoleName,
                    roleDescription:        spec.Fixture.RoleDescription,
                    tailoredResumeMarkdown: spec.Fixture.TailoredResumeMarkdown,
                    modelId:                model,
                    apiKey:                 apiKey,
                    onProgress:             onProgress,
                    stage1ModelId:          stage1Model));

            // OpenAiProvider.AnswerQuestionAsync calls WriteLog up to twice when
            // _environment.IsDevelopment() is true:
            //   items[0] = AnswerFocusResult  (always present)
            //   items[1] = selectedPriorities (skipped if Stage 1 gated to insufficient)
            var items = captured.Items;
            var focus              = items.Count > 0 ? items[0].Node : null;
            var focusElapsed       = items.Count > 0 ? items[0].Elapsed : TimeSpan.Zero;
            var selectedPriorities = items.Count > 1 ? items[1].Node : null;
            var selectionElapsed   = items.Count > 1 ? items[1].Elapsed : focusElapsed;

            var stage1Ms = focusElapsed.TotalMilliseconds;
            var stage2Ms = items.Count > 1
                ? (captured.Total - selectionElapsed).TotalMilliseconds
                : 0.0;

            // Usage attribution by step label emitted from inside OpenAiProvider.
            // AnswerQuestionAsync produces "qa-stage1" (always) and "qa-stage2" (when
            // Stage 1 didn't gate the answer as insufficient).
            var allUsage = usageRecorder.Snapshot();
            var stage1Usage = allUsage.FirstOrDefault(u => u.Step == "qa-stage1");
            var stage2Usage = allUsage.FirstOrDefault(u => u.Step == "qa-stage2");

            result.Stage1 = new Stage1Block
            {
                LatencyMs          = stage1Ms,
                Focus              = focus,
                SelectedPriorities = selectedPriorities,
                Usage              = stage1Usage,
            };

            var answer = captured.Result ?? "";
            var wasInsufficient = answer.Trim() == InsufficientSentinel;
            var stage2Skipped   = wasInsufficient && items.Count <= 1;

            if (!stage2Skipped)
            {
                result.Stage2 = new Stage2Block
                {
                    LatencyMs  = stage2Ms,
                    AnswerText = answer,
                    Usage      = stage2Usage,
                };
            }

            result.AllUsage = allUsage;
            result.Totals = new UsageTotals(
                InputTokens:       allUsage.Sum(u => u.InputTokens),
                CachedInputTokens: allUsage.Sum(u => u.CachedInputTokens),
                OutputTokens:      allUsage.Sum(u => u.OutputTokens),
                ReasoningTokens:   allUsage.Sum(u => u.ReasoningTokens),
                TotalTokens:       allUsage.Sum(u => u.TotalTokens),
                CostUsd:           allUsage.Sum(u => u.CostUsd));

            var actual = focus?["strategy"]?.GetValue<string>();
            var confRaw = focus?["confidence"]?.ToJsonString();
            result.Result = new ResultBlock
            {
                ActualStrategy           = actual,
                StrategyMatchedExpected  = string.Equals(actual, spec.Question.ExpectedStrategy, StringComparison.Ordinal),
                WasInsufficient          = wasInsufficient,
                ConfidenceRaw            = confRaw,
                Stage2Skipped            = stage2Skipped,
            };
        }
        catch (Exception ex)
        {
            result.Error = new ErrorBlock
            {
                Stage   = "session",
                Type    = ex.GetType().FullName ?? ex.GetType().Name,
                Message = ex.Message,
            };

            // Recover any usage that landed before the throw so the run-level
            // totals still reflect what was billed.
            var partial = usageRecorder.Snapshot();
            if (partial.Count > 0)
            {
                result.AllUsage = partial;
                result.Totals = new UsageTotals(
                    InputTokens:       partial.Sum(u => u.InputTokens),
                    CachedInputTokens: partial.Sum(u => u.CachedInputTokens),
                    OutputTokens:      partial.Sum(u => u.OutputTokens),
                    ReasoningTokens:   partial.Sum(u => u.ReasoningTokens),
                    TotalTokens:       partial.Sum(u => u.TotalTokens),
                    CostUsd:           partial.Sum(u => u.CostUsd));
            }
        }

        return result;
    }
}
