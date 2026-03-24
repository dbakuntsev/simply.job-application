namespace Simply.JobApplication.Models;

public class MatchEvaluation
{
    public string Score { get; set; } = "";
    public List<string> Gaps { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public bool IsGoodMatch { get; set; }
    public List<SuggestedKeyword> SuggestedKeywords { get; set; } = new();

    // Carries the Responses API response id so GenerateMaterialsAsync can
    // continue the same conversation thread as turn 2.
    public string? AiResponseId { get; set; }
}
