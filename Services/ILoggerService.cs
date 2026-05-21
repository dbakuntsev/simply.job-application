namespace Simply.JobApplication.Services
{
    public interface ILoggerService
    {
        Task WriteLog(params object[] message);
    }
}
