namespace Simply.JobApplication.Models;

public class QualificationExtractionResult
{
    public List<string> Required { get; set; } = new();
    public List<string> Preferred { get; set; } = new();
}
