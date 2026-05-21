namespace Simply.JobApplication.Services.AI;

// Token usage from a single AI API call, along with the dollar cost computed
// from the model's pricing at the time of the call. `Step` is a short label
// supplied by the caller inside the provider (e.g. "evaluate", "generate",
// "qa-stage1") so consumers can attribute usage to a logical pipeline stage.
public sealed record UsageRecord(
    string         Step,
    string         Model,
    int            InputTokens,
    int            CachedInputTokens,
    int            OutputTokens,
    int            ReasoningTokens,
    int            TotalTokens,
    decimal        CostUsd,
    DateTimeOffset Timestamp);

// Per-million-token unit prices for one model. CachedInputPerMillion may be
// equal to InputPerMillion if a model does not offer a prompt-cache discount.
public sealed record ModelPricing(
    string  ModelId,
    decimal InputPerMillion,
    decimal CachedInputPerMillion,
    decimal OutputPerMillion);

// Side-channel for token usage. Providers call Record once per API request.
// The default registration (NullUsageRecorder) makes this a no-op for the
// production app; the harness substitutes a capturing implementation.
public interface IUsageRecorder
{
    void Record(UsageRecord record);
}

public sealed class NullUsageRecorder : IUsageRecorder
{
    public static readonly NullUsageRecorder Instance = new();
    public void Record(UsageRecord record) { /* no-op */ }
}
