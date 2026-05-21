using System.Text.RegularExpressions;
using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services.QnA;

// The quality-rule library. Every rule corresponds to a concrete instruction
// in the Stage 2 prompt that bans a specific surface or structural pattern,
// or to a contract between Stage 1 and Stage 2 that Stage 2 may violate.
//
// Two consumers share this library:
//   - Runtime: Stage2RejectionSampler runs the rules after each Stage 2 call
//     and re-prompts with the violations as feedback (up to a retry budget).
//   - Offline: Tools/QnA.Harness analyze-run / compare-runs apply the same
//     rules to logged session JSON for prompt-iteration reporting.
//
// Adding a rule to the Stage 2 prompt without adding a detector here is the
// drift mode this library exists to prevent — the prompt rule is then only
// enforced by the model's own self-policing, with no external feedback when
// it slips. SourceFix on each rule is the audit trail back to the prompt
// instruction it mirrors.
//
// Detector style:
//   - ForbiddenSurface  → regex on stage2.answerText.
//   - ForbiddenStructural → custom function, sentence-aware.
//   - ContractAdherence → cross-checks stage2.answerText against stage1.focus
//     or the Stage 2 inputs (length, etc.).
//
// All detectors return RuleMatch values containing the matched text and a
// short surrounding context. The owning rule's Id is stamped on the match
// by the caller — keeping the detector closures stateless.
public enum RuleKind
{
    ForbiddenSurface,
    ForbiddenStructural,
    ContractAdherence,
}

public sealed record RuleMatch(string MatchedText, string Context);

public sealed record QualityRule(
    string                                  Id,
    string                                  SourceFix,
    RuleKind                                Kind,
    string                                  Description,
    Func<SessionView, IEnumerable<RuleMatch>> Detect);

// View of one session that the detectors operate on. The runtime sampler
// populates every field directly from the inputs to Stage 2; the harness
// builds it from logged session JSON. Length fields are nullable because
// pre-existing harness session logs predate the length-compliance rule —
// when null, that rule no-ops rather than reporting spurious failures.
public sealed record SessionView(
    string                SessionId,
    string                AnswerText,
    string                GapAcknowledgment,
    IReadOnlyList<string> Boundaries,
    IReadOnlyList<string> Ignore,
    int?                  ExpectedLengthValue   = null,
    QuestionLengthUnit?   ExpectedLengthUnit    = null,
    // Concatenated resumeEvidence strings from the selected role-fit priorities.
    // Drives the metric-strip rule: every numeric/scope token in evidence that
    // is absent from the answer is flagged as a paraphrased-away metric.
    // Null = the rule no-ops (harness logs from before this field existed).
    IReadOnlyList<string>? SelectedResumeEvidence = null);

public static class QualityRules
{
    public static IReadOnlyList<QualityRule> All { get; } = Build();

    public static int LibrarySize => All.Count;

