namespace Simply.JobApplication.Models;

public class StoredFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string DataBase64 { get; set; } = "";
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public int SessionCount { get; set; } = 0;
}

public class StoredFileMeta
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastUsedAt { get; set; }
    public int SessionCount { get; set; }
}
