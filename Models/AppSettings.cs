namespace Simply.JobApplication.Models;

public class AppSettings
{
    public string AiProvider { get; set; } = "openai";
    public Dictionary<string, string> ProviderApiKeys { get; set; } = new();
    public Dictionary<string, string> ProviderModels { get; set; } = new();
    public int HistoryLimit { get; set; } = 20;
    public int FilesLimit { get; set; } = 41;
    // null = match the page count of the input resume
    public int? OutputResumePages { get; set; } = null;
}
