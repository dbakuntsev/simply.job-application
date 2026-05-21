using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Text;
using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services.QnA;

// One Stage 2 generation attempt. Persisted for telemetry so the harness can
// inspect the trajectory across retries: which rules fired on attempt 1,
// whether feedback recovered them on attempt 2, which attempt was finally
// accepted, etc.
public sealed record Stage2AttemptLog
{
    public required int                            AttemptIndex { get; init; }
    public required string                         AnswerText   { get; init; }
    public required IReadOnlyList<QualityViolation> Violations  { get; init; }

    // True for the attempt the sampler ultimately surfaced to the caller —
    // either the first clean attempt, or (when all retries exhaust) the
    // attempt with the fewest violations.
    public bool Accepted { get; init; }
}

// Inputs the sampler needs to validate each attempt against the QualityRules
// library. Pre-bound from Stage 1's focus + the Stage 2 length directive so
// the sampler doesn't depend on OpenAI-specific types.
public sealed record Stage2SamplerInputs(
    string                GapAcknowledgment,
    IReadOnlyList<string> Boundaries,
    IReadOnlyList<string> Ignore,
    IReadOnlyList<string> SelectedResumeEvidence,
    string                OrganizationName,
    string                RoleName,
    int                   LengthValue,
    QuestionLengthUnit    LengthUnit);

public sealed record Stage2SamplerResult(
    string                          AnswerText,
    bool                            Degraded,
    IReadOnlyList<Stage2AttemptLog> Attempts);

// Drives rejection sampling around a delegate that generates one Stage 2
// attempt. Per-attempt flow:
//   1. Caller's delegate produces the next answer. Attempt 1 is the original
//      Stage 2 turn; attempts 2+ receive a feedback string and are expected
//      to continue the Responses-API conversation off the prior attempt so
//      Stage 1 context stays cached server-side.
//   2. The validator scores the answer; clean → return immediately.
//   3. Otherwise the loop builds a feedback turn from the violations and
//      passes it back into the delegate for the next attempt.
//
// On exhaustion, the attempt with the fewest violations is returned and the
// result is marked Degraded so callers / telemetry can surface that the
// sampler couldn't reach a clean output.
public sealed class Stage2RejectionSampler
{
    private const int MaxAttempts = 3;

    private readonly IQualityValidator           _validator;
    private readonly ILoggerService              _logger;
    private readonly IWebAssemblyHostEnvironment _env;

    public Stage2RejectionSampler(
        IQualityValidator           validator,
        ILoggerService              logger,
        IWebAssemblyHostEnvironment env)
    {
        _validator = validator;
        _logger    = logger;
        _env       = env;
    }

    // generateAttempt receives (attemptIndex, feedbackTurnText) and returns the
    // raw Stage 2 model output. feedbackTurnText is null on attempt 1 (use the
    // initial Stage 2 user message) and non-null on retries (re-prompt body).
    public async Task<Stage2SamplerResult> RunAsync(
        Stage2SamplerInputs              inputs,
        Func<int, string?, Task<string>> generateAttempt,
        Action<string>?                  onProgress = null)
    {
        var attempts = new List<Stage2AttemptLog>(capacity: MaxAttempts);
        string? feedbackTurn = null;

        for (var i = 1; i <= MaxAttempts; i++)
        {
            onProgress?.Invoke(i == 1
                ? "Generating answer…"
                : $"Refining answer ({i - 1}/{MaxAttempts - 1})…");

            var raw       = await generateAttempt(i, feedbackTurn);
            var normalized = NormalizeAnswerText(raw, inputs.LengthUnit);

            var violations = await _validator.ValidateAsync(
                normalized,
                inputs.GapAcknowledgment,
                inputs.Boundaries,
                inputs.Ignore,
                inputs.SelectedResumeEvidence,
                inputs.OrganizationName,
                inputs.RoleName,
                inputs.LengthValue,
                inputs.LengthUnit);

            var attempt = new Stage2AttemptLog
            {
                AttemptIndex = i,
                AnswerText   = normalized,
                Violations   = violations,
                Accepted     = false,
            };

            if (violations.Count == 0)
            {
                attempts.Add(attempt with { Accepted = true });
                await LogIfDevAsync(attempts, degraded: false);
                return new Stage2SamplerResult(normalized, Degraded: false, attempts);
            }

            attempts.Add(attempt);
            feedbackTurn = BuildFeedbackTurn(violations);
        }

        // All attempts had violations — pick the one with the fewest and report
        // it as degraded. Ties broken by earliest attempt (cheapest answer).
        var bestIndex = 0;
        for (var i = 1; i < attempts.Count; i++)
        {
            if (attempts[i].Violations.Count < attempts[bestIndex].Violations.Count)
                bestIndex = i;
        }
        attempts[bestIndex] = attempts[bestIndex] with { Accepted = true };
        await LogIfDevAsync(attempts, degraded: true);

        return new Stage2SamplerResult(
            attempts[bestIndex].AnswerText,
            Degraded:  true,
            Attempts:  attempts);
    }

    // The feedback turn fed back into the model on retry. Lists every rule that
    // fired with the matched text and the prompt-rule description, then asks
    // for a clean rewrite. Output mechanics are restated explicitly — the
    // continuation turn carries the original instructions, but the model has
    // already shown it can drift from them, so a terse reminder helps.
    private static string BuildFeedbackTurn(IReadOnlyList<QualityViolation> violations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("The previous answer violated these constraints. Rewrite it to fix every one of them.");
        sb.AppendLine("Output the corrected answer only — plain text, no preamble, no explanation, no markdown.");
        sb.AppendLine();
        sb.AppendLine("Violations:");
        foreach (var v in violations)
        {
            sb.Append("- ").Append(v.RuleId).Append(": ").Append(v.Description);
            if (!string.IsNullOrWhiteSpace(v.MatchedText))
                sb.Append(" Found: \"").Append(v.MatchedText).Append('"');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // Matches OpenAiProvider.NormalizeAnswerText: collapse internal whitespace
    // in sentence-mode answers so the validator's sentence count and the user-
    // visible output agree on what constitutes a sentence.
    private static string NormalizeAnswerText(string content, QuestionLengthUnit lengthUnit)
    {
        var trimmed = content.Trim();
        return lengthUnit == QuestionLengthUnit.Sentences
            ? System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ")
            : trimmed;
    }

    private async Task LogIfDevAsync(IReadOnlyList<Stage2AttemptLog> attempts, bool degraded)
    {
        if (!_env.IsDevelopment()) return;
        await _logger.WriteLog(new
        {
            stage2Sampling = new
            {
                attempts = attempts.Select(a => new
                {
                    attemptIndex = a.AttemptIndex,
                    answerText   = a.AnswerText,
                    accepted     = a.Accepted,
                    violations   = a.Violations.Select(v => new
                    {
                        ruleId      = v.RuleId,
                        kind        = v.Kind.ToString(),
                        matchedText = v.MatchedText,
                        context     = v.Context,
                    }),
                }),
                degraded,
            },
        });
    }
}
