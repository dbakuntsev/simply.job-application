using Simply.JobApplication.Models;
using Simply.JobApplication.Services.AI;
using Simply.JobApplication.Services.AI.OpenAi;
using Simply.JobApplication.Tools.QnA.Harness;

// ────────────────────────────────────────────────────────────────────────────
// Q&A Harness
//
// Drives Services/AI/OpenAi/OpenAiProvider.AnswerQuestionAsync across a matrix
// of (fixture × question/strategy × tone × length). Writes per-session JSON,
// a flat index.json, and a strategy-grouped summary.md so a Claude Code agent
// can read the run directly.
//
// Usage:
//   dotnet run --project Tools/QnA.Harness [-- options]
//
// Subcommands:
//   summarize-history      Walk Tools/QnA.Harness/runs/*/ and emit a Markdown
//                          report of (commit, date, subject, failure rates, cost)
//                          partitioned by (model, stage1Model). See
//                          `summarize-history --help` for options.
//   analyze-run <id>       Apply the QualityRules library to every session in
//                          one run; emit quality.json + quality.md into the
//                          run directory and to stdout. Use "latest" or a
//                          timestamp like 20260515-202123 as <id>. See
//                          `analyze-run --help` for options.
//   compare-runs <a> <b>   Diff the quality analyses of two runs; emit a
//                          rule-delta table and a session-classification
//                          report (improved / regressed / mixed / identical)
//                          into Tools/QnA.Harness/comparisons/ and stdout.
//                          See `compare-runs --help` for options.
//
// Options (default subcommand — run the matrix):
//   --model <id>           OpenAI model id for Stage 2 (default: gpt-5.4).
//                          Also used for Stage 1 unless --stage1-model overrides.
//   --stage1-model <id>    Override the model used for Stage 1 (question
//                          classification + evidence extraction). Lets you
//                          run the expensive Stage 2 on a strong model while
//                          paying mini rates on Stage 1.
//   --output <dir>         Output root            (default: Tools/QnA.Harness/runs/<UTC-ts>)
//   --concurrency <N>      Parallel sessions      (default: 4)
//   --api-key <key>        Override OPENAI_API_KEY
//   --fixtures <a,b>       Fixture key filter     (default: all)
//   --strategies <a,b>     Strategy filter        (default: all 9)
//   --tones <a,b>          Tone filter            (default: Formal,Conversational,Concise)
//   --lengths <a,b>        Sentence-length filter (default: 1,2,3)
//   --dry-run              Print matrix and exit, no API calls
//   --allow-dirty          Allow runs with uncommitted changes / no git anchor
//   --help                 Show this help
//
// Rate limiting: the harness uses a process-wide gate that self-calibrates
// from OpenAI's `x-ratelimit-*` response headers. No configuration knob.
// First call per model goes through a cold-start serialization; subsequent
// calls run at full --concurrency within the observed remaining budget.
//
// Environment:
//   OPENAI_API_KEY         Required unless --api-key is passed. A .env file at
//                          the repo root is auto-loaded if present.
// ────────────────────────────────────────────────────────────────────────────

// Subcommand dispatch. Anything else falls through to Run (the matrix driver),
// which keeps every existing invocation unchanged.
if (args.Length > 0 && args[0] == "summarize-history")
{
    return await SummarizeHistory.RunAsync(args.Skip(1).ToArray(), FindRepoRoot());
}
if (args.Length > 0 && args[0] == "analyze-run")
{
    return await AnalyzeRun.RunAsync(args.Skip(1).ToArray(), FindRepoRoot());
}
if (args.Length > 0 && args[0] == "compare-runs")
{
    return await CompareRuns.RunAsync(args.Skip(1).ToArray(), FindRepoRoot());
}
return await Run(args);

