namespace Simply.JobApplication.Models;

public enum HistorySource
{
    DirectEdit,
    StageQuickEdit,
    EvaluateAndGenerate,
    QualificationExtraction,
}

public class FieldChange
{
    public string FieldName { get; set; } = "";
    public string OldValue { get; set; } = "";
}

public class OpportunityFieldHistory : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public string OpportunityId { get; set; } = "";
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public HistorySource Source { get; set; }
    public List<FieldChange> Changes { get; set; } = new();
}
