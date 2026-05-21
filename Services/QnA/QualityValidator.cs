using Microsoft.JSInterop;
using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services.QnA;

public sealed class QualityValidator : IQualityValidator
{
    private readonly IJSRuntime    _js;
    private readonly ILoggerService _logger;
    private IJSObjectReference?    _module;
    private bool                   _moduleLoadFailed;

    public QualityValidator(IJSRuntime js, ILoggerService logger)
    {
        _js     = js;
        _logger = logger;
    }

    public async Task<IReadOnlyList<QualityViolation>> ValidateAsync(
        string                answerText,
        string                gapAcknowledgment,
        IReadOnlyList<string> boundaries,
        IReadOnlyList<string> ignore,
        IReadOnlyList<string> selectedResumeEvidence,
        string                organizationName,
        string                roleName,
        int                   lengthValue,
        QuestionLengthUnit    lengthUnit)
    {
        var view = new SessionView(
            SessionId:              "live",
            AnswerText:             answerText,
            GapAcknowledgment:      gapAcknowledgment,
            Boundaries:             boundaries,
            Ignore:                 ignore,
            ExpectedLengthValue:    lengthValue,
            ExpectedLengthUnit:     lengthUnit,
            SelectedResumeEvidence: selectedResumeEvidence);

        var violations = new List<QualityViolation>();

        // C# rules — deterministic, always run.
        foreach (var rule in QualityRules.All)
        {
            foreach (var match in rule.Detect(view))
            {
                violations.Add(new QualityViolation(
                    rule.Id, rule.Kind, rule.Description, match.MatchedText, match.Context));
            }
        }

        // The source-fidelity rule treats permitted-by-prompt references — the
        // target employer name and the role title — as if they were evidence.
        // Without this, the rule fires on every legitimate "Shield" or
        // "Senior Developer" mention and the sampler regenerates an answer
        // that strips the specific reference for a vaguer one. Empties are
        // filtered so the JS-side join doesn't introduce blank tokens.
        var fidelityCorpus = new List<string>(selectedResumeEvidence.Count + 2);
        fidelityCorpus.AddRange(selectedResumeEvidence);
        if (!string.IsNullOrWhiteSpace(organizationName)) fidelityCorpus.Add(organizationName);
        if (!string.IsNullOrWhiteSpace(roleName))         fidelityCorpus.Add(roleName);

        // JS POS rules — best-effort. If wink-nlp can't load (offline first run,
        // CDN outage, etc.) the validator falls back to C#-only and logs once.
        var jsHits = await TryRunJsRulesAsync(answerText, fidelityCorpus);
        violations.AddRange(jsHits);

        return violations;
    }

    private async Task<IReadOnlyList<QualityViolation>> TryRunJsRulesAsync(
        string answerText, IReadOnlyList<string> fidelityCorpus)
    {
        if (_moduleLoadFailed) return Array.Empty<QualityViolation>();

        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/qa-validator.js");

            var rawHits = await _module.InvokeAsync<JsValidatorHit[]>(
                "evaluate", answerText, fidelityCorpus);

            if (rawHits.Length == 0) return Array.Empty<QualityViolation>();

            var result = new List<QualityViolation>(rawHits.Length);
            foreach (var hit in rawHits)
            {
                // JS rules are all structural/contract — Kind is informational; we
                // pick the closest match for telemetry rather than threading a
                // second enum across the JS/C# boundary.
                var kind = hit.RuleId == "source-fidelity-named"
                    ? RuleKind.ContractAdherence
                    : RuleKind.ForbiddenStructural;
                result.Add(new QualityViolation(
                    hit.RuleId, kind, hit.Description, hit.MatchedText, hit.Context));
            }
            return result;
        }
        catch (Exception ex)
        {
            _moduleLoadFailed = true;
            await _logger.WriteLog(new
            {
                qaValidatorError = ex.GetType().Name,
                message          = ex.Message,
                note             = "JS POS rules unavailable for this session; C# rules still applied.",
            });
            return Array.Empty<QualityViolation>();
        }
    }

    private sealed record JsValidatorHit(
        string RuleId,
        string Description,
        string MatchedText,
        string Context);
}
