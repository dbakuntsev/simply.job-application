using Simply.JobApplication.Models;
using Simply.JobApplication.Services.QnA;

namespace Simply.JobApplication.Tests.QA;

// Unit tests for the QualityRules detector library. These cover the rule
// implementations directly (no HTTP, no provider) — the prompt-side tests
// asserting that the Stage 1/2 instructions match what the rules check live
// in AnswerQuestionTests.
//
// Each rule has its own block. The library is exposed via QualityRules.All;
// we look the rule up by Id and invoke its Detect closure on a hand-built
// SessionView so we can assert on the exact match list returned.
public class QualityRulesTests
{
    private static SessionView MakeView(
        string                 answer,
        string                 gap              = "",
        int?                   lengthValue      = null,
        QuestionLengthUnit?    lengthUnit       = null,
        IReadOnlyList<string>? selectedEvidence = null)
        => new(
            SessionId:              "test",
            AnswerText:             answer,
            GapAcknowledgment:      gap,
            Boundaries:             Array.Empty<string>(),
            Ignore:                 Array.Empty<string>(),
            ExpectedLengthValue:    lengthValue,
            ExpectedLengthUnit:     lengthUnit,
            SelectedResumeEvidence: selectedEvidence);

    private static IReadOnlyList<RuleMatch> RunDetector(string ruleId, SessionView view)
    {
        var rule = QualityRules.All.SingleOrDefault(r => r.Id == ruleId);
        Assert.NotNull(rule);
        return rule.Detect(view).ToList();
    }

    private const string InsufficientSentinel =
        "I cannot determine that from the provided resume and role information.";

    // ── gap-splice-malformed ─────────────────────────────────────────────
    //
    // Failure A — uppercase splice opener (Stage 1 emitted a sentence/noun).
    // Failure B — determiner/noun-starter token after "have not".
    // Failure C — sentence-terminator inside the splice (full-sentence gap).
    // Negative — past-participle opener, empty gap, insufficient sentinel,
    // template not used (delegated to gap-template-mismatch), and qualifier
    // adverbs like "yet" that legitimately precede the verb.

    [Fact]
    public void GapSpliceMalformed_UppercaseOpener_Fires()
    {
        // Reproduces the events_BehavioralExample_Concise_2s failure from run
        // 20260520-014045 — Stage 1 returned a full sentence in
        // gapAcknowledgment and Stage 2 spliced it verbatim.
        var view = MakeView(
            answer: "While I have not No specific disruption incident is described in the resume., " +
                    "I have planned and ran the annual festival.",
            gap:    "No specific disruption incident is described in the resume.");

        var hits = RunDetector("gap-splice-malformed", view);

        var hit = Assert.Single(hits);
        Assert.Contains("uppercase splice opener", hit.Context);
        Assert.Contains("\"No\"", hit.Context);
    }

    [Fact]
    public void GapSpliceMalformed_DeterminerOpener_Fires()
    {
        // Lowercase, but a determiner ("the") cannot grammatically follow
        // "have not" — Stage 1 emitted a noun-phrase splice.
        var view = MakeView(
            answer: "While I have not the experience required, I have built reliable systems.",
            gap:    "the experience required");

        var hits = RunDetector("gap-splice-malformed", view);

        var hit = Assert.Single(hits);
        Assert.Contains("determiner/noun splice opener", hit.Context);
    }

    [Fact]
    public void GapSpliceMalformed_PeriodInsideSplice_Fires()
    {
        // Lowercase past-participle opener, but a period before the comma
        // indicates a full-sentence gap string was spliced in.
        var view = MakeView(
            answer: "While I have not described a specific incident. for festival logistics, " +
                    "I have planned community events.",
            gap:    "described a specific incident. for festival logistics");

        var hits = RunDetector("gap-splice-malformed", view);

        var hit = Assert.Single(hits);
        Assert.Contains("sentence-terminator inside splice", hit.Context);
    }

    [Fact]
    public void GapSpliceMalformed_ValidPastParticipleOpener_DoesNotFire()
    {
        // The "good" shape per the Stage 1 verb-phrase rule. All four
        // past-participle starters that appeared in the verified focused-run
        // output (described, run, had, owned) should pass.
        var samples = new[]
        {
            ("described a specific incident on the resume",  "described a specific incident on the resume"),
            ("run a grants-reporting workflow end-to-end",   "run a grants-reporting workflow end-to-end"),
            ("had direct SOC 2 experience",                  "had direct SOC 2 experience"),
            ("owned a grants-reporting workflow end-to-end", "owned a grants-reporting workflow end-to-end"),
        };
        foreach (var (gap, _) in samples)
        {
            var view = MakeView(
                answer: $"While I have not {gap}, I have built reliable systems.",
                gap:    gap);

            Assert.Empty(RunDetector("gap-splice-malformed", view));
        }
    }

