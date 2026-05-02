namespace Simply.JobApplication.Models;

public class BackupValidationResult
{
    public List<string> Errors   { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public bool HasErrors   => Errors.Count   > 0;
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasIssues   => HasErrors || HasWarnings;
}
