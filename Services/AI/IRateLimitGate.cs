namespace Simply.JobApplication.Services.AI;

// Process-wide throttle that admits a Responses-API call only when the
// upstream rate-limit state permits it. Implementations must be safe for
// concurrent callers.
//
// Design: the gate's source of truth is the rate-limit information returned
// by the API in response headers (`x-ratelimit-*`). The provider is
// responsible for parsing those headers off every response (success or
// 429) and calling `UpdateLimits` so the gate's view stays current. The
// gate itself maintains no policy beyond "admit when remaining ≥ estimated;
// otherwise wait until reset".
//
// Lifecycle:
//   using var lease = await gate.AcquireAsync(modelId, estimated);
//   ... issue the API call ...
//   gate.UpdateLimits(modelId, snapshotFromHeaders);     // server state
//   lease.Settle(actualTotalTokens);                     // accounting only
//
// AcquireAsync blocks until admission is safe. UpdateLimits is one-way and
// non-blocking. Settle is informational — it does not affect admission.
public interface IRateLimitGate
{
    Task<IRateLimitLease> AcquireAsync(string modelId, int estimatedTokens, CancellationToken ct = default);

    // Push the latest server-reported rate-limit state into the gate. Safe
    // to call from any thread. Implementations should clear any pending
    // local debit tracking since the new snapshot already reflects the
    // server's truth.
    void UpdateLimits(string modelId, RateLimitSnapshot snapshot);

    // Observed snapshots, keyed by model id — used by the harness to write
    // the rate-limit profile that was in effect into run-meta.json.
    IReadOnlyDictionary<string, RateLimitSnapshot> ObservedLimits { get; }
}

public interface IRateLimitLease : IDisposable
{
    void Settle(int actualTokens);
}

// Snapshot of OpenAI's `x-ratelimit-*` response headers for one (api-key, model)
// tuple. All fields are nullable because individual headers may be absent on
// any given response (some error paths omit the rate-limit block entirely).
// `ObservedAtUtc` anchors `ResetTokens` / `ResetRequests` to wall-clock time.
public sealed record RateLimitSnapshot(
    int?      LimitTokens,
    int?      RemainingTokens,
    TimeSpan? ResetTokens,
    int?      LimitRequests,
    int?      RemainingRequests,
    TimeSpan? ResetRequests,
    DateTime  ObservedAtUtc);

// No-op implementation. Registered by default in the main app, which makes
// the gate path a pure pass-through for production (where requests are
// driven by a single interactive user and TPM contention is not a concern).
public sealed class NullRateLimitGate : IRateLimitGate
{
    public static readonly NullRateLimitGate Instance = new();

    public IReadOnlyDictionary<string, RateLimitSnapshot> ObservedLimits { get; }
        = new Dictionary<string, RateLimitSnapshot>();

    public Task<IRateLimitLease> AcquireAsync(string modelId, int estimatedTokens, CancellationToken ct = default)
        => Task.FromResult<IRateLimitLease>(NullLease.Instance);

    public void UpdateLimits(string modelId, RateLimitSnapshot snapshot) { /* no-op */ }

    private sealed class NullLease : IRateLimitLease
    {
        public static readonly NullLease Instance = new();
        public void Settle(int actualTokens) { /* no-op */ }
        public void Dispose() { /* no-op */ }
    }
}