    [Fact]
    public void GapSpliceMalformed_QualifierAdverbBeforeVerb_DoesNotFire()
    {
        // "yet" and "ever" precede the verb legitimately — the detector's
        // bad-starter set deliberately excludes them. Documents that
        // exclusion as a stable contract.
        var view = MakeView(
            answer: "While I have not yet completed the certification, I have studied the framework.",
            gap:    "yet completed the certification");

        Assert.Empty(RunDetector("gap-splice-malformed", view));
    }

    [Fact]
    public void GapSpliceMalformed_EmptyGap_DoesNotFire()
    {
        // No gap declared by Stage 1 → no template-splice contract to check.
        // Belt-and-braces: even if the answer happens to start with "While I
        // have not …" by coincidence, the rule must stay silent without a
        // populated gap.
        var view = MakeView(
            answer: "While I have not seen this exact setup, I have built similar systems.",
            gap:    "");

        Assert.Empty(RunDetector("gap-splice-malformed", view));
    }

    [Fact]
    public void GapSpliceMalformed_InsufficientSentinel_DoesNotFire()
    {
        // Stage 1 gating produced the insufficient sentinel; Stage 2 was
        // skipped. No real answer to check, so contract rules don't fire.
        var view = MakeView(
            answer: InsufficientSentinel,
            gap:    "described a specific incident");

        Assert.Empty(RunDetector("gap-splice-malformed", view));
    }

    [Fact]
    public void GapSpliceMalformed_TemplateNotUsed_DelegatesToSiblingRule()
    {
        // When Stage 2 didn't use the "While I have not …" template at all,
        // the gap-template-mismatch sibling rule catches it. This rule is
        // scoped to splice quality within the template, so it must stay
        // silent — otherwise both rules would fire on the same failure and
        // inflate the rate.
        var view = MakeView(
            answer: "I have not run grant reporting end to end, but I wrote volunteer narratives.",
            gap:    "run a grants-reporting workflow end-to-end");

        Assert.Empty(RunDetector("gap-splice-malformed", view));
    }

    // ── metric-strip (LENGTH-bounded) ────────────────────────────────────
    //
    // Stage 2's per-priority budget is derived from LENGTH:
    //   Sentences  → budget = lengthValue
    //   Paragraphs → budget = lengthValue × 2
    // Metrics in priorities past that budget are not "strips" — the priority
    // itself was supposed to be omitted. The pre-fix detector walked every
    // selected priority regardless of budget, producing systematic false
    // positives on every short answer with a metric-bearing tail priority
    // (11 of 12 hits on run 20260521-021924 were that false-positive class).

    // Canonical evidence strings used across the metric-strip tests. Each
    // carries exactly one extractable figure under the detector's regex
    // (digits with K/M/B/% suffix, or a 4-digit count). Anchoring on the
    // figures the regex actually extracts — not idiomatic phrasings like
    // "4x" that the regex deliberately skips — keeps test intent clear and
    // matches the patterns the harness flagged in real runs (18M, 310K, 220).
    private const string EvidenceWithMetric_18M =
        "Owned the rebuild of a settlement service that processed 18M transactions per month";
    private const string EvidenceWithMetric_310K =
        "Tracked event budgets across nine programs totaling $310K annually";
    private const string EvidenceWithoutMetric_Audit =
        "Partnered with the data team to design schemas for daily settlement audit trails";
    private const string EvidenceWithoutMetric_OnCall =
        "Acted as on-call lead for the payments squad";

    [Fact]
    public void MetricStrip_LeadPriorityRetainsItsMetric_DoesNotFire()
    {
        var view = MakeView(
            answer: "I owned the rebuild of a settlement service that processed 18M transactions per month.",
            lengthValue:      1,
            lengthUnit:       QuestionLengthUnit.Sentences,
            selectedEvidence: new[] { EvidenceWithMetric_18M, EvidenceWithoutMetric_OnCall });

        Assert.Empty(RunDetector("metric-strip", view));
    }

    [Fact]
    public void MetricStrip_LeadPriorityDropsItsMetric_Fires()
    {
        // Same lead as above; the answer paraphrases "18M transactions"
        // away. The detector must flag 18M.
        var view = MakeView(
            answer: "I owned the rebuild of a settlement service for a payments platform.",
            lengthValue:      1,
            lengthUnit:       QuestionLengthUnit.Sentences,
            selectedEvidence: new[] { EvidenceWithMetric_18M, EvidenceWithoutMetric_OnCall });

        var hit = Assert.Single(RunDetector("metric-strip", view));
        Assert.Equal("18M", hit.MatchedText);
    }

    [Fact]
    public void MetricStrip_OutOfBudgetPriorityDropsItsMetric_DoesNotFire()
    {
        // Reproduces the software_GapOrWeakness_Concise_1s false positive
        // from run 20260521-021924 (pre-fix). LENGTH=1, lead has no metric
        // (audit-trails) and the answer faithfully uses it; #2 has 18M but
        // is out of budget — Stage 2 was instructed to omit it, so dropping
        // its metric is correct behavior, not a "strip".
        var view = MakeView(
            answer: "I partnered with the data team to design schemas for daily settlement audit trails.",
            lengthValue:      1,
            lengthUnit:       QuestionLengthUnit.Sentences,
            selectedEvidence: new[]
            {
                EvidenceWithoutMetric_Audit,
                EvidenceWithMetric_18M,
                EvidenceWithoutMetric_OnCall,
            });

        Assert.Empty(RunDetector("metric-strip", view));
    }

