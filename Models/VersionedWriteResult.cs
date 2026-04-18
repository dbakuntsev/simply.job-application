namespace Simply.JobApplication.Models;

public enum VersionedWriteResult
{
    Success,
    VersionMismatch,
    NotFound,
}
