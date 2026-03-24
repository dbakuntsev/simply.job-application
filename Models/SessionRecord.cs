namespace Simply.JobApplication.Models;

public class SessionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CompanyName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string JobDetails { get; set; } = "";
    public string InputResumeFileId { get; set; } = "";
    public string MatchScore { get; set; } = "";
    public List<string> MatchGaps { get; set; } = new();
    public List<string> MatchStrengths { get; set; } = new();
    public List<SuggestedKeyword> AdditionalKeywords { get; set; } = new();
    public string CoverLetterText { get; set; } = "";
    public string WhyApplyText { get; set; } = "";
    public string? TailoredResumeFileId { get; set; }
    public string? CoverLetterFileId { get; set; }
}
