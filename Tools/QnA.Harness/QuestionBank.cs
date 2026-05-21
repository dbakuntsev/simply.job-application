namespace Simply.JobApplication.Tools.QnA.Harness;

// Strategy enum values mirror the private AnswerStrategy enum inside
// OpenAiProvider. Stage 1 returns one of these as a string; the harness
// compares the returned value against the expected one for each question.
internal static class Strategies
{
    public const string FitNarrative            = "FitNarrative";
    public const string MotivationNarrative     = "MotivationNarrative";
    public const string RelevantExperience      = "RelevantExperience";
    public const string BehavioralExample       = "BehavioralExample";
    public const string DirectFactual           = "DirectFactual";
    public const string EligibilityOrCompliance = "EligibilityOrCompliance";
    public const string CompensationOrLogistics = "CompensationOrLogistics";
    public const string GapOrWeakness           = "GapOrWeakness";
    public const string Other                   = "Other";

    public static readonly string[] All =
    {
        FitNarrative, MotivationNarrative, RelevantExperience, BehavioralExample,
        DirectFactual, EligibilityOrCompliance, CompensationOrLogistics, GapOrWeakness, Other,
    };
}

internal sealed record QuestionSpec(string ExpectedStrategy, string QuestionText);

internal static class QuestionBank
{
    // Each fixture gets exactly one question per strategy. Questions are written
    // to elicit the named strategy from Stage 1's classifier. The actualStrategy
    // recorded in the session result lets us spot when Stage 1 misclassifies.
    public static IReadOnlyList<QuestionSpec> For(Fixture f)
    {
        if (f.Key == Fixtures.Software.Key) return SoftwareBank;
        if (f.Key == Fixtures.Events.Key)   return EventsBank;
        throw new ArgumentOutOfRangeException(nameof(f), $"No question bank for fixture '{f.Key}'.");
    }

    private static readonly IReadOnlyList<QuestionSpec> SoftwareBank = new QuestionSpec[]
    {
        new(Strategies.FitNarrative,
            "Why are you a strong fit for this Senior Backend Engineer role at LedgerLoop?"),

        new(Strategies.MotivationNarrative,
            "What draws you to working at LedgerLoop specifically, and why on a fintech reconciliation product?"),

        new(Strategies.RelevantExperience,
            "Tell us about your experience designing and operating event-driven services on Kafka in .NET."),

        new(Strategies.BehavioralExample,
            "Tell me about a time you led a service migration that materially improved reliability or throughput."),

        new(Strategies.DirectFactual,
            "How many years of production C# / .NET experience do you have?"),

        new(Strategies.EligibilityOrCompliance,
            "Are you authorized to work in the United States or Canada without sponsorship?"),

        new(Strategies.CompensationOrLogistics,
            "What are your base salary expectations for this role, and when could you start?"),

        new(Strategies.GapOrWeakness,
            "This role mentions audit and SOC 2 familiarity as preferred. You don't have direct SOC 2 experience — how would you handle that?"),

        new(Strategies.Other,
            "If LedgerLoop's mascot were an animal, what should it be and why?"),
    };

    private static readonly IReadOnlyList<QuestionSpec> EventsBank = new QuestionSpec[]
    {
        new(Strategies.FitNarrative,
            "Why are you a strong fit for the Events & Volunteer Coordinator role at Riverside Arts Council?"),

        new(Strategies.MotivationNarrative,
            "What draws you to Riverside Arts Council and to community-arts work specifically?"),

        new(Strategies.RelevantExperience,
            "Describe your experience using volunteer management software such as VolunteerLocal or Better Impact."),

        new(Strategies.BehavioralExample,
            "Tell me about a time you ran a large outdoor event where logistics did not go to plan."),

        new(Strategies.DirectFactual,
            "How many years of event coordination experience do you have, and what is the largest attendee count you have managed?"),

        new(Strategies.EligibilityOrCompliance,
            "This role requires a background check before start. Are you willing to complete one?"),

        new(Strategies.CompensationOrLogistics,
            "The salary range is $52K–$60K. Where would you expect to land, and are you available for evenings and weekends?"),

        new(Strategies.GapOrWeakness,
            "You haven't run a grants-reporting workflow end-to-end. How would you approach that part of this role?"),

        new(Strategies.Other,
            "If you could program any artist as the festival headliner, who would it be and why?"),
    };
}
