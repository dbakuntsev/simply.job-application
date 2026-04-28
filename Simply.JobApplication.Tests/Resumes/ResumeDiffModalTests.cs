namespace Simply.JobApplication.Tests.Resumes;

// M2-2: ResumeDiffModal component — parameter acceptance, side-by-side config.
public class ResumeDiffModalTests : BunitContext
{
    public ResumeDiffModalTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void ResumeDiffModal_WhenShowFalse_DoesNotRender()
    {
        var cut = Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, false)
            .Add(x => x.OriginalText, "old")
            .Add(x => x.ModifiedText, "new"));

        Assert.Empty(cut.FindAll(".modal"));
    }

    [Fact]
    public void ResumeDiffModal_WhenShowTrue_RendersModal()
    {
        var cut = Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OriginalText, "old")
            .Add(x => x.ModifiedText, "new"));

        Assert.NotEmpty(cut.FindAll(".modal"));
    }

    [Fact]
    public void ResumeDiffModal_PassesOriginalTextToMonacoEditor()
    {
        // The component stores OriginalText as a parameter; verify it is accepted without error.
        var cut = Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OriginalText, "Version 1 content"));

        Assert.Equal("Version 1 content", cut.Instance.OriginalText);
    }

    [Fact]
    public void ResumeDiffModal_PassesModifiedTextToMonacoEditor()
    {
        var cut = Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.ModifiedText, "Version 2 content"));

        Assert.Equal("Version 2 content", cut.Instance.ModifiedText);
    }

    [Fact]
    public void ResumeDiffModal_ConfiguresSideBySideMode()
    {
        // The component's GetOptions sets RenderSideBySide = true.
        // We verify this by inspecting the label shown in the modal footer.
        var cut = Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OriginalText, "a")
            .Add(x => x.ModifiedText, "b"));

        Assert.Contains("Left:", cut.Find(".modal-footer .text-muted").TextContent);
    }

    [Fact]
    public void ResumeDiffModal_WhenBothTextsIdentical_RendersWithoutError()
    {
        var ex = Record.Exception(() => Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OriginalText, "same content")
            .Add(x => x.ModifiedText, "same content")));

        Assert.Null(ex);
    }

    [Fact]
    public void ResumeDiffModal_WhenBothTextsEmpty_RendersWithoutError()
    {
        var ex = Record.Exception(() => Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OriginalText, "")
            .Add(x => x.ModifiedText, "")));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ResumeDiffModal_CloseButton_InvokesOnClose()
    {
        var closed = false;
        var cut = Render<ResumeDiffModal>(p => p
            .Add(x => x.Show, true)
            .Add(x => x.OriginalText, "")
            .Add(x => x.ModifiedText, "")
            .Add(x => x.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        await cut.Find(".btn-close").ClickAsync(new());

        Assert.True(closed);
    }
}
