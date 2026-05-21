using System.Net;
using System.Text.Json;
using Simply.JobApplication.Services.AI.OpenAi;
using Simply.JobApplication.Tests.Helpers;
using static Simply.JobApplication.Tests.Helpers.ProviderBuilder;

namespace Simply.JobApplication.Tests.QA;

// QA-1: OpenAiProvider.AnswerQuestionAsync unit tests.
public class AnswerQuestionTests
{
    private const string InsufficientAnswerDataResponse =
        "I cannot determine that from the provided resume and role information.";

    private sealed class QueueHttpHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> CapturedBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedBodies.Add(request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : "");

            return _responses.Dequeue();
        }
    }

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

    private static string MakeSseBody(string text)
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
                        content = new[] { new { text } }
                    }
                }
            }
        };
        return $"data: {JsonSerializer.Serialize(ev)}\ndata: [DONE]\n";
    }

    // Builds a Stage-1 focus JSON that matches the **current** AnswerFocusResult
    // schema (roleFitPriorities + answerPlan). Earlier iterations had a
    // `fitThemes` shape; that was replaced in the Fix C/D/J/K series. The
    // priority below is `supported: true` and named in `answerPlan.leadPriority`
    // so SelectRoleFitPriorities picks it and Stage 2 sees it.
    private static string MakeFocusJson(
        string strategy = "MotivationNarrative",
        bool requiresStrictAnswer = false,
        bool canAnswer = true,
        double confidence = 0.82,
        string roleNeed = "Reliable business application work",
        string resumeEvidence = "Built and maintained scalable systems.")
        => JsonSerializer.Serialize(new
        {
            strategy,
            requiresStrictAnswer,
            canAnswer,
            confidence,
            employerConcern = "Whether the applicant has practical experience building reliable systems.",
            roleFitPriorities = new[]
            {
                new
                {
                    priority       = "PrimaryResponsibilityMatch",
                    roleNeed,
                    resumeEvidence,
                    supported      = true,
                }
            },
            answerPlan = new
            {
                leadPriority      = "PrimaryResponsibilityMatch",
                secondaryPriority = "",
                optionalPriority  = "",
            },
            gapAcknowledgment      = "",
            boundaries             = Array.Empty<string>(),
            insufficientDataReason = "",
            questionComponents     = Array.Empty<string>(),
            unsupportedComponents  = Array.Empty<string>(),
            allComponentsRequired  = false,
            ignore                 = new[] { "Unrelated technology inventory" },
        });

    private static HttpResponseMessage OkSse(string text)
        => new(HttpStatusCode.OK) { Content = new StringContent(MakeSseBody(text)) };

    // SSE body that includes a `usage` block matching the Responses-API shape,
    // so OpenAiProvider's UsageRecorder hook can extract token counts.
    private static string MakeSseBodyWithUsage(
        string text, int inputTokens, int outputTokens, int cachedInput = 0, int reasoning = 0)
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
                        content = new[] { new { text } }
                    }
                },
                usage = new
                {
                    input_tokens          = inputTokens,
                    input_tokens_details  = new { cached_tokens = cachedInput },
                    output_tokens         = outputTokens,
                    output_tokens_details = new { reasoning_tokens = reasoning },
                    total_tokens          = inputTokens + outputTokens,
                }
            }
        };
        return $"data: {JsonSerializer.Serialize(ev)}\ndata: [DONE]\n";
    }

    private static HttpResponseMessage OkSseWithUsage(
        string text, int inputTokens, int outputTokens, int cachedInput = 0)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(MakeSseBodyWithUsage(text, inputTokens, outputTokens, cachedInput))
        };

    // Attaches OpenAI's standard `x-ratelimit-*` response headers to an
    // existing response, in the format the provider's header parser expects.
    private static HttpResponseMessage WithRateLimitHeaders(
        HttpResponseMessage response,
        int  limitTokens     = 200_000,
        int  remainingTokens = 198_000,
        string resetTokens   = "6s",
        int  limitRequests   = 500,
        int  remainingRequests = 499,
        string resetRequests = "120ms")
    {
        response.Headers.TryAddWithoutValidation("x-ratelimit-limit-tokens",      limitTokens.ToString());
        response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-tokens",  remainingTokens.ToString());
        response.Headers.TryAddWithoutValidation("x-ratelimit-reset-tokens",      resetTokens);
        response.Headers.TryAddWithoutValidation("x-ratelimit-limit-requests",    limitRequests.ToString());
        response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-requests", remainingRequests.ToString());
        response.Headers.TryAddWithoutValidation("x-ratelimit-reset-requests",    resetRequests);
        return response;
    }

    private static HttpResponseMessage TooManyRequestsWithBodyHint(string hint = "100ms")
        => new(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                $"{{\"error\":{{\"type\":\"tokens\",\"code\":\"rate_limit_exceeded\"," +
                $"\"message\":\"Rate limit reached for gpt-5.4 on tokens per min (TPM). Please try again in {hint}.\"}}}}")
        };

    private static Task<string> CallAsync(
        OpenAiProvider provider,
        string questionText = "Why are you interested in this role?",
        int lengthValue = 3,
        QuestionLengthUnit lengthUnit = QuestionLengthUnit.Sentences,
        string? orgDescription = "We build enterprise software",
        Action<string>? onProgress = null)
        => provider.AnswerQuestionAsync(
            questionText:           questionText,
            tone:                   QuestionTone.Formal,
            lengthValue:            lengthValue,
            lengthUnit:             lengthUnit,
            orgName:                "Acme Corp",
            orgDescription:         orgDescription,
            roleName:               "Software Engineer",
            roleDescription:        "Build scalable systems.",
            tailoredResumeMarkdown: "# Resume\nSome content",
            modelId:                "gpt-5.4",
            apiKey:                 "sk-test",
            onProgress:             onProgress);

    [Fact]
    public async Task AnswerQuestionAsync_WithValidContext_ReturnsAnswerText()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("I am genuinely excited about this role."));

        var result = await CallAsync(MakeProvider(handler));

        Assert.Equal("I am genuinely excited about this role.", result);
    }

    [Fact]
    public async Task AnswerQuestionAsync_TrimsWhitespaceFromAnswer()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("  Answer with surrounding whitespace.  \n"));

        var result = await CallAsync(MakeProvider(handler));

        Assert.Equal("Answer with surrounding whitespace.", result);
    }

    [Fact]
    public async Task AnswerQuestionAsync_SentenceLength_CollapsesLineBreaks()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("First sentence.\nSecond sentence."));

        var result = await CallAsync(
            MakeProvider(handler),
            lengthValue: 2,
            lengthUnit: QuestionLengthUnit.Sentences);

        Assert.Equal("First sentence. Second sentence.", result);
    }

    [Fact]
    public async Task AnswerQuestionAsync_StageOneIncludesResumeButStageTwoUsesSelectedEvidenceOnly()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("Answer."));

        await CallAsync(MakeProvider(handler));

        Assert.Equal(2, handler.CapturedBodies.Count);
        Assert.Contains("# Resume\\nSome content", handler.CapturedBodies[0]);
        Assert.DoesNotContain("# Resume\\nSome content", handler.CapturedBodies[1]);
        Assert.Contains("Reliable business application work", handler.CapturedBodies[1]);
        Assert.Contains("Built and maintained scalable systems.", handler.CapturedBodies[1]);
    }

    // FitNarrative strategy dispatch — verifies the right strategy-specific
    // guidance reaches Stage 2 without pinning the exact wording (which
    // iterates frequently). Earlier versions of this test asserted on
    // specific phrases that were rewritten across the Fix C/D/E/J/K/L/M
    // series; that brittleness moved the assertions here to stable markers:
    //   - Stage 1 prompt names the strategy and the JSON field shape.
    //   - Stage 2 prompt dispatches on the strategy (the "Strategy: X."
    //     marker is the switch-statement output).
    //   - FitNarrative-specific guidance about not opening with a profession
    //     label and not turning the answer into a resume summary lives in
    //     the strategy block and has been stable across recent commits.
    //   - The contract that Stage 2 sees only SELECTED ROLE FIT PRIORITIES,
    //     not the resume, is asserted via the named blocks.
    [Fact]
    public async Task AnswerQuestionAsync_FitNarrativeStrategy_FlowsCorrectGuidanceIntoStageTwo()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson(strategy: "FitNarrative")),
            OkSse("My experience supporting healthcare systems makes me attentive to reliability."));

        await CallAsync(MakeProvider(handler));

        // Stage 1 names the strategy set and the structured-output shape.
        Assert.Contains("FitNarrative", handler.CapturedBodies[0]);
        Assert.Contains("MotivationNarrative", handler.CapturedBodies[0]);
        Assert.Contains("roleFitPriorities", handler.CapturedBodies[0]);
        Assert.Contains("answerPlan", handler.CapturedBodies[0]);

        // Stage 2 dispatches on the strategy returned by Stage 1.
        Assert.Contains("Strategy: FitNarrative.", handler.CapturedBodies[1]);

        // FitNarrative-specific guidance: don't open with profession labels,
        // don't make the answer a resume summary. The body is JSON, so the
        // apostrophe in "applicant's" is encoded as ' on the wire.
        Assert.Contains("Do not open with the applicant\\u0027s job title", handler.CapturedBodies[1]);
        Assert.Contains("Do not turn the answer into a resume summary", handler.CapturedBodies[1]);

        // Stage 2 receives the curated evidence block by name, not the resume.
        Assert.Contains("SELECTED ROLE FIT PRIORITIES", handler.CapturedBodies[1]);
        Assert.Contains("The full resume is intentionally not provided", handler.CapturedBodies[1]);
    }

    [Fact]
    public async Task AnswerQuestionAsync_SentenceLengthRequest_RequiresExactConciseSingleParagraphAnswer()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson(strategy: "FitNarrative")),
            OkSse("Answer."));

        await CallAsync(
            MakeProvider(handler),
            lengthValue: 2,
            lengthUnit: QuestionLengthUnit.Sentences);

        Assert.Contains("Write exactly 2 sentences", handler.CapturedBodies[1]);
        Assert.Contains("Use a single paragraph with no blank lines", handler.CapturedBodies[1]);
        Assert.Contains("Each sentence should be concise", handler.CapturedBodies[1]);
        Assert.Contains("Do not join multiple independent ideas", handler.CapturedBodies[1]);
        Assert.Contains("Follow the LENGTH instructions exactly", handler.CapturedBodies[1]);
        Assert.Contains("Do not use paragraph breaks unless LENGTH explicitly asks for paragraphs", handler.CapturedBodies[1]);
    }

    [Fact]
    public async Task AnswerQuestionAsync_StrictUnsupportedQuestion_ReturnsStandardInsufficientDataAnswer()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson(
                strategy: "EligibilityOrCompliance",
                requiresStrictAnswer: true,
                canAnswer: false,
                confidence: 0.2)));

        var result = await CallAsync(MakeProvider(handler));

        Assert.Equal(InsufficientAnswerDataResponse, result);
        Assert.Single(handler.CapturedBodies);
    }

    [Fact]
    public async Task AnswerQuestionAsync_OtherStrategy_ReturnsStandardInsufficientDataAnswer()
    {
        var handler = new QueueHttpHandler(OkSse(MakeFocusJson(strategy: "Other")));

        var result = await CallAsync(MakeProvider(handler));

        Assert.Equal(InsufficientAnswerDataResponse, result);
        Assert.Single(handler.CapturedBodies);
    }

    [Fact]
    public async Task AnswerQuestionAsync_InvalidStageOneJson_ThrowsException()
    {
        var handler = new QueueHttpHandler(OkSse("not json"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CallAsync(MakeProvider(handler)));
    }

    [Fact]
    public async Task AnswerQuestionAsync_ApiError_ThrowsException()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Unauthorized\"}")
        });

        await Assert.ThrowsAnyAsync<Exception>(() => CallAsync(MakeProvider(handler)));
    }

    [Fact]
    public async Task AnswerQuestionAsync_NetworkError_ThrowsAfterRetries()
    {
        var handler = new ThrowingHttpHandler(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAnyAsync<Exception>(() => CallAsync(MakeProvider(handler)));
    }

    [Fact]
    public async Task AnswerQuestionAsync_InvokesOnProgressCallback()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("Progress answer."));

        var messages = new List<string>();
        await CallAsync(MakeProvider(handler), onProgress: msg => messages.Add(msg));

        Assert.Contains("Selecting answer focus…", messages);
        Assert.Contains("Generating answer…", messages);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WithOrgDescription_IncludesDescriptionInRequest()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("Answer."));

        await CallAsync(MakeProvider(handler), orgDescription: "We build enterprise software");

        Assert.Contains("We build enterprise software", handler.CapturedBodies[0]);
        Assert.Contains("We build enterprise software", handler.CapturedBodies[1]);
    }

    [Fact]
    public async Task AnswerQuestionAsync_WithNullOrgDescription_OmitsDescriptionFromRequest()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("Answer."));

        await CallAsync(MakeProvider(handler), orgDescription: null);

        Assert.DoesNotContain("We build enterprise software", string.Join("\n", handler.CapturedBodies));
    }

    // ── Split-model routing (stage1ModelId parameter) ──────────────────

    [Fact]
    public async Task AnswerQuestionAsync_NoStage1Override_BothStagesUseSameModelId()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("Answer."));

        await CallAsync(MakeProvider(handler));

        // Both wire bodies should carry the same `"model"` value. The current
        // CallAsync passes modelId="gpt-5.4" with no stage1 override.
        Assert.Contains("\"model\":\"gpt-5.4\"", handler.CapturedBodies[0]);
        Assert.Contains("\"model\":\"gpt-5.4\"", handler.CapturedBodies[1]);
    }

    [Fact]
    public async Task AnswerQuestionAsync_Stage1ModelOverride_RoutesStagesIndependently()
    {
        var handler = new QueueHttpHandler(
            OkSse(MakeFocusJson()),
            OkSse("Answer."));

        // Direct call — CallAsync helper doesn't expose stage1ModelId.
        await MakeProvider(handler).AnswerQuestionAsync(
            questionText:           "Why are you interested in this role?",
            tone:                   QuestionTone.Formal,
            lengthValue:            3,
            lengthUnit:             QuestionLengthUnit.Sentences,
            orgName:                "Acme Corp",
            orgDescription:         "We build enterprise software",
            roleName:               "Software Engineer",
            roleDescription:        "Build scalable systems.",
            tailoredResumeMarkdown: "# Resume\nSome content",
            modelId:                "gpt-5.4",
            apiKey:                 "sk-test",
            stage1ModelId:          "gpt-5.4-mini");

        // Stage 1 uses the override; Stage 2 uses the primary modelId.
        Assert.Contains("\"model\":\"gpt-5.4-mini\"", handler.CapturedBodies[0]);
        Assert.DoesNotContain("\"model\":\"gpt-5.4\"", handler.CapturedBodies[0]);

        Assert.Contains("\"model\":\"gpt-5.4\"", handler.CapturedBodies[1]);
        Assert.DoesNotContain("\"model\":\"gpt-5.4-mini\"", handler.CapturedBodies[1]);
    }

    // ── IUsageRecorder integration ─────────────────────────────────────

    [Fact]
    public async Task AnswerQuestionAsync_RecordsUsageForBothStages_WithStepLabelsAndTokenCounts()
    {
        var recorder = new CapturingUsageRecorder();
        var handler = new QueueHttpHandler(
            OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000, outputTokens: 200),
            OkSseWithUsage("Answer.",       inputTokens:   500, outputTokens: 100));

        await CallAsync(MakeProvider(handler, recorder, null));

        Assert.Equal(2, recorder.Records.Count);

        var stage1 = recorder.Records[0];
        Assert.Equal("qa-stage1", stage1.Step);
        Assert.Equal("gpt-5.4",   stage1.Model);
        Assert.Equal(1_000,       stage1.InputTokens);
        Assert.Equal(200,         stage1.OutputTokens);
        Assert.Equal(1_200,       stage1.TotalTokens);
        Assert.True(stage1.CostUsd > 0m, "Stage 1 cost should be > 0 with non-zero tokens and known pricing");

        var stage2 = recorder.Records[1];
        Assert.Equal("qa-stage2", stage2.Step);
        Assert.Equal("gpt-5.4",   stage2.Model);
        Assert.Equal(500,         stage2.InputTokens);
        Assert.Equal(100,         stage2.OutputTokens);
        Assert.Equal(600,         stage2.TotalTokens);
        Assert.True(stage2.CostUsd > 0m);
    }

    [Fact]
    public async Task AnswerQuestionAsync_CostMatchesPricingTable()
    {
        var recorder = new CapturingUsageRecorder();
        var handler  = new QueueHttpHandler(
            OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000_000, outputTokens: 0),
            OkSseWithUsage("Answer.",       inputTokens:         0, outputTokens: 1_000_000));

        var provider = MakeProvider(handler, recorder, null);
        await CallAsync(provider);

        // Cost computed inside the provider against its _pricing table should
        // equal the published rate exactly when the call uses exactly 1M
        // input tokens on Stage 1 and 1M output tokens on Stage 2 — proves
        // the multiplication path and units (per-million).
        var pricing = provider.GetPricing("gpt-5.4")!;
        Assert.Equal(pricing.InputPerMillion,  recorder.Records[0].CostUsd);
        Assert.Equal(pricing.OutputPerMillion, recorder.Records[1].CostUsd);
    }

    [Fact]
    public async Task AnswerQuestionAsync_CachedInputTokens_BilledAtCachedRate()
    {
        var recorder = new CapturingUsageRecorder();
        var handler  = new QueueHttpHandler(
            // 1M input tokens total, all cached. Output zero, so cost = cached rate.
            OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000_000, outputTokens: 0, cachedInput: 1_000_000),
            OkSseWithUsage("Answer.",       inputTokens: 0,         outputTokens: 0));

        var provider = MakeProvider(handler, recorder, null);
        await CallAsync(provider);

        var pricing = provider.GetPricing("gpt-5.4")!;
        Assert.Equal(pricing.CachedInputPerMillion, recorder.Records[0].CostUsd);
    }

    // ── IRateLimitGate integration ─────────────────────────────────────

    [Fact]
    public async Task AnswerQuestionAsync_AcquiresGate_BeforeEachResponsesApiCall()
    {
        var gate    = new CapturingRateLimitGate();
        var handler = new QueueHttpHandler(
            OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000, outputTokens: 200),
            OkSseWithUsage("Answer.",       inputTokens:   500, outputTokens: 100));

        await CallAsync(MakeProvider(handler, null, gate));

        Assert.Equal(2, gate.Acquires.Count);
        Assert.All(gate.Acquires, a => Assert.Equal("gpt-5.4", a.ModelId));
        Assert.All(gate.Acquires, a => Assert.True(a.EstimatedTokens > 0,
            "estimated tokens passed to the gate should be positive"));
    }

    [Fact]
    public async Task AnswerQuestionAsync_ParsesRateLimitHeaders_AndPushesSnapshotToGate()
    {
        var gate    = new CapturingRateLimitGate();
        var handler = new QueueHttpHandler(
            WithRateLimitHeaders(
                OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000, outputTokens: 200),
                limitTokens: 200_000, remainingTokens: 195_000, resetTokens: "6s",
                limitRequests: 500,   remainingRequests: 499,   resetRequests: "120ms"),
            WithRateLimitHeaders(
                OkSseWithUsage("Answer.", inputTokens: 500, outputTokens: 100),
                limitTokens: 200_000, remainingTokens: 194_400, resetTokens: "5s",
                limitRequests: 500,   remainingRequests: 498,   resetRequests: "240ms"));

        await CallAsync(MakeProvider(handler, null, gate));

        Assert.Equal(2, gate.Updates.Count);

        var s1 = gate.Updates[0].Snapshot;
        Assert.Equal(200_000,                  s1.LimitTokens);
        Assert.Equal(195_000,                  s1.RemainingTokens);
        Assert.Equal(TimeSpan.FromSeconds(6),  s1.ResetTokens);
        Assert.Equal(500,                      s1.LimitRequests);
        Assert.Equal(499,                      s1.RemainingRequests);
        Assert.Equal(TimeSpan.FromMilliseconds(120), s1.ResetRequests);

        var s2 = gate.Updates[1].Snapshot;
        Assert.Equal(194_400,                  s2.RemainingTokens);
        Assert.Equal(TimeSpan.FromSeconds(5),  s2.ResetTokens);
        Assert.Equal(498,                      s2.RemainingRequests);
        Assert.Equal(TimeSpan.FromMilliseconds(240), s2.ResetRequests);
    }

    [Fact]
    public async Task AnswerQuestionAsync_LeaseSettled_WithActualTotalTokens()
    {
        var gate    = new CapturingRateLimitGate();
        var handler = new QueueHttpHandler(
            OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000, outputTokens: 200),
            OkSseWithUsage("Answer.",       inputTokens:   500, outputTokens: 100));

        await CallAsync(MakeProvider(handler, null, gate));

        // Settle is called once per Responses-API call with the actual
        // total_tokens from the usage block (1000+200 and 500+100).
        Assert.Equal(2, gate.Settles.Count);
        Assert.Contains(1_200, gate.Settles);
        Assert.Contains(600,   gate.Settles);
    }

    [Fact]
    public async Task AnswerQuestionAsync_RateLimitDurationHeader_ParsesCompoundFormats()
    {
        var gate    = new CapturingRateLimitGate();
        // Compound durations like "1m30s" appear in practice for low-tier
        // accounts on the requests axis. The parser must sum components.
        var handler = new QueueHttpHandler(
            WithRateLimitHeaders(
                OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000, outputTokens: 200),
                resetTokens: "1m30s", resetRequests: "2h"),
            WithRateLimitHeaders(
                OkSseWithUsage("Answer.", inputTokens: 500, outputTokens: 100)));

        await CallAsync(MakeProvider(handler, null, gate));

        var s1 = gate.Updates[0].Snapshot;
        Assert.Equal(TimeSpan.FromSeconds(90), s1.ResetTokens);
        Assert.Equal(TimeSpan.FromHours(2),    s1.ResetRequests);
    }

    // ── Retry policy ───────────────────────────────────────────────────

    [Fact]
    public async Task AnswerQuestionAsync_429ThenSuccess_RetriesAndReturnsAnswer()
    {
        // Stage 1 gets a 429 once with a tiny body hint, then succeeds.
        // Stage 2 then runs normally. Three HTTP requests total.
        var handler = new QueueHttpHandler(
            TooManyRequestsWithBodyHint("50ms"),
            OkSse(MakeFocusJson()),
            OkSse("Recovered answer."));

        var result = await CallAsync(MakeProvider(handler));

        Assert.Equal("Recovered answer.", result);
        Assert.Equal(3, handler.CapturedBodies.Count);
    }

    [Fact]
    public async Task AnswerQuestionAsync_NonRetryableStatus_DoesNotRetry()
    {
        // 401 is not in the retryable list (408, 429, 500, 502, 503, 524) —
        // it should fail immediately on the first attempt.
        var calls = 0;
        var handler = new StubHttpHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"unauthorized\"}")
            };
        });

        await Assert.ThrowsAnyAsync<Exception>(() => CallAsync(MakeProvider(handler)));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AnswerQuestionAsync_429_FeedsSnapshotToGateBeforeRetrying()
    {
        // A 429 carries `x-ratelimit-*` headers too. The gate must be
        // informed so its admission logic can drop into recovery before
        // the retry hits the next attempt.
        var gate    = new CapturingRateLimitGate();
        var handler = new QueueHttpHandler(
            WithRateLimitHeaders(
                TooManyRequestsWithBodyHint("50ms"),
                limitTokens: 200_000, remainingTokens: 0, resetTokens: "5s"),
            WithRateLimitHeaders(
                OkSseWithUsage(MakeFocusJson(), inputTokens: 1_000, outputTokens: 200),
                limitTokens: 200_000, remainingTokens: 190_000, resetTokens: "6s"),
            WithRateLimitHeaders(
                OkSseWithUsage("Answer.", inputTokens: 500, outputTokens: 100),
                limitTokens: 200_000, remainingTokens: 189_400, resetTokens: "5s"));

        await CallAsync(MakeProvider(handler, null, gate));

        // Three updates total — one per HTTP response, including the 429.
        Assert.Equal(3, gate.Updates.Count);
        Assert.Equal(0, gate.Updates[0].Snapshot.RemainingTokens);
        Assert.Equal(190_000, gate.Updates[1].Snapshot.RemainingTokens);
        Assert.Equal(189_400, gate.Updates[2].Snapshot.RemainingTokens);
    }
}