static async Task<int> Run(string[] args)
{
    var opts = CliOptions.Parse(args);
    if (opts.ShowHelp)
    {
        PrintHelp();
        return 0;
    }

    // .env at repo root (two levels up from Tools/QnA.Harness). Silent if absent.
    var repoRoot = FindRepoRoot();
    if (repoRoot is not null) DotEnv.LoadIfPresent(Path.Combine(repoRoot, ".env"));

    // Anchor the run to a commit. Refuse to run on a dirty tree (or outside a
    // git repo) unless --allow-dirty is passed — uncommitted edits make the
    // run irreproducible from the SHA alone, which destroys the forensic
    // value of run-meta.json. --dry-run is exempt: it has no side effects.
    var gitInfo = repoRoot is null ? null : GitProbe.TryCapture(repoRoot);
    if (!opts.DryRun)
    {
        if (gitInfo is null && !opts.AllowDirty)
        {
            Console.Error.WriteLine("error: cannot determine git commit (not a repo, or git unavailable).");
            Console.Error.WriteLine("       Run results would have no commit anchor for later analysis.");
            Console.Error.WriteLine("       Pass --allow-dirty to override.");
            return 3;
        }
        if (gitInfo is { IsDirty: true } && !opts.AllowDirty)
        {
            Console.Error.WriteLine($"error: working tree is dirty ({gitInfo.DirtyFileCount} file(s) modified).");
            Console.Error.WriteLine("       Run results would not correspond to any committed state.");
            Console.Error.WriteLine("       Commit or stash your changes, or pass --allow-dirty to override.");
            // Show the first few files so the user can see what's blocking them.
            foreach (var f in gitInfo.DirtyFiles.Take(10)) Console.Error.WriteLine($"         {f}");
            if (gitInfo.DirtyFileCount > 10) Console.Error.WriteLine($"         …and {gitInfo.DirtyFileCount - 10} more");
            return 3;
        }
    }

    var apiKey = opts.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (!opts.DryRun && string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("error: OPENAI_API_KEY is not set (and --api-key was not passed).");
        Console.Error.WriteLine("       Put OPENAI_API_KEY=... in .env at the repo root, or export it.");
        return 2;
    }

    var fixtures   = SelectFixtures(opts.FixtureFilter);
    var strategies = SelectStrategies(opts.StrategyFilter);
    var tones      = SelectTones(opts.ToneFilter);
    var lengths    = SelectLengths(opts.LengthFilter);

    var specs = new List<SessionSpec>();
    foreach (var fx in fixtures)
    {
        var bank = QuestionBank.For(fx);
        foreach (var q in bank.Where(q => strategies.Contains(q.ExpectedStrategy)))
        foreach (var t in tones)
        foreach (var L in lengths)
            specs.Add(new SessionSpec(fx, q, t, L, QuestionLengthUnit.Sentences));
    }

    var runDir = opts.OutputDir ?? DefaultRunDir(repoRoot);
    Directory.CreateDirectory(runDir);

    Console.WriteLine($"Q&A Harness");
    if (gitInfo is { } gi)
    {
        var tag = gi.IsDirty ? $"DIRTY ({gi.DirtyFileCount} files)" : "clean";
        Console.WriteLine($"  commit:       {gi.ShortSha} on {gi.Branch} — {tag}");
    }
    else
    {
        Console.WriteLine($"  commit:       (none — git unavailable or not a repo)");
    }
    Console.WriteLine($"  model:        {opts.Model}{(opts.Stage1Model is null ? "" : $"  (Stage 2)")}");
    if (opts.Stage1Model is not null)
        Console.WriteLine($"  stage1 model: {opts.Stage1Model}");
    Console.WriteLine($"  fixtures:     {string.Join(", ", fixtures.Select(f => f.Key))}");
    Console.WriteLine($"  strategies:   {string.Join(", ", strategies)}");
    Console.WriteLine($"  tones:        {string.Join(", ", tones)}");
    Console.WriteLine($"  lengths:      {string.Join(", ", lengths)}");
    Console.WriteLine($"  sessions:     {specs.Count}");
    Console.WriteLine($"  concurrency:  {opts.Concurrency}");
    Console.WriteLine($"  rate limit:   header-driven (auto-calibrates from x-ratelimit-* headers)");
    Console.WriteLine($"  output:       {runDir}");
    Console.WriteLine();

    if (opts.DryRun)
    {
        foreach (var s in specs) Console.WriteLine($"  {s.SessionId}");
        return 0;
    }

    var startedUtc = DateTimeOffset.UtcNow;

    // Shared HttpClient — each session creates its own OpenAiProvider but shares
    // socket pool. Generous timeout because the Responses API is streamed and the
    // provider itself enforces per-attempt retries.
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

    // Shared rate-limit gate. Self-calibrates per model from OpenAI's
    // `x-ratelimit-*` response headers, fed by OpenAiProvider.CallResponsesApiAsync
    // after every successful or failed call.
    var rateLimitGate = new HeaderDrivenRateLimitGate();

    var sessions = new List<SessionResult>(specs.Count);
    var lockObj  = new object();
    var done = 0;

    using var concurrencyGate = new SemaphoreSlim(opts.Concurrency);
    var tasks = specs.Select(async spec =>
    {
        await concurrencyGate.WaitAsync();
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // onProgress fires from inside the provider; suppress per-call chatter
            // in concurrent mode but keep visible session start/end on stdout.
            var sr = await SessionRunner.RunAsync(spec, opts.Model, opts.Stage1Model, apiKey!, httpClient, rateLimitGate, onProgress: null);
            sw.Stop();

            lock (lockObj)
            {
                sessions.Add(sr);
                done++;
                var status = sr.Error is not null ? "ERR" : (sr.Result?.WasInsufficient == true ? "INSF" : "OK ");
                Console.WriteLine($"[{done,3}/{specs.Count}] {status} {spec.SessionId}  ({sw.Elapsed.TotalSeconds:0.0}s)");
            }

            OutputWriter.WriteSession(runDir, sr);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }).ToArray();

    await Task.WhenAll(tasks);

    var finishedUtc = DateTimeOffset.UtcNow;

    // Pricing for run-meta. Read from a temporary provider — IAiProvider exposes
    // this without us needing to mirror the rate table inside the harness.
    using var pricingHttp = new HttpClient();
    var pricingProvider = new OpenAiProvider(
        pricingHttp,
        new WasmEnvironmentStub(),
        new CapturingLogger(),
        new HarnessUsageRecorder());
    var pricing       = pricingProvider.GetPricing(opts.Model);
    var stage1Pricing = opts.Stage1Model is null || opts.Stage1Model == opts.Model
        ? null
        : pricingProvider.GetPricing(opts.Stage1Model);

    var usageTotals = BuildRunUsageTotals(sessions);

    var meta = new RunMeta(
        StartedUtc:    startedUtc,
        FinishedUtc:   finishedUtc,
        Model:         opts.Model,
        Stage1Model:   opts.Stage1Model is null || opts.Stage1Model == opts.Model ? null : opts.Stage1Model,
        CommandLine:   string.Join(' ', args),
        Concurrency:   opts.Concurrency,
        SessionCount:  sessions.Count,
        Pricing:       pricing,
        Stage1Pricing: stage1Pricing,
        UsageTotals:        usageTotals,
        Git:                gitInfo,
        ObservedRateLimits: rateLimitGate.ObservedLimits);

    OutputWriter.WriteRunMeta(runDir, meta);
    OutputWriter.WriteIndex(runDir, sessions);
    OutputWriter.WriteSummary(runDir, meta, sessions);

    var errors = sessions.Count(s => s.Error is not null);
    var mismatches = sessions.Count(s => s.Result is { } r && !r.StrategyMatchedExpected && s.Error is null);
    Console.WriteLine();
    Console.WriteLine($"Done. {sessions.Count} sessions in {(finishedUtc - startedUtc).TotalSeconds:0.0}s. Errors: {errors}. Strategy mismatches: {mismatches}.");
    if (usageTotals is not null)
    {
        var modelLabel = opts.Stage1Model is null || opts.Stage1Model == opts.Model
            ? opts.Model
            : $"Stage 1: {opts.Stage1Model}, Stage 2: {opts.Model}";
        Console.WriteLine($"Usage: {usageTotals.TotalTokens:N0} total tokens · ${usageTotals.CostUsd:0.0000} USD ({modelLabel}).");
    }
    Console.WriteLine($"Index:   {Path.Combine(runDir, "index.json")}");
    Console.WriteLine($"Summary: {Path.Combine(runDir, "summary.md")}");

    return errors > 0 ? 1 : 0;
}

