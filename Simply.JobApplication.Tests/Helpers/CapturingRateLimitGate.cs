using Simply.JobApplication.Services.AI;

namespace Simply.JobApplication.Tests.Helpers;

// Test gate for verifying IRateLimitGate integration on OpenAiProvider.
// Records every Acquire / UpdateLimits / lease-Settle call so tests can
// inspect the order and arguments. Admits every request unconditionally
// (we're testing wiring, not throttle behavior).
internal sealed class CapturingRateLimitGate : IRateLimitGate
{
    private readonly List<RateLimitSnapshot> _observedAll = new();
    private readonly Dictionary<string, RateLimitSnapshot> _observedLatest =
        new(StringComparer.OrdinalIgnoreCase);

    public List<(string ModelId, int EstimatedTokens)>          Acquires { get; } = new();
    public List<(string ModelId, RateLimitSnapshot Snapshot)>   Updates  { get; } = new();
    public List<int>                                            Settles  { get; } = new();

    public IReadOnlyDictionary<string, RateLimitSnapshot> ObservedLimits => _observedLatest;
    public IReadOnlyList<RateLimitSnapshot>               AllSnapshots   => _observedAll;

    public Task<IRateLimitLease> AcquireAsync(string modelId, int estimatedTokens, CancellationToken ct = default)
    {
        Acquires.Add((modelId, estimatedTokens));
        return Task.FromResult<IRateLimitLease>(new Lease(this));
    }

    public void UpdateLimits(string modelId, RateLimitSnapshot snapshot)
    {
        Updates.Add((modelId, snapshot));
        _observedAll.Add(snapshot);
        _observedLatest[modelId] = snapshot;
    }

    private sealed class Lease : IRateLimitLease
    {
        private readonly CapturingRateLimitGate _gate;
        public Lease(CapturingRateLimitGate gate) => _gate = gate;
        public void Settle(int actualTokens) => _gate.Settles.Add(actualTokens);
        public void Dispose() { }
    }
}