    private static IReadOnlyList<QualityRule> Build() => new[]
    {
        // ── Forbidden surface phrases ────────────────────────────────────
        new QualityRule(
            Id:          "schema-leakage-supported",
            SourceFix:   "Fix D (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Schema-flag use of \"supported\": \"is a supported strength/match/...\", \"is supported by the resume\", \"this is a supported claim\". Action-verb usage (\"I supported the systems\", \"supporting clinical workflows\") is allowed — the rule used to ban all instances of the word and forced models to substitute weaker verbs.",
            Detect:      RegexDetector(
                @"\b(is|are|was|were)\s+(a\s+|an\s+)?supported\b" +
                @"|\bsupported\s+(strength|strengths|match|matches|priority|priorities|fit|fits|claim|claims|evidence|by\s+the\s+resume)\b" +
                @"|\bthis\s+is\s+supported\b",
                RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "additive-i-also",
            SourceFix:   "Fix L (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Additive construction \"I also\" or \"I have also\" anywhere in the answer (sentence opener or trailing clause).",
            // Case-sensitive on the leading I to reduce false positives on words
            // like "particular" / "official" / "filial" that contain "i also" as a substring.
            Detect:      RegexDetector(@"\bI (have )?also\b", RegexOptions.None)),

        new QualityRule(
            Id:          "additive-conjunction",
            SourceFix:   "Fix D (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Additive conjunctions: \"Additionally\", \"Furthermore\", \"Moreover\".",
            Detect:      RegexDetector(@"\b(Additionally|Furthermore|Moreover)\b", RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "evaluative-self-predicate",
            SourceFix:   "Fix D (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Self-evaluative predicate (\"is a strong/good/natural/clear match/fit/strength\", \"makes me well-suited/well-positioned\", \"aligns well with\", \"is highly relevant\").",
            Detect:      RegexDetector(
                @"\b((is|are|was|were|makes|positions)\s+(me\s+)?(a\s+|an\s+|well[- ])?(strong|good|natural|clear)\s+(match|fit|strength)" +
                @"|is\s+well[- ]suited" +
                @"|aligns\s+well\s+with" +
                @"|makes\s+me\s+well[- ]positioned" +
                @"|is\s+highly\s+relevant)\b",
                RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "relevance-bridge-finite",
            SourceFix:   "Fix C (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Relative clause explaining relevance to the role: \"which/that matches/fits/aligns/maps/supports/prepares/reflects/demonstrates/highlights/connects\".",
            Detect:      RegexDetector(
                @"\b(which|that)\s+(matches|fits|aligns|maps|supports|prepares|reflects|demonstrates|highlights|connects)\b",
                RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "relevance-bridge-participial",
            SourceFix:   "Fix C (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Participial relevance bridge that characterizes preceding evidence as proof of an abstract trait — \"preparing me to\", \"positioning me to\", \"strengthening how I\", \"making me well-suited\", \"showing a consistent focus\", \"demonstrating my commitment\", \"reflecting an ongoing pattern\".",
            Detect:      RegexDetector(
                @"\b(preparing|positioning)\s+me\s+to\b" +
                @"|\bstrengthening\s+how\s+(I|we)\b" +
                @"|\bmaking\s+me\s+well[- ]suited\b" +
                // Abstract-evaluative-noun shape. A participle of perception or
                // assertion, followed by a determiner / characterizing modifier,
                // followed by an evaluative noun. Catches "showing a consistent
                // focus on X" but not concrete uses like "showing the system to
                // the team" (which has neither the modifier nor the eval-noun).
                @"|\b(showing|demonstrating|indicating|reflecting|revealing|illustrating|signaling|signalling|highlighting)\s+" +
                @"(a|an|my|consistent|ongoing|sustained|strong|continued|deep|clear|long[- ]standing|broad)\s+" +
                @"(focus|commitment|emphasis|dedication|preference|interest|capability|ability|" +
                @"track\s+record|pattern|history|orientation|tendency|approach|alignment|fit|" +
                @"willingness|readiness)\b",
                RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "relevance-bridge-purpose",
            SourceFix:   "Fix C (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Infinitive purpose clause attached to evidence: \"to bring to this role\", \"to contribute here\", \"to deliver to your team\".",
            Detect:      RegexDetector(
                @"\bto\s+(bring|contribute|deliver|offer)\s+(to\s+)?(this\s+role|your\s+team|here)\b",
                RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "filler-application-letter",
            SourceFix:   "Fix E sub-bullet (Stage 2)",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Application-letter filler: \"I would bring/contribute\", \"I am eager/excited\", \"I look forward\", \"I welcome the opportunity\".",
            Detect:      RegexDetector(
                @"\bI\s+(would\s+(bring|contribute)|am\s+(eager|excited)|look\s+forward|welcome\s+the\s+opportunity)\b",
                RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "identity-label-opener",
            SourceFix:   "Stage 2 opening rule",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "First sentence opens with an identity label (\"I specialize in\", \"I am experienced in\", \"I am a/an <profession>\", \"My background/experience includes/is\") instead of a concrete first-person action.",
            Detect:      DetectIdentityLabelOpener),

        // ── Forbidden structural patterns ────────────────────────────────
        new QualityRule(
            Id:          "template-repetition-i-have",
            SourceFix:   "Fix E (Stage 2)",
            Kind:        RuleKind.ForbiddenStructural,
            Description: "Two consecutive sentences both opening with \"I have <word>\" — the template counts as repeated regardless of which verb is used.",
            Detect:      DetectIHaveTemplateRepetition),

        new QualityRule(
            Id:          "comma-list-of-three-or-more",
            SourceFix:   "Fix A (Stage 1) / Stage 2 sentence-shape rule",
            Kind:        RuleKind.ForbiddenStructural,
            Description: "Comma-separated list of three or more items (\"X, Y, Z\" or \"X, Y, Z, and W\").",
            Detect:      DetectCommaListOfThreeOrMore),

        new QualityRule(
            Id:          "multiple-metrics-per-sentence",
            SourceFix:   "Stage 2 'one metric per sentence' rule",
            Kind:        RuleKind.ForbiddenStructural,
            Description: "Sentence contains more than one metric, count, percentage, duration, or scope figure. Stage 2 must distribute multiple figures across separate sentences.",
            Detect:      DetectMultipleMetricsPerSentence),

        // ── Contract adherence ──────────────────────────────────────────
        new QualityRule(
            Id:          "boundary-leak",
            SourceFix:   "Stage 2 source-fidelity rule",
            Kind:        RuleKind.ContractAdherence,
            Description: "Answer mentions an item Stage 1 placed in focus.boundaries — Stage 2 was instructed not to surface these.",
            Detect:      v => DetectListLeak(v, v.Boundaries)),

        new QualityRule(
            Id:          "ignore-leak",
            SourceFix:   "Stage 2 source-fidelity rule",
            Kind:        RuleKind.ContractAdherence,
            Description: "Answer mentions an item Stage 1 placed in focus.ignore — Stage 2 was instructed to disregard these.",
            Detect:      v => DetectListLeak(v, v.Ignore)),

        new QualityRule(
            Id:          "gap-template-mismatch",
            SourceFix:   "Stage 2 gap-acknowledgment rule",
            Kind:        RuleKind.ContractAdherence,
            Description: "Stage 1 supplied a non-empty gapAcknowledgment but Stage 2's answer doesn't open with \"While I have not <gap>, I have <strength>\".",
            Detect:      DetectGapTemplateMismatch),

        new QualityRule(
            Id:          "gap-splice-malformed",
            SourceFix:   "Stage 1 gapAcknowledgment verb-phrase rule",
            Kind:        RuleKind.ContractAdherence,
            Description: "Stage 2 used the \"While I have not <gap>\" template, but the spliced gap is not a grammatical verb-phrase completion — starts with an uppercase noun (\"No\", \"Resume\"), a determiner/article (\"a\", \"the\", \"my\"), or contains a sentence-terminator before the comma. Splice failure caused by Stage 1 emitting a sentence or noun phrase where a past-participle verb phrase was required.",
            Detect:      DetectGapSpliceMalformed),

        new QualityRule(
            Id:          "length-compliance",
            SourceFix:   "Stage 2 LENGTH directive",
            Kind:        RuleKind.ContractAdherence,
            Description: "Answer's sentence count (sentence-mode) or paragraph count (paragraph-mode) does not match the requested LENGTH.",
            Detect:      DetectLengthCompliance),

        new QualityRule(
            Id:          "empty-back-reference",
            SourceFix:   "Stage 2 No empty back-references rule",
            Kind:        RuleKind.ForbiddenSurface,
            Description: "Trailing pronominal phrase that points back at the sentence's own context without adding information (\"in that setting\", \"in such roles\", \"on such systems\").",
            Detect:      RegexDetector(
                @"\b(in|on|from|across|during|for)\s+(that|those|such|this|these)\s+(setting|settings|role|roles|context|contexts|circumstance|circumstances|capacity|capacities|environment|environments|system|systems|area|areas|domain|domains|work)\b",
                RegexOptions.IgnoreCase)),

        new QualityRule(
            Id:          "metric-strip",
            SourceFix:   "Stage 2 Source completeness rule",
            Kind:        RuleKind.ContractAdherence,
            Description: "A numeric metric, percentage, duration, or scope figure present in the in-budget selected resumeEvidence is absent from the answer — Stage 2 paraphrased the figure away. Only the first N selected priorities are checked, where N is the LENGTH budget (one priority per sentence, two per paragraph) — Stage 2 is instructed to omit out-of-budget priorities, so missing metrics from those priorities are not stripped.",
            Detect:      DetectMetricStrip),
    };

    // ── Detector helpers ────────────────────────────────────────────────

    private static Func<SessionView, IEnumerable<RuleMatch>> RegexDetector(string pattern, RegexOptions options)
    {
        var rx = new Regex(pattern, options | RegexOptions.Compiled);
        return view =>
        {
            var hits = new List<RuleMatch>();
            foreach (Match m in rx.Matches(view.AnswerText))
            {
                hits.Add(new RuleMatch(m.Value, ExtractContext(view.AnswerText, m.Index, m.Length)));
            }
            return hits;
        };
    }

    // Opens with "I specialize in", "I am experienced in", "I am a <noun>",
    // "I am an <noun>", "My background includes/is", "My experience includes/is".
    // Limited to the first sentence — later sentences may use identity labels in
    // service of a concrete claim (the prompt rule targets the opening).
    private static readonly Regex _identityLabelOpenerRx = new(
        @"^\s*(I\s+(specialize\s+in|am\s+experienced\s+in|am\s+an?\s+\w+)" +
        @"|My\s+(background|experience)\s+(includes|is)\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static IEnumerable<RuleMatch> DetectIdentityLabelOpener(SessionView view)
    {
        if (IsInsufficientSentinel(view.AnswerText)) yield break;

        var first = SplitSentences(view.AnswerText).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first)) yield break;

        var m = _identityLabelOpenerRx.Match(first);
        if (m.Success)
            yield return new RuleMatch(m.Value.Trim(), Truncate(first, 120));
    }

    private static IEnumerable<RuleMatch> DetectIHaveTemplateRepetition(SessionView view)
    {
        // Approximate: any consecutive pair of sentences both starting with
        // "I have <word>". Over-broad on purpose — false positives are easier
        // for the reader to dismiss than false negatives are to discover.
        var sentences = SplitSentences(view.AnswerText).ToList();
        var startsWithIHave = sentences.Select(s =>
            Regex.IsMatch(s, @"^\s*I\s+have\s+\w+\b", RegexOptions.None)).ToList();

        for (var i = 0; i < sentences.Count - 1; i++)
        {
            if (startsWithIHave[i] && startsWithIHave[i + 1])
            {
                var a = Truncate(sentences[i], 100);
                var b = Truncate(sentences[i + 1], 100);
                yield return new RuleMatch($"{a} || {b}", $"sentence {i + 1}+{i + 2}: {a} | {b}");
            }
        }
    }

    private static readonly Regex _commaListRx = new(
        @"\b\w+(?:,\s+\w+){2,}(?:,?\s+and\s+\w+)?\b",
        RegexOptions.Compiled);

    private static IEnumerable<RuleMatch> DetectCommaListOfThreeOrMore(SessionView view)
    {
        foreach (Match m in _commaListRx.Matches(view.AnswerText))
        {
            yield return new RuleMatch(m.Value, ExtractContext(view.AnswerText, m.Index, m.Length));
        }
    }

    // Numeric figures that count as "metrics" for the per-sentence cap. Matches
    // the same shapes the metric-strip rule looks for in evidence: bare ints,
    // decimals, percentages, "70+" approximations, K/M/B suffixes. The cap is
    // intentionally lenient on single-digit isolated tokens (handled via the
    // length check) — those are noisy and rarely the kind of scope figure the
    // Stage 2 prompt is talking about.
    private static readonly Regex _sentenceMetricRx = new(
        @"\b\d+(?:[.,]\d+)?\+?(?:[KMB])?%?\b",
        RegexOptions.Compiled);

    private static IEnumerable<RuleMatch> DetectMultipleMetricsPerSentence(SessionView view)
    {
        if (IsInsufficientSentinel(view.AnswerText)) yield break;

        foreach (var sentence in SplitSentences(view.AnswerText))
        {
            var figures = new List<string>();
            foreach (Match m in _sentenceMetricRx.Matches(sentence))
            {
                var f = m.Value;
                // Skip single-character bare digits with no unit suffix —
                // numbers like "I have 1 X and 2 Y" trigger false positives.
                if (f.Length < 2 && !f.EndsWith('+') && !f.EndsWith('%')) continue;
                figures.Add(f);
            }
            if (figures.Count > 1)
            {
                yield return new RuleMatch(
                    string.Join(", ", figures),
                    $"sentence: \"{Truncate(sentence, 120)}\" carries {figures.Count} figures");
            }
        }
    }

    private static IEnumerable<RuleMatch> DetectListLeak(SessionView view, IReadOnlyList<string> items)
    {
        // Skip very short items (≤3 chars): they generate noise (matching "the",
        // "a", "of"). Real boundaries / ignore items are phrases, not tokens.
        foreach (var raw in items)
        {
            var item = raw?.Trim();
            if (string.IsNullOrEmpty(item) || item.Length < 4) continue;

            var idx = view.AnswerText.IndexOf(item, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                yield return new RuleMatch(item, ExtractContext(view.AnswerText, idx, item.Length));
            }
        }
    }

    // OpenAiProvider's InsufficientAnswerDataResponse constant. Sessions
    // ending with this sentinel had no real Stage 2 output — they were gated
    // by Stage 1 (strict-question with insufficient evidence) and the
    // sentinel was returned in lieu of generation. Contract rules that
    // assume an actual generated answer must not fire on these.
    private const string InsufficientSentinelPrefix =
        "I cannot determine that from the provided resume and role information";

    private static bool IsInsufficientSentinel(string answerText)
        => answerText.TrimStart().StartsWith(InsufficientSentinelPrefix, StringComparison.Ordinal);

    private static IEnumerable<RuleMatch> DetectGapTemplateMismatch(SessionView view)
    {
        if (string.IsNullOrWhiteSpace(view.GapAcknowledgment)) yield break;
        if (IsInsufficientSentinel(view.AnswerText))           yield break;

        var trimmed = view.AnswerText.TrimStart();
        if (Regex.IsMatch(trimmed, @"^While\s+I\s+have\s+not\b", RegexOptions.IgnoreCase))
            yield break;

        var preview = Truncate(trimmed, 80);
        yield return new RuleMatch(
            preview,
            $"gap=\"{Truncate(view.GapAcknowledgment, 60)}\"; opens: \"{preview}\"");
    }

    // Words that, when they appear as the first spliced token after "While I have
    // not ", indicate Stage 1 supplied a noun phrase or sentence rather than a
    // past-participle verb. "yet"/"ever" are deliberately excluded — qualifier
    // adverbs that precede a verb ("while I have not yet completed…") are fine.
    private static readonly HashSet<string> GapSpliceBadStarters = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "a", "an", "the", "my", "this", "that", "these", "those",
        "resume", "specific", "detailed", "direct",
    };

    private static IEnumerable<RuleMatch> DetectGapSpliceMalformed(SessionView view)
    {
        if (string.IsNullOrWhiteSpace(view.GapAcknowledgment)) yield break;
        if (IsInsufficientSentinel(view.AnswerText))           yield break;

        var trimmed = view.AnswerText.TrimStart();

        // The splice is the substring between "While I have not " and the next
        // comma; anything else means the gap template isn't in use and the
        // sibling rule (gap-template-mismatch) covers it.
        var spliceMatch = Regex.Match(
            trimmed,
            @"^While\s+I\s+have\s+not\s+([^,]+?),",
            RegexOptions.IgnoreCase);
        if (!spliceMatch.Success) yield break;

        var splice = spliceMatch.Groups[1].Value;
        var firstWord = Regex.Match(splice, @"^\S+").Value;
        if (firstWord.Length == 0) yield break;

        // Failure A — first spliced token is uppercase. Past participles are
        // lowercase, so an uppercase opener almost always means Stage 1 supplied
        // a sentence ("No specific…", "Resume supports…") rather than a verb.
        if (char.IsUpper(firstWord[0]))
        {
            yield return new RuleMatch(
                $"While I have not {Truncate(firstWord, 30)}…",
                $"gap=\"{Truncate(view.GapAcknowledgment, 60)}\"; uppercase splice opener: \"{firstWord}\"");
            yield break;
        }

        // Failure B — first spliced token is a determiner/noun-starter that
        // can't follow "have not" grammatically.
        if (GapSpliceBadStarters.Contains(firstWord.TrimEnd(',', '.', ';', ':')))
        {
            yield return new RuleMatch(
                $"While I have not {Truncate(splice, 50)},",
                $"gap=\"{Truncate(view.GapAcknowledgment, 60)}\"; determiner/noun splice opener: \"{firstWord}\"");
            yield break;
        }

        // Failure C — period (or other sentence terminator) inside the splice
        // before the comma. Indicates a full-sentence gapAcknowledgment.
        if (splice.IndexOfAny(new[] { '.', '!', '?' }) >= 0)
        {
            yield return new RuleMatch(
                $"While I have not {Truncate(splice, 50)},",
                $"gap=\"{Truncate(view.GapAcknowledgment, 60)}\"; sentence-terminator inside splice");
        }
    }

    private static IEnumerable<RuleMatch> DetectLengthCompliance(SessionView view)
    {
        if (view.ExpectedLengthValue is not int expected || view.ExpectedLengthUnit is not { } unit)
            yield break;
        if (IsInsufficientSentinel(view.AnswerText)) yield break;

        var trimmed = view.AnswerText.Trim();
        if (trimmed.Length == 0) yield break;

        int actual;
        string what;
        if (unit == QuestionLengthUnit.Sentences)
        {
            actual = SplitSentences(trimmed).Count();
            what   = "sentence";
        }
        else if (unit == QuestionLengthUnit.Paragraphs)
        {
            actual = Regex.Split(trimmed, @"\n\s*\n").Count(p => p.Trim().Length > 0);
            what   = "paragraph";
        }
        else
        {
            yield break;
        }

        if (actual == expected) yield break;

        yield return new RuleMatch(
            $"{actual}/{expected} {what}{(expected == 1 ? "" : "s")}",
            $"expected {expected} {what}{(expected == 1 ? "" : "s")}, got {actual}");
    }

    // Extracts numeric scope tokens from evidence — digits with optional %, K/M,
    // years/days/hours, or "70+" style "at least" markers. Each match is the
    // canonical figure plus any unit-suffix that gives it meaning (e.g. "70+",
    // "20%", "120K end users", "5M records"). Conservative on purpose — a vague
    // claim of "many" should not pretend to mirror a concrete figure.
    private static readonly Regex _metricFigureRx = new(
        @"\b(\d{1,3}(?:[.,]\d+)?\+?(?:[KMB])?%?)\b",
        RegexOptions.Compiled);

    private static IEnumerable<RuleMatch> DetectMetricStrip(SessionView view)
    {
        if (view.SelectedResumeEvidence is null) yield break;
        if (IsInsufficientSentinel(view.AnswerText)) yield break;

        // Stage 2's per-priority budget is derived from the LENGTH directive.
        // The Stage 2 prompt rule is explicit: "Use exactly one priority per
        // sentence. If LENGTH allows N sentences and there are K supported
        // priorities, use the first min(N, K) priorities in order — one per
        // sentence — and omit the rest." For paragraph mode the prompt
        // (OpenAiProvider.cs) sets expectedSlotCount = lengthValue × 2,
        // roughly two priorities per paragraph.
        //
        // The detector must mirror this rule. A metric that appears only in
        // an out-of-budget priority is not a "strip" — the priority itself
        // was supposed to be dropped, so dropping its metric is correct
        // behavior. Walking all 5 selected priorities regardless of budget
        // produces false positives on every short answer whose lead priority
        // is metric-less but whose backfill priority is metric-bearing
        // (which is the dominant 1- and 2-sentence pattern).
        //
        // When the length fields are missing (older harness logs), fall back
        // to checking all selected evidence — preserves the original behavior
        // and is the safest assumption when no budget is known.
        int budget = view.ExpectedLengthUnit switch
        {
            QuestionLengthUnit.Sentences  when view.ExpectedLengthValue is int ls => ls,
            QuestionLengthUnit.Paragraphs when view.ExpectedLengthValue is int lp => lp * 2,
            _                                                                     => int.MaxValue,
        };

        // Normalize the answer for figure comparison: strip the optional
        // space before %. The figure regex is invariant to casing already; the
        // normalize is just so that "20%" and "20 %" match the same lookup
        // (we don't currently emit the latter, but harmless).
        var answer = view.AnswerText.Replace(" %", "%");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inBudgetEvidence = view.SelectedResumeEvidence.Take(budget);
        foreach (var evidence in inBudgetEvidence)
        {
            if (string.IsNullOrWhiteSpace(evidence)) continue;

            foreach (Match m in _metricFigureRx.Matches(evidence))
            {
                var figure = m.Value;
                // Skip noise: single-digit years like "8 years" can collide with
                // arbitrary "8" tokens in answer prose. Bare 1-character tokens
                // without a unit-suffix are too ambiguous to flag.
                if (figure.Length < 2 && !figure.EndsWith('+') && !figure.EndsWith('%')) continue;
                if (!seen.Add(figure)) continue;

                // The figure must literally appear in the answer. Substring is
                // sufficient — regex word-boundaries fail for "20%" because %
                // is not a word character.
                if (answer.Contains(figure, StringComparison.OrdinalIgnoreCase)) continue;

                yield return new RuleMatch(
                    figure,
                    $"evidence has \"{Truncate(evidence, 80)}\" with figure {figure}; answer omits it");
            }
        }
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        // Crude but adequate: split on sentence-terminator + whitespace.
        // Quoted inner periods produce false splits; not a concern at this
        // analysis fidelity.
        return Regex
            .Split(text, @"(?<=[.!?])\s+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
    }

    private static string ExtractContext(string text, int matchIndex, int matchLength)
    {
        const int padding = 40;
        var start = Math.Max(0, matchIndex - padding);
        var end   = Math.Min(text.Length, matchIndex + matchLength + padding);
        var snippet = text.Substring(start, end - start)
            .Replace('\n', ' ')
            .Replace('\r', ' ');
        var prefix = start > 0           ? "…" : "";
        var suffix = end   < text.Length ? "…" : "";
        return prefix + snippet + suffix;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..max] + "…";
    }
}
