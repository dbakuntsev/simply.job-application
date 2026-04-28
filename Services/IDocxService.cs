namespace Simply.JobApplication.Services;

public interface IDocxService
{
    int? GetPageCount(byte[] docxBytes);
    string ExtractMarkdown(byte[] docxBytes);
    string ExtractTextAsMarkdown(byte[] docxBytes);
    string MarkdownToBodyXml(string markdown, out Dictionary<string, string> hyperlinkRels);
    byte[] GenerateResumeDocx(byte[] originalDocxBytes, string resumeMarkdown);
    byte[] GenerateCoverLetterDocx(string coverLetterText);
}