static IReadOnlyList<Fixture> SelectFixtures(string[]? filter)
{
    if (filter is null || filter.Length == 0) return Fixtures.All;
    var picked = new List<Fixture>();
    foreach (var key in filter)
    {
        var f = Fixtures.FindByKey(key) ?? throw new ArgumentException(
            $"Unknown fixture '{key}'. Available: {string.Join(", ", Fixtures.All.Select(x => x.Key))}");
        picked.Add(f);
    }
    return picked;
}

static HashSet<string> SelectStrategies(string[]? filter)
{
    if (filter is null || filter.Length == 0) return new HashSet<string>(Strategies.All);
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var s in filter)
    {
        var match = Strategies.All.FirstOrDefault(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"Unknown strategy '{s}'. Available: {string.Join(", ", Strategies.All)}");
        set.Add(match);
    }
    return set;
}

static IReadOnlyList<QuestionTone> SelectTones(string[]? filter)
{
    var all = new[] { QuestionTone.Formal, QuestionTone.Conversational, QuestionTone.Concise };
    if (filter is null || filter.Length == 0) return all;
    return filter
        .Select(s => Enum.TryParse<QuestionTone>(s, ignoreCase: true, out var t)
            ? t
            : throw new ArgumentException($"Unknown tone '{s}'. Available: {string.Join(", ", all)}"))
        .ToList();
}

