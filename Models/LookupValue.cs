namespace Simply.JobApplication.Models;

public class LookupValue
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Value { get; set; } = "";
}
