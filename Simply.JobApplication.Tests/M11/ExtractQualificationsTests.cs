using System.Net;
using System.Text.Json;
using Simply.JobApplication.Services.AI.OpenAi;

namespace Simply.JobApplication.Tests.M11;

// M11-1: OpenAiProvider.ExtractQualificationsAsync — unit tests.
public class ExtractQualificationsTests
{
    // ── HTTP handler helpers ──────────────────────────────────────────────────

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private sealed class ThrowingHttpHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(ex);
    }

    /// <summary>
    /// Builds an SSE response body that wraps the given JSON content in the
    /// OpenAI Responses API response.completed event format.
    /// </summary>
    private static string MakeSseBody(string contentJson)
    {
        var ev = new
        {
            type = "response.completed",
            response = new
            {
                id = "resp_test",
                output = new[]
                {
                    new
                    {
                        type    = "message",
                        content = new[] { new { text = contentJson } }
                    }
                }
            }
        };
        var json = JsonSerializer.Serialize(ev);
        return $"data: {json}\ndata: [DONE]\n";
    }

    private static OpenAiProvider MakeProvider(HttpMessageHandler handler)
        => new(new HttpClient(handler));

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractQualificationsAsync_WithValidRoleDescription_ReturnsRequiredAndPreferredLists()
    {
        var expected = new QualificationExtractionResult
        {
            Required  = new List<string> { "5+ years C#", "API design" },
            Preferred = new List<string> { "Azure certification" }
        };
        var innerJson = JsonSerializer.Serialize(expected,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(MakeSseBody(innerJson))
        });
        var provider = MakeProvider(handler);

        var result = await provider.ExtractQualificationsAsync(
            "We need a senior C# developer.", "gpt-5.4", "sk-test");

        Assert.NotEmpty(result.Required);
        Assert.Contains("5+ years C#", result.Required);
        Assert.Contains("Azure certification", result.Preferred);
    }

    [Fact]
    public async Task ExtractQualificationsAsync_ApiError_ThrowsException()
    {
        // 401 is not in RetryableHttpCodes → throws InvalidOperationException immediately
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Unauthorized\"}")
        });
        var provider = MakeProvider(handler);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            provider.ExtractQualificationsAsync("Role description.", "gpt-5.4", "sk-bad-key"));
    }

    [Fact]
    public async Task ExtractQualificationsAsync_NetworkError_ThrowsException()
    {
        // HttpRequestException is retryable; all 3 attempts fail → exception propagates
        var handler  = new ThrowingHttpHandler(new HttpRequestException("Connection refused"));
        var provider = MakeProvider(handler);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            provider.ExtractQualificationsAsync("Role description.", "gpt-5.4", "sk-test"));
    }

    [Fact]
    public async Task ExtractQualificationsAsync_MalformedResponse_ThrowsException()
    {
        // SSE response.completed event arrives, but content is not valid JSON
        var sseBody = MakeSseBody("not valid json at all");
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseBody)
        });
        var provider = MakeProvider(handler);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            provider.ExtractQualificationsAsync("Role description.", "gpt-5.4", "sk-test"));
    }

    [Fact]
    public async Task ExtractQualificationsAsync_InvokesOnProgressCallback()
    {
        var expected  = new QualificationExtractionResult
        {
            Required = new List<string> { "Python" }, Preferred = new List<string>()
        };
        var innerJson = JsonSerializer.Serialize(expected,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(MakeSseBody(innerJson))
        });
        var provider = MakeProvider(handler);

        var progressMessages = new List<string>();
        await provider.ExtractQualificationsAsync(
            "We need a Python dev.", "gpt-5.4", "sk-test",
            msg => progressMessages.Add(msg));

        Assert.NotEmpty(progressMessages);
        Assert.Contains(progressMessages, m => m.Contains("Extracting"));
    }
}