static IReadOnlyList<int> SelectLengths(string[]? filter)
{
    var defaults = new[] { 1, 2, 3 };
    if (filter is null || filter.Length == 0) return defaults;
    return filter
        .Select(s => int.TryParse(s, out var n) && n >= 1
            ? n
            : throw new ArgumentException($"Invalid length '{s}'. Use positive integers."))
        .ToList();
}

static string? FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Simply.JobApplication.slnx")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}

// Aggregates UsageRecords across all sessions in a run: a grand total plus a
// per-step breakdown keyed by the step labels emitted from OpenAiProvider
// (e.g. "qa-stage1", "qa-stage2"). Returns null when no usage was recorded.
static RunUsageTotals? BuildRunUsageTotals(IReadOnlyList<SessionResult> sessions)
{
    var all = sessions
        .SelectMany(s => s.AllUsage ?? Array.Empty<UsageRecord>())
        .ToList();
    if (all.Count == 0) return null;

    var byStep = all
        .GroupBy(u => u.Step)
        .ToDictionary(
            g => g.Key,
            g => new RunStepTotals(
                Calls:             g.Count(),
                InputTokens:       g.Sum(u => u.InputTokens),
                CachedInputTokens: g.Sum(u => u.CachedInputTokens),
                OutputTokens:      g.Sum(u => u.OutputTokens),
                ReasoningTokens:   g.Sum(u => u.ReasoningTokens),
                TotalTokens:       g.Sum(u => u.TotalTokens),
                CostUsd:           g.Sum(u => u.CostUsd)));

    return new RunUsageTotals(
        InputTokens:       all.Sum(u => u.InputTokens),
        CachedInputTokens: all.Sum(u => u.CachedInputTokens),
        OutputTokens:      all.Sum(u => u.OutputTokens),
        ReasoningTokens:   all.Sum(u => u.ReasoningTokens),
        TotalTokens:       all.Sum(u => u.TotalTokens),
        CostUsd:           all.Sum(u => u.CostUsd),
        ByStep:            byStep);
}

