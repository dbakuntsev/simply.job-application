namespace Simply.JobApplication.Models;

public class ContactOpportunityRole : IVersioned
{
    public string ContactId { get; set; } = "";
    public string OpportunityId { get; set; } = "";
    public int Version { get; set; } = 1;
    public string[] Roles { get; set; } = Array.Empty<string>();
}
