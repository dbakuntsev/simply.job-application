namespace Simply.JobApplication.Models;

public class BaseResumeVersion : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public string ResumeId { get; set; } = "";
    public int VersionNumber { get; set; }
    public string FileDataBase64 { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
