using System.Collections.Concurrent;
using Simply.JobApplication.Services.AI;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Probe-and-release rate-limit gate. Self-calibrating from OpenAI's
// `x-ratelimit-*` response headers; no static configuration.
//
// Three modes, per model:
//
//   ColdStart  No snapshot yet. Single-flight: serialize callers through a
//              FIFO queue and release one at a time. The first response that
//              lands populates a snapshot and switches the gate into one of
//              the other two modes.
//
//   Normal     Latest snapshot shows comfortable remaining capacity. The
//              queue drains greedily — release as many waiters as the
//              snapshot supports, accounting for the calls already in
//              flight (which the server hasn't seen yet).
//
//   Recovery   Latest snapshot shows the bucket is exhausted (or close to
//              it), or we just observed a 429. Single-flight again: at most
//              one in-flight call. The next probe is gated by the server's
//              own `reset_tokens` / `reset_requests` clock — we don't
//              re-fire until that time has elapsed. When the probe lands,
//              its fresh snapshot transitions us back to Normal (and the
//              queue drains) or keeps us in Recovery (and we wait for the
//              next reset).
//
// Transitions live in UpdateMode and only run inside state.Lock, so any
// caller (AcquireAsync enqueue, UpdateLimits, lease Dispose, the deferred
// probe-release timer) drives the state machine through the same path.
internal sealed class HeaderDrivenRateLimitGate : IRateLimitGate
{
    // Below this many remaining tokens, we treat the bucket as critical and
    // drop into Recovery. Sized to leave headroom for one comfortable call —
    // a single large prompt + generous output budget fits under this.
    private const int HealthyTokenMargin = 8_000;

    // Heuristic average per-call token cost used when projecting how many
    // queued waiters can be released in Normal mode against a known
    // `remainingTokens`. Estimate ≈ ~4500 input + ~1500 output for Q&A
    // calls. Erring slightly high here is fine: it under-releases, which is
    // self-correcting (the next response refreshes the snapshot and the
    // queue drains further).
    private const int AvgCallTokens = 6_000;

    // Pad applied to server-supplied reset times so the next probe lands
    // just past the API's projected refill point, not exactly at it.
    private static readonly TimeSpan ResetSafetyPad = TimeSpan.FromMilliseconds(250);

