using Simply.JobApplication.Services.AI;
using Simply.JobApplication.Services.AI.OpenAi;
using Simply.JobApplication.Tests.Helpers;

namespace Simply.JobApplication.Tests.QA;

// IAiProvider.GetPricing exposes the per-model rate table that OpenAiProvider
// uses both to compute UsageRecord.CostUsd and to surface pricing in run-meta.
// These tests assert the table is populated for every advertised model and
// that lookup is case-insensitive (the harness writes model ids as the user
// passes them on the CLI, which may differ in case from the dictionary keys).
public class OpenAiProviderPricingTests
{
    private static OpenAiProvider Build()
        => ProviderBuilder.MakeProvider(new EmptyHandler());

    private sealed class EmptyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }

    [Theory]
    [InlineData("gpt-5.4")]
    [InlineData("gpt-5.4-mini")]
    [InlineData("gpt-4.1")]
    [InlineData("gpt-4.1-mini")]
    public void GetPricing_AdvertisedModel_ReturnsConsistentRates(string modelId)
    {
        var pricing = Build().GetPricing(modelId);

        Assert.NotNull(pricing);
        Assert.Equal(modelId, pricing.ModelId);
        Assert.True(pricing.InputPerMillion       > 0,
            $"InputPerMillion for {modelId} should be > 0");
        Assert.True(pricing.OutputPerMillion      > 0,
            $"OutputPerMillion for {modelId} should be > 0");
        Assert.True(pricing.CachedInputPerMillion >= 0,
            $"CachedInputPerMillion for {modelId} should be >= 0");

        // Sanity invariants that hold for OpenAI's published pricing structure:
        // output tokens are always at least as expensive as input tokens, and
        // cache-hit input is never more expensive than fresh input.
        Assert.True(pricing.OutputPerMillion >= pricing.InputPerMillion,
            $"Output rate should be >= input rate for {modelId} " +
            $"({pricing.OutputPerMillion} vs {pricing.InputPerMillion})");
        Assert.True(pricing.CachedInputPerMillion <= pricing.InputPerMillion,
            $"Cached-input rate should be <= input rate for {modelId} " +
            $"({pricing.CachedInputPerMillion} vs {pricing.InputPerMillion})");
    }

    [Theory]
    [InlineData("GPT-5.4")]
    [InlineData("GPT-5.4-Mini")]
    [InlineData("Gpt-4.1")]
    public void GetPricing_CaseInsensitive_ResolvesToSameEntry(string variant)
    {
        var lowered = Build().GetPricing(variant.ToLowerInvariant());
        var asGiven = Build().GetPricing(variant);

        Assert.NotNull(lowered);
        Assert.NotNull(asGiven);
        Assert.Equal(lowered.InputPerMillion,       asGiven.InputPerMillion);
        Assert.Equal(lowered.CachedInputPerMillion, asGiven.CachedInputPerMillion);
        Assert.Equal(lowered.OutputPerMillion,      asGiven.OutputPerMillion);
    }

    [Theory]
    [InlineData("not-a-model")]
    [InlineData("gpt-4.0")]
    [InlineData("")]
    public void GetPricing_UnknownModel_ReturnsNull(string modelId)
    {
        Assert.Null(Build().GetPricing(modelId));
    }
}