    [Fact]
    public void MetricStrip_InBudgetMetricDropped_AcrossMultiplePriorities_Fires()
    {
        // LENGTH=2 → budget=2. Both #1 (310K) and #2 (18M) are in-budget.
        // The answer surfaces 310K but drops 18M — that's a real strip
        // from an in-budget priority.
        var view = MakeView(
            answer: "I tracked event budgets across nine programs totaling $310K annually. " +
                    "I owned the rebuild of a settlement service for a payments platform.",
            lengthValue:      2,
            lengthUnit:       QuestionLengthUnit.Sentences,
            selectedEvidence: new[]
            {
                EvidenceWithMetric_310K,
                EvidenceWithMetric_18M,
                EvidenceWithoutMetric_OnCall,
            });

        var hit = Assert.Single(RunDetector("metric-strip", view));
        Assert.Equal("18M", hit.MatchedText);
    }

    [Fact]
    public void MetricStrip_ParagraphsBudget_IsDoubleTheLengthValue()
    {
        // LENGTH=1 paragraph → budget=2 priorities (per Stage 2's
        // expectedSlotCount = lengthValue × 2). #1 retains its 310K
        // metric, #2 has no metric, #3's 18M is out of budget.
        var view = MakeView(
            answer: "I tracked event budgets across nine programs totaling $310K annually. " +
                    "I acted as on-call lead for the payments squad.",
            lengthValue:      1,
            lengthUnit:       QuestionLengthUnit.Paragraphs,
            selectedEvidence: new[]
            {
                EvidenceWithMetric_310K,
                EvidenceWithoutMetric_OnCall,
                EvidenceWithMetric_18M,
            });

        Assert.Empty(RunDetector("metric-strip", view));
    }

    [Fact]
    public void MetricStrip_NullLengthFields_FallsBackToCheckingAllEvidence()
    {
        // Pre-existing harness logs (and any external caller that doesn't
        // know LENGTH) get the old behavior — check every selected priority.
        // This is the safety fallback so the rule never silently no-ops on
        // older artifacts when LENGTH context is missing.
        var view = MakeView(
            answer: "I tracked event budgets across nine programs totaling $310K annually.",
            lengthValue:      null,
            lengthUnit:       null,
            selectedEvidence: new[] { EvidenceWithMetric_310K, EvidenceWithMetric_18M });

        var hit = Assert.Single(RunDetector("metric-strip", view));
        Assert.Equal("18M", hit.MatchedText);
    }

    [Fact]
    public void MetricStrip_InsufficientSentinel_DoesNotFire()
    {
        var view = MakeView(
            answer: InsufficientSentinel,
            lengthValue:      1,
            lengthUnit:       QuestionLengthUnit.Sentences,
            selectedEvidence: new[] { EvidenceWithMetric_18M });

        Assert.Empty(RunDetector("metric-strip", view));
    }

    [Fact]
    public void MetricStrip_NullSelectedEvidence_DoesNotFire()
    {
        // Older session logs (pre-rule) have no SelectedResumeEvidence field.
        // The detector must short-circuit instead of NRE'ing.
        var view = MakeView(
            answer:           "Some answer with no figures",
            lengthValue:      1,
            lengthUnit:       QuestionLengthUnit.Sentences,
            selectedEvidence: null);

        Assert.Empty(RunDetector("metric-strip", view));
    }

    // ── Library smoke tests ──────────────────────────────────────────────

    [Fact]
    public void QualityRules_All_ContainsBothGapRulesWithDistinctIds()
    {
        // Both rules must exist and be distinct — the gap-splice-malformed
        // rule was deliberately separated from gap-template-mismatch so the
        // historical trend on the original rule stays clean. A rename or
        // accidental dedup would skew the trend across runs.
        var ids = QualityRules.All.Select(r => r.Id).ToHashSet();
        Assert.Contains("gap-template-mismatch", ids);
        Assert.Contains("gap-splice-malformed",  ids);
    }

    [Fact]
    public void QualityRules_All_HaveUniqueIdsAndNonEmptyDescriptions()
    {
        // Quality-of-library sanity: ids are the public identifiers that
        // appear in run reports, so collisions or empty descriptions would
        // make analyzer output ambiguous. Cheap to assert; valuable when
        // someone adds a new rule and forgets a field.
        var rules = QualityRules.All;

        Assert.Equal(rules.Count, rules.Select(r => r.Id).Distinct().Count());
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.Id)));
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.Description)));
        Assert.All(rules, r => Assert.False(string.IsNullOrWhiteSpace(r.SourceFix)));
    }
}
