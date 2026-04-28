namespace Simply.JobApplication.Tests.Resumes;

// M2-1: DocxService.ExtractTextAsMarkdown tests using TestDocxBuilder.
public class DocxExtractTests
{
    private static readonly DocxService _svc = new();

    [Fact]
    public void ExtractTextAsMarkdown_Heading1Paragraph_ProducesMarkdownH1()
    {
        var bytes = TestDocxBuilder.WithHeading1("Introduction");
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.StartsWith("# Introduction", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_Heading2Paragraph_ProducesMarkdownH2()
    {
        var bytes = TestDocxBuilder.WithHeading2("Skills");
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.StartsWith("## Skills", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_BoldRun_ProducesMarkdownBold()
    {
        var bytes = TestDocxBuilder.WithBoldRun("Important");
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.Contains("**Important**", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_ItalicRun_ProducesMarkdownItalic()
    {
        var bytes = TestDocxBuilder.WithItalicRun("Emphasized");
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.Contains("*Emphasized*", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_Hyperlink_ProducesMarkdownLink()
    {
        var bytes = TestDocxBuilder.WithHyperlink("Click here", "https://example.com");
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.Contains("[Click here](https://example.com)", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_BulletList_ProducesMarkdownUnorderedList()
    {
        var bytes = TestDocxBuilder.WithBulletList("Apple", "Banana");
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.Contains("- Apple", md);
        Assert.Contains("- Banana", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_NumberedList_ProducesMarkdownOrderedList()
    {
        var bytes = TestDocxBuilder.WithOrderedList("First", "Second");
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.Contains("1. First", md);
        Assert.Contains("1. Second", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_EmptyDocument_ReturnsEmptyString()
    {
        var bytes = TestDocxBuilder.Empty();
        var md    = _svc.ExtractTextAsMarkdown(bytes);
        Assert.Equal("", md);
    }

    [Fact]
    public void ExtractTextAsMarkdown_NullInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _svc.ExtractTextAsMarkdown(null!));
    }

    [Fact]
    public void ExtractTextAsMarkdown_EmptyByteArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _svc.ExtractTextAsMarkdown(Array.Empty<byte>()));
    }
}