static string DefaultRunDir(string? repoRoot)
{
    var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
    var root  = repoRoot is null
        ? Path.Combine(AppContext.BaseDirectory, "runs")
        : Path.Combine(repoRoot, "Tools", "QnA.Harness", "runs");
    return Path.Combine(root, stamp);
}

static void PrintHelp()
{
    Console.WriteLine("""
        Q&A Harness — drives OpenAiProvider.AnswerQuestionAsync across a matrix.

        Usage:
          dotnet run --project Tools/QnA.Harness -- [options]

        Options:
          --model <id>          OpenAI model id for Stage 2 (default: gpt-5.4).
                                Also used for Stage 1 unless --stage1-model is set.
          --stage1-model <id>   Override the model used for Stage 1 only
                                (question classification + evidence extraction).
          --output <dir>        Output root (default: Tools/QnA.Harness/runs/<UTC-ts>)
          --concurrency <N>     Parallel sessions (default: 4)
          --api-key <key>       Override OPENAI_API_KEY
          --fixtures <a,b>      Fixture filter: software, events
          --strategies <a,b>    Strategy filter (9 values; see Strategies.cs)
          --tones <a,b>         Formal,Conversational,Concise
          --lengths <a,b>       Positive integers (default: 1,2,3)
          --dry-run             Print matrix only (no API calls, no git gate)
          --allow-dirty         Allow running with uncommitted changes / outside a git repo
                                (run-meta.json will still record gitInfo when available)
          --help                Show this help

        Rate limiting is automatic — the harness reads OpenAI's `x-ratelimit-*`
        response headers and self-calibrates per model. No configuration knob.

        Default matrix size: 2 fixtures × 9 strategies × 3 tones × 3 lengths = 162 sessions
        (each session ≈ 2 OpenAI requests).
        """);
}

// ── CLI parsing ─────────────────────────────────────────────────────────────

internal sealed class CliOptions
{
    public string Model        { get; set; } = "gpt-5.4";
    public string? Stage1Model { get; set; }
    public string? OutputDir   { get; set; }
    public int Concurrency     { get; set; } = 4;
    public string? ApiKey      { get; set; }
    public string[]? FixtureFilter   { get; set; }
    public string[]? StrategyFilter  { get; set; }
    public string[]? ToneFilter      { get; set; }
    public string[]? LengthFilter    { get; set; }
    public bool DryRun         { get; set; }
    public bool AllowDirty     { get; set; }
    public bool ShowHelp       { get; set; }

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--help":
                case "-h":
                    o.ShowHelp = true; return o;
                case "--dry-run":
                    o.DryRun = true; break;
                case "--allow-dirty":
                    o.AllowDirty = true; break;
                case "--model":         o.Model       = RequireValue(args, ref i, a); break;
                case "--stage1-model":  o.Stage1Model = RequireValue(args, ref i, a); break;
                case "--output":        o.OutputDir   = RequireValue(args, ref i, a); break;
                case "--concurrency":   o.Concurrency = int.Parse(RequireValue(args, ref i, a)); break;
                case "--api-key":       o.ApiKey      = RequireValue(args, ref i, a); break;
                case "--fixtures":      o.FixtureFilter  = SplitCsv(RequireValue(args, ref i, a)); break;
                case "--strategies":    o.StrategyFilter = SplitCsv(RequireValue(args, ref i, a)); break;
                case "--tones":         o.ToneFilter     = SplitCsv(RequireValue(args, ref i, a)); break;
                case "--lengths":       o.LengthFilter   = SplitCsv(RequireValue(args, ref i, a)); break;
                default:
                    throw new ArgumentException($"Unknown option: {a}. Use --help.");
            }
        }
        if (o.Concurrency < 1) o.Concurrency = 1;
        return o;
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"{flag} requires a value");
        return args[++i];
    }

    private static string[] SplitCsv(string s)
        => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
