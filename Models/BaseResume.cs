namespace Simply.JobApplication.Models;

public class BaseResume : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
