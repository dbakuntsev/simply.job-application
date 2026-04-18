namespace Simply.JobApplication.Models;

public class CorrespondenceFile : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public string CorrespondenceId { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string FileDataBase64 { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
