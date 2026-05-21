namespace Simply.JobApplication.Models;

public enum QuestionTone { Formal, Conversational, Concise }

public enum QuestionLengthUnit { Sentences, Paragraphs }

public enum QuestionAnswerStatus { Generating, Done, Error }

public class QuestionAnswerEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string QuestionText { get; set; } = "";
    public QuestionTone Tone { get; set; }
    public int LengthValue { get; set; } = 3;
    public QuestionLengthUnit LengthUnit { get; set; }
    public string? AnswerText { get; set; }
    public QuestionAnswerStatus Status { get; set; } = QuestionAnswerStatus.Generating;
    public string? ErrorMessage { get; set; }

    // Stage 1's natural-length recommendation for this question, independent of
    // the user's chosen length. Nullable so pre-existing persisted answers (and
    // sessions where Stage 1 didn't return the field) round-trip unchanged.
    // Displayed as a small advisory next to the answer when it differs from
    // the user's chosen length.
    public int?                SuggestedLengthValue     { get; set; }
    public QuestionLengthUnit? SuggestedLengthUnit      { get; set; }
    public string?             SuggestedLengthRationale { get; set; }
}

// Result of the on-demand length estimator (IAiProvider.EstimateAnswerLengthAsync).
// Invoked from the Ask Question modal via a user-clicked button; output is
// shown as advisory in the modal and, if the user submits, persisted on the
// entry as SuggestedLength* so the answer-detail view continues to surface it.
public sealed record AnswerLengthEstimate(
    int                Value,
    QuestionLengthUnit Unit,
    string             Rationale);
