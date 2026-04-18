namespace Simply.JobApplication.Models;

public enum CorrespondenceType
{
    ResumeSubmitted,
    Email,
    PhoneCall,
    VideoCall,
    Text,
    Interview,
    Other,
}

public enum CorrespondenceDirection
{
    Incoming = 0,
    Outgoing = 1,
}

public class Correspondence : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public string OpportunityId { get; set; } = "";
    public CorrespondenceType Type { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public CorrespondenceDirection Direction { get; set; } = CorrespondenceDirection.Incoming;
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? ContactId { get; set; }
    public string? LinkedSessionId { get; set; }
    public bool CoverLetterSubmitted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
