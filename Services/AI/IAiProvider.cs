using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services.AI;

public interface IAiProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    IReadOnlyList<AiModel> AvailableModels { get; }
    string DefaultModelId { get; }

    Task<MatchEvaluation> EvaluateMatchAsync(
        JobDescription job,
        string resumeMarkdown,
        string modelId,
        string apiKey,
        IReadOnlyList<string>? additionalKeywords = null,
        Action<string>? onProgress = null);

    Task<GeneratedMaterials> GenerateMaterialsAsync(
        JobDescription job,
        string resumeMarkdown,
        MatchEvaluation evaluation,
        string modelId,
        string apiKey,
        IReadOnlyList<string>? additionalKeywords = null,
        int sourcePageCount = 2,
        int targetPageCount = 2,
        Action<string>? onProgress = null);

    Task<QualificationExtractionResult> ExtractQualificationsAsync(
        string roleDescription,
        string modelId,
        string apiKey,
        Action<string>? onProgress = null);

    // `modelId` is the Stage 2 generation model. `stage1ModelId` overrides the
    // model used for Stage 1 (question classification + evidence extraction);
    // when null, Stage 1 runs on `modelId` too. Splitting the models lets
    // callers swap a cheaper model into Stage 1 to reduce iteration cost —
    // the harness exposes this as `--stage1-model`.
    Task<string> AnswerQuestionAsync(
        string questionText,
        QuestionTone tone,
        int lengthValue,
        QuestionLengthUnit lengthUnit,
        string orgName,
        string? orgDescription,
        string roleName,
        string roleDescription,
        string tailoredResumeMarkdown,
        string modelId,
        string apiKey,
        Action<string>? onProgress = null,
        string? stage1ModelId = null);

    // Estimates the natural answer length for a question, on demand. Called by
    // the Ask Question modal when the user clicks the "Estimate Length" button —
    // intentionally not invoked automatically, so the user is in control of the
    // extra model call. The estimator sees question text + tone + role/org
    // context but NOT the resume — its job is to judge question shape, not
    // candidate evidence. Use a cheap/fast model where possible.
    Task<AnswerLengthEstimate> EstimateAnswerLengthAsync(
        string questionText,
        QuestionTone tone,
        string orgName,
        string roleName,
        string roleDescription,
        string modelId,
        string apiKey,
        Action<string>? onProgress = null);

    // Returns the per-million-token pricing for `modelId`, or null when the
    // provider does not have rates registered for the given id. Consumers
    // (e.g. the Q&A harness) use this to surface the rate table alongside
    // recorded token usage in run artifacts.
    ModelPricing? GetPricing(string modelId);
}

public record AiModel(string Id, string DisplayName);
