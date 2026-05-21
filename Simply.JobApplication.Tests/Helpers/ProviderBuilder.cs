using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Simply.JobApplication.Services.AI;
using Simply.JobApplication.Services.AI.OpenAi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simply.JobApplication.Tests.Helpers
{
    internal static class ProviderBuilder
    {
        private class WebAssemblyHostEnvironmentStub : IWebAssemblyHostEnvironment
        {
            public static readonly WebAssemblyHostEnvironmentStub Instance = new();

            public string Environment { get; } = "Development";
            public string BaseAddress { get; } = "https://localhost/";
        }

        private class LoggerServiceStub : ILoggerService
        {
            public static readonly LoggerServiceStub Instance = new();

            public Task WriteLog(params object[] message)
            {
                return Task.CompletedTask;
            }
        }

        // Default factory used by most tests — both side-channels (usage,
        // rate limit) are no-ops, so tests that don't care about them see
        // bare provider behavior unchanged from previous fixtures.
        public static OpenAiProvider MakeProvider(HttpMessageHandler handler)
            => MakeProvider(handler, null, null);

        // Overload for tests that want to verify the usage / rate-limit
        // wiring on AnswerQuestionAsync. Pass a CapturingUsageRecorder or
        // CapturingRateLimitGate from Helpers/ and assert against its
        // captured calls after the provider runs.
        public static OpenAiProvider MakeProvider(
            HttpMessageHandler handler,
            IUsageRecorder?    usage,
            IRateLimitGate?    rateLimit)
            => new(
                new HttpClient(handler),
                WebAssemblyHostEnvironmentStub.Instance,
                LoggerServiceStub.Instance,
                usage     ?? NullUsageRecorder.Instance,
                rateLimit ?? NullRateLimitGate.Instance);
    }
}