    private readonly ConcurrentDictionary<string, ModelState> _state =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RateLimitSnapshot> ObservedLimits
    {
        get
        {
            var d = new Dictionary<string, RateLimitSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var (model, state) in _state)
            {
                lock (state.Lock)
                    if (state.LastSnapshot is { } s) d[model] = s;
            }
            return d;
        }
    }

    public Task<IRateLimitLease> AcquireAsync(string modelId, int estimatedTokens, CancellationToken ct = default)
    {
        if (estimatedTokens <= 0) estimatedTokens = 1;
        var state  = _state.GetOrAdd(modelId, _ => new ModelState());
        var waiter = new Waiter();

        lock (state.Lock)
        {
            state.Queue.Enqueue(waiter);
            TryDrainQueue(state);
        }

        return AwaitWaiterAsync(state, waiter, estimatedTokens, ct);
    }

    private async Task<IRateLimitLease> AwaitWaiterAsync(
        ModelState state, Waiter waiter, int estimatedTokens, CancellationToken ct)
    {
        // The cancellation callback may run on any thread. It marks the
        // waiter and re-drains the queue so the next non-cancelled head
        // can be considered.
        using var reg = ct.Register(() =>
        {
            waiter.Cancel();
            lock (state.Lock) TryDrainQueue(state);
        });

        await waiter.Released.ConfigureAwait(false);
        return new Lease(this, state, estimatedTokens);
    }

    public void UpdateLimits(string modelId, RateLimitSnapshot snapshot)
    {
        var state = _state.GetOrAdd(modelId, _ => new ModelState());
        lock (state.Lock)
        {
            if (snapshot.LimitTokens       is { } lt) state.LimitTokens       = lt;
            if (snapshot.RemainingTokens   is { } rt) state.RemainingTokens   = rt;
            if (snapshot.LimitRequests     is { } lr) state.LimitRequests     = lr;
            if (snapshot.RemainingRequests is { } rr) state.RemainingRequests = rr;

            if (snapshot.ResetTokens   is { } rtT)
                state.ResetTokensAt   = DateTime.UtcNow + rtT + ResetSafetyPad;
            if (snapshot.ResetRequests is { } rqT)
                state.ResetRequestsAt = DateTime.UtcNow + rqT + ResetSafetyPad;

            state.LastSnapshot = snapshot;
            state.HasSnapshot  = true;

            UpdateMode(state);
            TryDrainQueue(state);
        }
    }

    // Called by Lease.Dispose. Decrements InFlight and tries to release more
    // waiters — in Recovery this becomes the trigger for the next probe.
    private void OnLeaseDisposed(ModelState state)
    {
        lock (state.Lock)
        {
            state.InFlight--;
            if (state.InFlight < 0) state.InFlight = 0;
            TryDrainQueue(state);
        }
    }

    private static void UpdateMode(ModelState state)
    {
        if (!state.HasSnapshot)
        {
            state.Mode = Mode.ColdStart;
            return;
        }
        // A limit value of 0 means "unknown" — treat as no constraint on
        // that axis, since the server didn't tell us.
        var tokensHealthy   = state.LimitTokens   <= 0 || state.RemainingTokens   >= HealthyTokenMargin;
        var requestsHealthy = state.LimitRequests <= 0 || state.RemainingRequests >= 1;
        state.Mode = (tokensHealthy && requestsHealthy) ? Mode.Normal : Mode.Recovery;
    }

    private void TryDrainQueue(ModelState state)
    {
        // Skip cancelled waiters at the head — they no longer represent a
        // session that wants admission. Mid-queue cancellations are dealt
        // with by ReleaseOne, which also skips them.
        while (state.Queue.Count > 0 && state.Queue.Peek().IsCancelled)
            state.Queue.Dequeue();

        switch (state.Mode)
        {
            case Mode.ColdStart:
                // Single-flight: one in flight until the first snapshot.
                if (state.InFlight == 0 && state.Queue.Count > 0)
                    ReleaseOne(state);
                break;

            case Mode.Normal:
                ReleaseUpToCapacity(state);
                break;

            case Mode.Recovery:
                TryReleaseInRecovery(state);
                break;
        }
    }

    // Release waiters greedily up to the capacity implied by the latest
    // snapshot, deducting an estimated cost for each call already in flight
    // (since the server's `remaining` was sampled before those calls were
    // dispatched).
    private void ReleaseUpToCapacity(ModelState state)
    {
        while (state.Queue.Count > 0)
        {
            var tokenSlots   = state.LimitTokens   <= 0
                ? int.MaxValue
                : (state.RemainingTokens - state.InFlight * AvgCallTokens) / AvgCallTokens;
            var requestSlots = state.LimitRequests <= 0
                ? int.MaxValue
                : state.RemainingRequests - state.InFlight;
            var capacity = Math.Min(tokenSlots, requestSlots);
            if (capacity <= 0) break;
            if (!ReleaseOne(state)) break;
        }
    }

    // In Recovery: at most one in-flight call, gated by the later of the two
    // reset times. If both have elapsed, fire the probe immediately. Else
    // schedule a deferred drain via Task.Delay (deduplicated by ProbeTimerScheduled).
    private void TryReleaseInRecovery(ModelState state)
    {
        if (state.InFlight > 0 || state.Queue.Count == 0) return;

        var probeAt = state.ResetTokensAt > state.ResetRequestsAt
            ? state.ResetTokensAt
            : state.ResetRequestsAt;
        // If we somehow have no reset info but are in Recovery (e.g. a 429
        // with no x-ratelimit-reset-* headers, defensive), probe right away.
        if (state.ResetTokensAt == default && state.ResetRequestsAt == default)
            probeAt = DateTime.UtcNow;

        var now = DateTime.UtcNow;
        if (now >= probeAt)
        {
            ReleaseOne(state);
            return;
        }

        if (state.ProbeTimerScheduled) return;
        state.ProbeTimerScheduled = true;
        var delay = probeAt - now;
        _ = Task.Delay(delay).ContinueWith(_ =>
        {
            lock (state.Lock)
            {
                state.ProbeTimerScheduled = false;
                TryDrainQueue(state);
            }
        }, TaskScheduler.Default);
    }

    // Returns true if a waiter was released. Skips cancelled entries.
    // Assumes state.Lock is held — callers are inside the state machine.
    private static bool ReleaseOne(ModelState state)
    {
        while (state.Queue.Count > 0)
        {
            var w = state.Queue.Dequeue();
            if (w.IsCancelled) continue;
            state.InFlight++;
            w.Release();
            return true;
        }
        return false;
    }

    private enum Mode { ColdStart, Normal, Recovery }

    private sealed class ModelState
    {
        public readonly object        Lock  = new();
        public          Mode          Mode  = Mode.ColdStart;
        public readonly Queue<Waiter> Queue = new();
        public          int           InFlight;
        public          int           LimitTokens;
        public          int           RemainingTokens;
        public          int           LimitRequests;
        public          int           RemainingRequests;
        public          DateTime      ResetTokensAt;     // default = no info
        public          DateTime      ResetRequestsAt;   // default = no info
        public          RateLimitSnapshot? LastSnapshot;
        public          bool          HasSnapshot;
        public          bool          ProbeTimerScheduled;
    }

    private sealed class Waiter
    {
        private readonly TaskCompletionSource<bool> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _cancelled;

        public bool IsCancelled => Volatile.Read(ref _cancelled) != 0;
        public Task Released    => _tcs.Task;
        public void Release()   => _tcs.TrySetResult(true);
        public void Cancel()
        {
            // Atomic latch — Release/Cancel race resolves by whichever
            // TrySetX wins on the underlying TCS. Setting _cancelled before
            // calling TrySetCanceled ensures ReleaseOne's IsCancelled check
            // sees the cancellation if it inspects this waiter later (e.g.
            // during a subsequent drain pass).
            if (Interlocked.Exchange(ref _cancelled, 1) == 0)
                _tcs.TrySetCanceled();
        }
    }

    private sealed class Lease : IRateLimitLease
    {
        private readonly HeaderDrivenRateLimitGate _gate;
        private readonly ModelState                _state;
        private readonly int                       _estimated;
        private          bool                      _disposed;

        public Lease(HeaderDrivenRateLimitGate gate, ModelState state, int estimated)
        {
            _gate      = gate;
            _state     = state;
            _estimated = estimated;
        }

        public void Settle(int actualTokens)
        {
            // Reconciliation against actuals happens via UpdateLimits — the
            // server's `remaining` already reflects this call's cost by the
            // time we see its headers. Settle is kept on the interface for
            // future telemetry uses.
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gate.OnLeaseDisposed(_state);
        }
    }
}
