using Simply.JobApplication.Services.AI;

namespace Simply.JobApplication.Tests.Helpers;

// Test recorder for verifying IUsageRecorder integration on OpenAiProvider.
// Appends every Record() call to Records in arrival order so tests can assert
// "Stage 1 was recorded, then Stage 2 was recorded" by index.
internal sealed class CapturingUsageRecorder : IUsageRecorder
{
    public List<UsageRecord> Records { get; } = new();
    public void Record(UsageRecord record) => Records.Add(record);
}
