using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services.QnA;

// One mechanical-rule hit produced by the validator. Drives both the
// rejection-sampling feedback turn (re-prompting the model with these
// listed) and the per-attempt telemetry logged for harness consumption.
public sealed record QualityViolation(
    string   RuleId,
    RuleKind Kind,
    string   Description,
    string   MatchedText,
    string   Context);

// Validates a Stage 2 answer against the QualityRules library. Pure C# rules
// (Services/QnA/QualityRules.cs) run synchronously in-process; POS-dependent
// rules run in the browser via JS interop into wwwroot/js/qa-validator.js,
// which loads wink-nlp from CDN. If the JS module fails to load (offline
// first run, CDN outage), the JS rules are skipped silently — the C# rules
// still report.
public interface IQualityValidator
{
    Task<IReadOnlyList<QualityViolation>> ValidateAsync(
        string                answerText,
        string                gapAcknowledgment,
        IReadOnlyList<string> boundaries,
        IReadOnlyList<string> ignore,
        IReadOnlyList<string> selectedResumeEvidence,
        // organizationName and roleName are not "evidence" in the resumeEvidence
        // sense — the Stage 2 prompt allows direct reference to the target
        // employer / role title — but the source-fidelity rule needs to know
        // they are permitted so it does not flag them as fabrications. Role
        // *description* and Stage 1 employerConcern are intentionally excluded
        // because they mention technologies the candidate may not actually have.
        string                organizationName,
        string                roleName,
        int                   lengthValue,
        QuestionLengthUnit    lengthUnit);
}
