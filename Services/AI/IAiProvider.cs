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
}

public record AiModel(string Id, string DisplayName);
