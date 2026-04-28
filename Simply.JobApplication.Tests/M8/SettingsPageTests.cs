namespace Simply.JobApplication.Tests.M8;

// M8-1, M8-2: SettingsPage — removed limit fields, storage breakdown.
public class SettingsPageTests : BunitContext
{
    private IRenderedComponent<SettingsPage> RenderSettings(IIndexedDbService? db = null)
    {
        var mocks = this.AddAppServices(db);

        // SettingsPage renders Providers.All and calls Providers.Get(selectedProvider)
        var provider = Substitute.For<IAiProvider>();
        provider.ProviderId.Returns("openai");
        provider.DisplayName.Returns("OpenAI");
        provider.DefaultModelId.Returns("gpt-5.4");
        provider.AvailableModels.Returns(new List<AiModel> { new("gpt-5.4", "GPT-5.4") });

        mocks.AiFactory.All.Returns(new List<IAiProvider> { provider });
        mocks.AiFactory.Get(Arg.Any<string>()).Returns(provider);

        return Render<SettingsPage>();
    }

    // ── M8-1: Removed limit fields ────────────────────────────────────────────

    [Fact]
    public void SettingsPage_DoesNotShowHistoryLimitField()
    {
        var cut = RenderSettings();
        Assert.DoesNotContain("History Limit", cut.Markup);
        Assert.DoesNotContain("HistoryLimit", cut.Markup);
    }

    [Fact]
    public void SettingsPage_DoesNotShowFilesLimitField()
    {
        var cut = RenderSettings();
        Assert.DoesNotContain("Files Limit", cut.Markup);
        Assert.DoesNotContain("FilesLimit", cut.Markup);
    }

    [Fact]
    public void SaveSettings_WithoutHistoryLimit_ModelHasNoSuchProperty()
    {
        // Regression guard: AppSettings must not declare HistoryLimit or FilesLimit
        Assert.Null(typeof(AppSettings).GetProperty("HistoryLimit"));
        Assert.Null(typeof(AppSettings).GetProperty("FilesLimit"));
    }

    // ── M8-2: Storage breakdown ───────────────────────────────────────────────

    [Fact]
    public async Task SettingsPage_StorageBreakdown_ShowsOrganizationsStore()
    {
        var cut = RenderSettings();
        // Storage loads async; wait until _storageLoaded is true (spinner disappears
        // and store labels appear in markup)
        await cut.WaitForStateAsync(() => cut.Markup.Contains("Organizations"), TimeSpan.FromSeconds(2));

        Assert.Contains("Organizations", cut.Markup);
    }

    [Fact]
    public async Task SettingsPage_StorageBreakdown_ShowsCorrespondenceStore()
    {
        var cut = RenderSettings();
        await cut.WaitForStateAsync(() => cut.Markup.Contains("Correspondence"), TimeSpan.FromSeconds(2));

        Assert.Contains("Correspondence", cut.Markup);
    }

    [Fact]
    public async Task SettingsPage_StorageBreakdown_ShowsBaseResumesStore()
    {
        var cut = RenderSettings();
        await cut.WaitForStateAsync(() => cut.Markup.Contains("Resumes"), TimeSpan.FromSeconds(2));

        Assert.Contains("Resumes", cut.Markup);
    }

    [Fact]
    public async Task SettingsPage_StorageBreakdown_TotalPercentageDoesNotExceed100()
    {
        // Set up storage estimate: usage=500, quota=1000 → pct=50%
        JSInterop.Setup<double[]?>("sjaGetStorageEstimate")
            .SetResult(new double[] { 1000.0, 500.0 });

        var cut = RenderSettings();
        await cut.WaitForStateAsync(() => cut.Markup.Contains("50.0%"), TimeSpan.FromSeconds(2));

        // The percentage displayed must be ≤ 100%
        Assert.Contains("50.0%", cut.Markup);
        Assert.DoesNotContain("101%", cut.Markup);
        Assert.DoesNotContain("200%", cut.Markup);
    }
}
