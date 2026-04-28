namespace Simply.JobApplication.Models;

public class SessionRecord : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Organization link
    public string? OrganizationId { get; set; }
    public string OrganizationNameSnapshot { get; set; } = "";

    // Opportunity link
    public string? OpportunityId { get; set; }
    public string OpportunityRoleSnapshot { get; set; } = "";

    // Session working copies (used for AI evaluation)
    public string Role { get; set; } = "";
    public string RoleDescription { get; set; } = "";

    // Resume link
    public string BaseResumeVersionId { get; set; } = "";
    public string BaseResumeNameSnapshot { get; set; } = "";
    public int BaseResumeVersionNumberSnapshot { get; set; }

    // Evaluation results
    public string MatchScore { get; set; } = "";
    public List<string> MatchGaps { get; set; } = new();
    public List<string> MatchStrengths { get; set; } = new();
    public List<SuggestedKeyword> AdditionalKeywords { get; set; } = new();

    // Generated content
    public string CoverLetterText { get; set; } = "";
    public string WhyApplyText { get; set; } = "";
    public string? TailoredResumeFileId { get; set; }
    public string? CoverLetterFileId { get; set; }
    public bool ArtifactsGenerated { get; set; }
}
