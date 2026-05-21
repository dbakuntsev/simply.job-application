using Simply.JobApplication.Services.AI;

namespace Simply.JobApplication.Tools.QnA.Harness;

// IUsageRecorder implementation owned by a single session. SessionRunner
// constructs a fresh instance for every session and passes it to
// OpenAiProvider, so concurrent sessions never share usage state. A simple
// lock is sufficient because AnswerQuestionAsync issues its Responses-API
// calls sequentially within one session (Stage 1, then Stage 2).
internal sealed class HarnessUsageRecorder : IUsageRecorder
{
    private readonly object _lock = new();
    private readonly List<UsageRecord> _records = new();

    public void Record(UsageRecord record)
    {
        lock (_lock) { _records.Add(record); }
    }

    public IReadOnlyList<UsageRecord> Snapshot()
    {
        lock (_lock) { return _records.ToArray(); }
    }
}
