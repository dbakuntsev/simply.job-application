using Microsoft.JSInterop;

namespace Simply.JobApplication.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly IJSRuntime _runtime;

        public LoggerService(IJSRuntime runtime)
        {
            _runtime = runtime;
        }

        public async Task WriteLog(params object[] message)
        {
            await _runtime.InvokeVoidAsync("console.log", message);
        }
    }
}
