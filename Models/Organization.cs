namespace Simply.JobApplication.Models;

public class Organization : IVersioned
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Size { get; set; } = "";
    public string Website { get; set; } = "";
    public string LinkedIn { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
