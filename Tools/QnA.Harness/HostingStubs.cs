using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Simply.JobApplication.Services;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Satisfies OpenAiProvider's DI contract without bringing in the WASM runtime.
// Environment = "Development" so the provider enables the diagnostic _logger.WriteLog
// calls inside AnswerQuestionAsync (focus + selectedPriorities), which is what we
// capture below.
internal sealed class WasmEnvironmentStub : IWebAssemblyHostEnvironment
{
    public string Environment   { get; } = "Development";
    public string BaseAddress   { get; } = "https://localhost/";
}

// Logger that JSON-serializes each WriteLog payload and routes it into the
// currently-active capture scope. Scopes are AsyncLocal so multiple sessions
// can run concurrently without their Stage 1 diagnostics interleaving.
//
// OpenAiProvider currently calls WriteLog twice per AnswerQuestionAsync:
//   1. WriteLog(focus)              — the AnswerFocusResult (private type)
//   2. WriteLog(selectedPriorities) — List<AnswerRoleFitPriority>
// Both are private nested types, so we serialize them rather than reference them.
internal sealed class CapturingLogger : ILoggerService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly AsyncLocal<Scope?> _current = new();

    public Task WriteLog(params object[] message)
    {
        var scope = _current.Value;
        if (scope is null) return Task.CompletedTask;

        var elapsed = scope.Stopwatch.Elapsed;
        foreach (var item in message)
        {
            JsonNode? node;
            try
            {
                var json = JsonSerializer.Serialize(item, item?.GetType() ?? typeof(object), _json);
                node = JsonNode.Parse(json);
            }
            catch (Exception ex)
            {
                node = new JsonObject
                {
                    ["_serializationError"] = ex.Message,
                    ["_toString"] = item?.ToString() ?? "",
                };
            }
            scope.Items.Add(new CapturedItem(elapsed, node));
        }
        return Task.CompletedTask;
    }

    // Run `body` with a fresh capture scope. Returns body's result paired with
    // every JsonNode that was passed to WriteLog during the call, in order,
    // each tagged with the elapsed time from the start of the body.
    public static async Task<CapturedRun<T>> CaptureAsync<T>(Func<Task<T>> body)
    {
        var scope = new Scope();
        var prior = _current.Value;
        _current.Value = scope;
        try
        {
            scope.Stopwatch.Start();
            var result = await body();
            var total = scope.Stopwatch.Elapsed;
            scope.Stopwatch.Stop();
            return new CapturedRun<T>(result, scope.Items, total);
        }
        finally
        {
            _current.Value = prior;
        }
    }

    internal sealed record CapturedItem(TimeSpan Elapsed, JsonNode? Node);

    internal sealed record CapturedRun<T>(T Result, IReadOnlyList<CapturedItem> Items, TimeSpan Total);

    private sealed class Scope
    {
        public List<CapturedItem> Items { get; } = new();
        public System.Diagnostics.Stopwatch Stopwatch { get; } = new();
    }
}
