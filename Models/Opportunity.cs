namespace Simply.JobApplication.Models;

public enum OpportunityStage
{
    Open,
    NotInterested,
    NotQualified,
    Applied,
    Interview,
    InProgress,
    Offer,
    Accepted,
    Rejected,
    Withdrawn,
}

public enum WorkArrangement
{
    Remote,
    Hybrid,
    OnSite,
}

public class Opportunity : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public string OrganizationId { get; set; } = "";
    public string Role { get; set; } = "";
    public string RoleDescription { get; set; } = "";
    public string PostingUrl { get; set; } = "";
    public string CompensationRange { get; set; } = "";
    public WorkArrangement? WorkArrangement { get; set; }
    public OpportunityStage Stage { get; set; } = OpportunityStage.Open;
    public string[] RequiredQualifications { get; set; } = Array.Empty<string>();
    public string[] PreferredQualifications { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
