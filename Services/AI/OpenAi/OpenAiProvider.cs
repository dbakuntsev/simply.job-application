using Simply.JobApplication.Models;
using System.Net.Http.Headers; // AuthenticationHeaderValue
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simply.JobApplication.Services.AI.OpenAi;

public class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string ProviderId   => "openai";
    public string DisplayName  => "OpenAI";
    public string DefaultModelId => "gpt-5.4";

    public IReadOnlyList<AiModel> AvailableModels { get; } = new List<AiModel>
    {
        new("gpt-5.4",      "GPT-5.4"),
        new("gpt-5-mini",   "GPT-5 mini"),
        new("gpt-4.1",      "GPT-4.1"),
        new("gpt-4.1-mini", "GPT-4.1 mini"),
    };

    public OpenAiProvider(HttpClient http) => _http = http;

    // ── Retry infrastructure ───────────────────────────────────────────────────

    private const int MaxRetryAttempts = 3;
    private static readonly int[] RetryableHttpCodes = { 408, 429, 500, 502, 503, 524 };

    // Thrown for HTTP status codes that warrant a retry.
    private sealed class RetryableApiException(int statusCode, string message, TimeSpan? retryAfter = null)
        : Exception(message)
    {
        public int      StatusCode { get; } = statusCode;
        public TimeSpan? RetryAfter { get; } = retryAfter;
    }

    // Thrown when the SSE stream closes before a response.completed event arrives.
    private sealed class TruncatedStreamException()
        : Exception("Responses API stream ended without a response.completed event");

    private static bool IsRetryable(Exception ex)
        => ex is RetryableApiException or TruncatedStreamException or HttpRequestException;

    // Exponential backoff (1 s, 2 s, 4 s … capped at 10 s) with ±20 % jitter.
    // Respects the Retry-After value from 429 responses when provided.
    private static TimeSpan ComputeBackoff(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } ra && ra > TimeSpan.Zero)
            return ra;
        var baseSeconds = Math.Min(Math.Pow(2, attempt - 1), 10.0);
        var jitter      = baseSeconds * 0.2 * (Random.Shared.NextDouble() * 2 - 1);
        return TimeSpan.FromSeconds(Math.Max(0.5, baseSeconds + jitter));
    }

    // Retries `operation` up to MaxRetryAttempts times on transient failures.
    // Reports progress via `onProgress` before each attempt so the UI can show
    // "… · retry N of M" messages.
    private static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation, string stepLabel, Action<string>? onProgress)
    {
        for (var attempt = 1; ; attempt++)
        {
            onProgress?.Invoke(attempt == 1
                ? stepLabel
                : $"{stepLabel} · retry {attempt} of {MaxRetryAttempts}");
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < MaxRetryAttempts)
            {
                await Task.Delay(ComputeBackoff(attempt, (ex as RetryableApiException)?.RetryAfter));
            }
        }
    }

    // ── Public interface ──────────────────────────────────────────────────────

    public async Task<MatchEvaluation> EvaluateMatchAsync(
        JobDescription job, string resumeMarkdown, string modelId, string apiKey,
        IReadOnlyList<string>? additionalKeywords = null,
        Action<string>? onProgress = null)
    {
        const string instructions = """
            You are an expert resume analyst.

            The candidate's resume is provided in Markdown format converted from their original DOCX file.
            Heading levels (#, ##) reflect the document's section structure. **Bold** spans indicate
            emphasized phrases. Links preserve the original hyperlinks.

            Adapt terminology, categorization, and evaluation criteria to the profession implied by the job description.

            ### Evaluation Objective

            Evaluate how well the candidate matches the job description.
            
            Scoring definition:
            - Excellent: Candidate meets most required qualifications and many preferred ones.
            - Good: Candidate meets the majority of required qualifications but may lack some preferred skills.
            - Fair: Candidate meets some required qualifications but lacks several key requirements.
            - Poor: Candidate lacks most required qualifications.

            ### Internal Analysis

            Internally identify:
            - required technologies
            - required responsibilities
            - required experience level
            - preferred qualifications
            Then evaluate match.

            ### Evaluation Rules

            Base the evaluation only on information explicitly present in the resume and, if provided, the
            "Additional Keywords Confirmed by Candidate" section.

            Compare the job description requirements directly against evidence in the resume.

            Treat as requirements only items that describe:
            - skills
            - tools or systems
            - methodologies or practices
            - certifications
            - years of experience
            - responsibilities necessary to perform the role

            Pay special attention to matching job-relevant skills, tools, systems, and core practices.

            These may include (depending on profession):
            - technical tools or platforms
            - analytical methods
            - professional practices
            - operational systems
            - role-specific competencies

            Consider both:
            - skill/tool alignment
            - responsibility scope and impact
            when determining the score.

            ### Seniority and Scope

            Consider whether the candidate's experience level aligns with the seniority implied in the job description.

            Evaluate:
            - years of experience
            - leadership responsibilities
            - scope of ownership
            - complexity and impact of work

            Evaluate whether the candidate's responsibilities reflect similar scope or impact as the role described.

            ### Matching Logic

            Consider common synonyms and variations of technologies when comparing resume and job description.

            If the job description lists a single specific required skill, tool, system, or certification and there is no evidence of 
            it in the resume, this should be considered a significant gap, and the score should not be higher than "Fair".

            If the candidate meets most required skills and responsibilities, the score should not be lower than "Good".

            If the candidate meets most required skills but lacks some responsibilities, the score should typically be "Good" rather than "Fair".

            Treat grouped or combined skill listings (e.g., "JavaScript/TypeScript") as valid evidence of each individual skill unless explicitly contradicted.
            
            Do not require phrasing identical to the job description if the meaning is clearly equivalent.
            
            # Suggested Keywords (ATS Optimization)

            Additionally, identify non-domain-specific keywords that:
            - are explicitly mentioned in the job description
            - do NOT appear in the resume
            - do NOT appear in "Additional Keywords Confirmed by Candidate" (if provided)

            Non-domain-specific keywords include:
            - skills, tools, systems, methodologies, or professional practices
            - transferable across organizations within the same profession or across industries

            Exclude:
            - organization-specific processes
            - highly specialized domain expertise (e.g., regulatory frameworks, niche industry systems)

            ### Ranking Criteria

            Rank suggested keywords by estimated ATS impact using the following priority:
            1. Keywords explicitly listed as required
            2. Keywords repeated multiple times in the job description
            3. Keywords tied to core responsibilities
            4. Frequently emphasized preferred qualifications

            ### Suggestion Constraints

            Only suggest keywords that are reasonably inferable from the candidate’s:
            - existing experience
            - adjacent skills
            - role responsibilities
            
            Do not assume the candidate has experience with a keyword. Only suggest it as a
            potential addition if it is plausible but not explicitly stated.

            Avoid suggesting:
            - soft skills (e.g., communication, teamwork)
            - redundant or overlapping keywords
            - keywords already clearly present in the resume
            - keywords already confirmed by the candidate
            
            Suggested keywords must be concrete, scannable terms usable in ATS (e.g., tools, systems, methods)
            
            Exclude abstract phrases, cultural descriptors, or vague competencies (e.g., "fast-paced environment", "engineering excellence")
            
            ### Keyword Categorization

            Group each keyword under a concise category appropriate to the role, such as:
            - Tools / Systems
            - Technical Skills
            - Professional Practices
            - Analytical Methods
            - Platforms
            - Frameworks / Methodologies
            - Data / Reporting
            - Client / Stakeholder Work
            - Operations / Process
            Use categories that best fit the profession implied by the job description.
            
            Use consistent, non-overlapping categories.

            Prefer a normalized set of categories adapted to the role.
            
            Do not create semantically redundant categories (e.g., "Technical Skills" vs "Tools / Systems").

            Return the keywords sorted in descending order of estimated ATS impact.
            Limit the total to 10 keywords.

            # Output Format

            Respond with a JSON object only — no markdown, no explanation — using this exact schema:
            
            {
              "score": "Poor" | "Fair" | "Good" | "Excellent",
              "gaps": ["string", ...],
              "strengths": ["string", ...],
              "isGoodMatch": true | false,
              "suggestedKeywords": [{"category": "string", "keyword": "string"}, ...]
            }

            # Output Rules

            - "gaps" lists specific qualifications, requirements, or experience areas where the candidate is poorly matched.
            - Only list gaps for required qualifications or clearly critical responsibilities.
            - Only report gaps that are explicitly mentioned in the job description.
            - Each gap must reference a specific requirement and quote relevant wording from the job description.
            - Phrase gaps as absence of evidence using varied, natural phrasing
            - Avoid repeating identical sentence structures across all gaps
            - Include up to 5 gaps only if they exist; do not invent or retain weak gaps to reach a count.
            - Prioritize gaps in this order:
              * Missing required skills/tools/systems
              * Missing critical responsibilities
              * Missing required experience level
            - Avoid duplicate or overlapping gaps.
            - Do NOT include any item in "gaps" if the requirement is clearly satisfied or exceeded.
            - If a requirement is exceeded, it must NOT appear in "gaps" under any circumstances.
            - Before finalizing output:
              * Remove any gap that contradicts the strengths or overall evaluation
              * Ensure all gaps represent true deficiencies, not satisfied or exceeded requirements
            - Do NOT include meta-commentary, corrections, or explanations inside gap entries
            - Each gap must be a clean statement of missing or insufficient evidence
            - "strengths" must:
              * reflect clear alignment with job requirements
              * be supported by explicit resume evidence
              * reference specific skills, tools, responsibilities, or qualifications
              * prioritize recent experience
              * be limited to 5 items
              * avoid duplication
            - "isGoodMatch" must be true only when score is "Good" or "Excellent".
            - "suggestedKeywords" must:
              * be an array (may be empty)
              * contain objects with "category" and "keyword" string fields.
            - Output must:
              * be valid JSON
              * match schema exactly 
              * contain no additional fields.
            
              ### Final Consistency Check

            Before output:
            - Ensure no gap contradicts any listed strength
            - Ensure no preferred qualification is listed as a gap
            - Ensure all suggestedKeywords comply with keyword constraints
            - Ensure no filler or weak gaps are included
            """;

        var additionalSection = additionalKeywords is { Count: > 0 }
            ? $"\n\nAdditional Keywords Confirmed by Candidate:\n{string.Join("\n", additionalKeywords.Select(k => $"- {k}"))}"
            : "";

        var userText = $"""
            Job Description
            Company: {job.CompanyName}
            Title: {job.JobTitle}
            Details:
            {job.JobDetails}

            Candidate Resume (Markdown):
            {resumeMarkdown}{additionalSection}

            Respond in JSON.
            """;

        var (responseId, raw) = await ExecuteWithRetryAsync(
            () => CreateResponseWithTextAsync(modelId, apiKey, instructions, userText),
            "Evaluating match…", onProgress);

        MatchEvaluation evaluation;
        try
        {
            evaluation = JsonSerializer.Deserialize<MatchEvaluation>(raw, _json)
                         ?? throw new InvalidOperationException("Empty response");
        }
        catch
        {
            throw new InvalidOperationException($"Could not parse AI response: {raw}");
        }

        evaluation.AiResponseId = responseId;
        return evaluation;
    }

    public async Task<GeneratedMaterials> GenerateMaterialsAsync(
        JobDescription job, string resumeMarkdown, MatchEvaluation evaluation, string modelId, string apiKey,
        IReadOnlyList<string>? additionalKeywords = null, int sourcePageCount = 2, int targetPageCount = 2,
        Action<string>? onProgress = null)
    {
        var instructions = $$"""
            You are an expert resume writer and career coach.

            Generate tailored job application materials based on the candidate's resume and the job description.

            Adapt terminology, categorization, and phrasing to the profession implied by the job description.

            ## Inputs

            Inputs available in this conversation:
            1. Job description text
            2. Original resume in Markdown format (structure and formatting preserved from the original DOCX)

            ## Markdown Structure

            The Markdown resume uses these conventions:
            - Line 1: candidate name (Title style)
            - Next 1-2 lines: contact / location lines (Subtitle style)
            - If additional paragraph text appears before the first # heading, treat it as the candidate's professional summary.
            - # headings: top-level resume sections
            - ## headings: job title + company + date lines
            - **...** spans: emphasized phrases (Emphasis character style)
            - [text](url): hyperlinks from the original resume

            ## Internal Analysis

            Internally identify:
            - top 5 required skills, tools, systems, or professional practices
            - top 3 core responsibilities or activities
            - seniority level (years, scope, leadership expectations)
            - industry or domain context (if present)
            Use this context when framing experience. Use domain context to emphasize relevant industry experience when present.

            ## Pre-Rewrite Planning

            Before generating outputs, internally determine:
            - job_keywords
            - resume_matching_keywords
            - resume_missing_keywords
            - top_experiences_to_highlight
            - experiences_to_deemphasize
            Then perform rewriting.

            ## Rewriting Principles

            When rewriting:
            - Use terminology from the job description when describing equivalent experience
            - Incorporate resume_matching_keywords where they accurately reflect actual work
            - Avoid artificial keyword repetition
            - Each keyword should appear only where it naturally fits

            Prioritize highlighting:
            - experience from the last 5–8 years
            - work most relevant to the role’s responsibilities and requirements

            ## Prioritization Framework

            Prioritize the following when tailoring:
            1. Explicit required skills/tools/practices
            2. Explicit preferred skills/tools/practices
            3. Skills implied by responsibilities
            4. Closely related or adjacent capabilities
            5. General transferable skills
            Only treat as explicit skills when clearly named in the job description.

            Skills may include:
            - tools or systems
            - technical or professional capabilities
            - analytical methods
            - platforms or environments
            - role-specific practices

            Do NOT treat general concepts (e.g., teamwork, communication, general methodologies) as primary skills unless explicitly emphasized.

            ## Structure Preservation

            Maintain the same high-level section structure as the original resume unless reordering improves relevance.

            Only reorder sections if doing so clearly improves ATS keyword visibility (for example moving Skills above Experience).

            Do not remove, change or rephrase:
            - applicant's name
            - contact information
            - organization names
            - job/role titles
            - employment dates

            Do not add seniority modifiers to titles unless present in the original resume.

            Do not change education details, certifications, or credentials.

            ## Professional Summary Handling
            
            If the original resume contains a professional summary paragraph between the contact information and the first section heading, then:
            - Retain and rewrite it
            - Place it immediately after contact/location lines
            - Keep it 2–4 concise sentences

            The summary should function as a high-impact positioning statement, not a general introduction.

            It must:
            - be written in a concise, declarative tone (no first-person pronouns)
            - clearly communicate seniority level (based on years of experience, scope, or leadership responsibility)
            - reflect the candidate’s primary area of specialization aligned to the role
            - include 1–2 concrete indicators of scope, scale, or complexity when available (e.g., size of projects, clients, operations, or outcomes)
            - incorporate 2–4 high-priority job-relevant keywords naturally
            - emphasize outcomes, ownership, or organizational impact where applicable

            Structure guidelines. Each sentence must serve a distinct purpose:
            - Sentence 1: identity and scope (no specialization details)
            - Sentence 2: primary specialization (no scale or metrics)
            - Sentence 3: scale, impact, or outcomes (must include concrete indicators if available)
            - Sentence 4 (optional): leadership or execution style
            
            Do not combine these roles within a single sentence.
            
            Sentence 3 must:
            - include concrete scale or measurable scope
            - optionally include one clear impact or outcome
            Avoid:
            - listing multiple generic quality attributes (e.g., reliability, scalability, security)
            - combining too many dimensions into one sentence
            
            Avoid generic performance phrases such as:
            - "proven record"
            - "strong record"
            - "consistent history"
            Replace with concrete evidence (e.g., scale, outcomes, scope).
            
            Strictly avoid:
            - first-person language ("I", "my")
            - expressions of interest or intent ("seeking", "interested in", "looking for")
            - references to the employer ("your", company name)
            - generic soft skills unless explicitly required
            - listing multiple generic quality attributes (e.g., reliability, scalability, security) unless directly differentiated

            The summary must be tailored to reflect the most important responsibility, function, or capability described in the job description.

            Do not remove the summary unless it is clearly redundant.
            
            If a professional summary exists, place it immediately after the contact/location lines and before the first # section heading. 

            If content must be shortened, prioritize trimming older experience before removing the summary.

            ## Tense Consistency

            Use consistent tense throughout the summary:

            - Prefer present tense for role identity and ongoing capabilities
            - Use past tense only for clearly completed achievements

            Avoid mixing tenses across sentences unless necessary for clarity.

            ## Seniority-Specific Professional Summary Adjustment

            Adjust emphasis based on the seniority implied by the role:

            For senior or leadership roles:
            - Emphasize ownership of functions, programs, or areas of responsibility rather than individual tasks
            - Highlight influence across teams, departments, or stakeholders
            - Focus on strategic contributions, decision-making, and oversight
            - Include indicators of scope (e.g., size of initiatives, teams, portfolios, or impact)
            - The final sentence should emphasize: influence, decision-making, enabling others

            For mid-level roles:
            - Balance independent execution with some ownership of projects or processes
            - Highlight specialized skills and contributions to outcomes

            For early-career roles:
            - Emphasize hands-on skills, foundational knowledge, and direct contributions

            The final sentence should describe leadership in terms of impact and influence, not activities.

            Prefer:
            - "driving direction and standards"
            - "guiding teams and shaping execution"
            - "enabling delivery across teams or functions"

            Avoid:
            - listing multiple activities (e.g., "code review, mentoring, planning")
            - process-heavy or operational enumeration

            Prefer concise descriptions of leadership impact over activity lists.
            
            ## Professional Summary Alignment Anchor

            Identify the single most important function, responsibility, or capability described in the job description.

            The summary must explicitly and clearly state this concept in Sentence 2 as the primary specialization using direct or closely aligned terminology.

            This concept should be immediately identifiable when scanning the summary.            

            ## Professional Summary Keyword Rule

            Within the summary:
            - Prioritize high-impact, role-defining keywords over broad coverage
            - Include no more than 3 core concepts total, with one clearly dominant specialization.
            - If more are present in the source resume, select the most relevant and omit the rest
            - Prefer phrases that imply responsibility, specialization, or outcomes over simple lists of tools or skills

            ## Redundant Domain Elimination

            Do not include broad or implied domains that are already conveyed by the candidate’s title or seniority level.

            Examples:
            - Avoid repeating "software architecture" for architect-level roles
            - Avoid generic fields that do not differentiate the candidate

            Instead:
            - prioritize more specific, distinguishing areas of expertise aligned with the job description
            
            ## Professional Summary Anti-Patterns

            Avoid the following:
            - Generic summaries that could apply to multiple roles or industries
            - Rewriting experience bullets into paragraph form
            - Listing tools, skills, or areas of knowledge without context
            - Overly broad descriptors (e.g., "results-driven", "detail-oriented") unless clearly supported
            - Narrative or cover letter-style language
            
            ## Summary Conciseness Constraint

            Avoid comma-separated lists of more than two items within a sentence.

            If multiple capabilities are present:
            - consolidate them into a single cohesive phrase
            - or prioritize the most relevant and omit the rest

            Do not enumerate multiple domains, systems, or practices in parallel.
            
            ## Dominant Specialization Requirement

            The professional summary must present one clearly dominant area of specialization.

            - This specialization must appear explicitly in Sentence 2
            - It must unify the candidate’s capabilities into a single theme
            - Supporting capabilities may be included, but must be subordinate to this primary focus

            If multiple domains are present in the resume:
            - Select the one most aligned with the job description
            - De-emphasize or omit others

            The reader should be able to answer in one phrase:
            "What is this person primarily specialized in?"

            ## Specialization Clarity Requirement

            Sentence 2 must contain a single, clearly identifiable specialization.

            - The specialization must be understandable as one unified concept
            - It must be immediately clear upon scanning
            - It must not be expressed as a list of multiple parallel areas

            The reader should be able to answer in one phrase:
            "What is this person primarily specialized in?"
            
            ## Specialization Framing Rule

            Sentence 2 must express the primary specialization as a single, cohesive phrase.

            This phrase must:
            - combine domain + function (what + why)
            - reflect how the candidate applies their expertise
            - align directly with the job description’s core responsibility

            Prefer:
            - "design of shared systems enabling consistency across applications"
            - "development of scalable client-facing platforms"
            - "management of complex, high-volume operational workflows"

            Avoid:
            - listing multiple independent domains (e.g., "X, Y, and Z")
            - disconnected technical or subject-matter areas without a unifying purpose

            ## Experience Section Optimization
            
            When modifying experience:
            - Move the most relevant achievements to the top of each role
            - De-emphasize or shorten less relevant content
            - Preserve factual accuracy

            Each achievement should:
            - reflect real experience from the resume
            - emphasize impact, outcomes, or scope where available
            - align with job-relevant responsibilities

            ## ATS Optimization

            Optimize the resume for Applicant Tracking Systems (ATS):
            - Use clear job-relevant keywords
            - Prefer simple, readable structures
            - Prefer standard section titles (Skills, Experience, Education)
            - Avoid unusual formatting
            - Include measurable outcomes when present in the original resume
            
            ## Handling Missing Skills

            If the job description includes skills not present in the resume:
            - Do NOT claim experience with them.
            - Instead emphasize adjacent technologies or transferable experience.

            ## Additional Keywords Confirmed by Candidate

            If "Additional Keywords Confirmed by Candidate" are provided:
            - Treat them as verified skills
            - Ensure ALL are included in the Skills section
            - Do not omit any

            Placement and formatting:
            - Integrate into the Skills section (not appended separately)
            - Group logically where possible
            - Ensure consistent naming
            - Avoid duplication
            - Present as discrete, scannable entries

            Usage in experience:
            - Reference only when they plausibly align with actual work
            - Do not force inclusion

            Keyword discipline:
            - Do not overuse confirmed keywords across multiple sections
            - Primary placement must be in the Skills section

            ## Suggested Keywords (Unconfirmed)

            If "Suggested Keywords" from prior analysis are provided:
            - Treat them as unverified
            - Include only if strongly supported by existing experience
            - Do not introduce unsupported claims

            ## Skills Section Rules

            The Skills section should contain:
            - existing resume skills
            - all confirmed additional keywords
            - relevant, supported job-aligned skills

            The Skills section must NOT include:
            - unsupported or inferred skills
            - redundant variations

            
            ## Length Control

            {{BuildLengthControlSection(sourcePageCount, targetPageCount)}}

            ## Cover Letter
            
            The cover letter is comprised of 2 paragraphs:
            - Paragraph 1:
              * Express interest in the role
              * Mention the organization or product
              * Reference a specific requirement, responsibility, or capability from the job description
            - Paragraph 2:
              * Highlight 2–3 relevant experiences
              * Briefly address any skill gap using transferable strengths
              * Focus on impact, alignment with role responsibilities
              * End with a forward-looking statement
            Avoid repeating resume bullet points

            ### Cover Letter Structure Enforcement

            The cover letter must contain exactly two paragraphs.

            #### Paragraph 1 (Intent + Role Alignment)

            Must include:
            - Clear expression of interest in the role and organization
            - Explicit reference to at least one key responsibility or requirement from the job description
            - Mention of 1–2 relevant capability areas aligned to the job description
            - Limit to one primary concept and at most one supporting concept
            - Do NOT list three or more parallel areas

            Must NOT include:
            - Career history summaries
            - Multiple detailed experience examples
            - Bullet-like enumeration of skills
            
            The first sentence must clearly emphasize a single dominant alignment.

            #### Paragraph 2 (Evidence + Gap + Forward Close)

            Must follow this strict structure:
            1. Opening sentences (Experience Evidence)
              - Present 2–3 relevant experiences or accomplishments
              - Each must be explicitly tied to a job requirement
            2. Gap Sentence (if applicable)
              - Clearly identify any missing or indirect capability
              - Must be ONE sentence only
              - Must not imply direct experience where none exists
            3. Closing Sentence
              - Forward-looking statement describing expected contribution or impact
              - Must NOT introduce new skills or new claims
              - Must NOT restate prior experience

            #### Gap Sentence Brevity Rule

            The gap statement must:
            - be direct and concise
            - avoid qualifiers such as:
            - “in the exact structure described”
            - “as outlined in the role”

            Prefer clean, neutral phrasing.

            ### Voice Constraint

            The cover letter must be written strictly in **first-person singular**.

            #### Requirements:

            - Use “I” and “my” only
            - Do NOT use third-person references to the candidate
            - Do NOT use resume-style narrative phrasing (e.g., “X brings”, “Y has”)
            - Do NOT refer to the candidate by name

            #### Objective:

            Ensure the output reads as a direct personal application rather than a profile summary.

            ### Abstract Language Constraint

            Avoid stacking multiple abstract qualities in a single phrase (e.g., “scalability, reliability, efficiency”).

            Instead:
            - use at most two
            - or replace with one abstract concept + one concrete indicator

            Prefer:
            - concrete outcomes, scale, or system behavior

            ### Seniority Tone Rule

            For senior roles:
            - prefer assertive, declarative statements
            - minimize justification or persuasion language

            The tone should reflect:
            - ownership
            - authority
            - prior success in similar problem spaces

            ### Job-Relevance Anchoring Requirement

            The cover letter must explicitly reference at least two distinct categories of job responsibilities, which may include:
            - technical systems or platforms
            - process ownership (e.g., planning, roadmap, execution)
            - metrics, KPIs, or outcome tracking
            - leadership or team enablement responsibilities
            - large-scale or high-traffic systems
            - shared or cross-team systems and frameworks
            
            #### Constraint:

            These must be clearly identifiable in the text and not implied vaguely.

            ### Experience Relevance Threshold

            Only include experiences that:
            - strongly map to core responsibilities, OR
            - demonstrate comparable scale, complexity, or ownership

            Do NOT include weaker or indirect examples solely to reach 2–3 items.

            It is acceptable to include only 2 experiences if they are stronger.

            ### Anti-Resume Drift Rule

            The cover letter must NOT:
            - Read like a condensed version of a resume or summary section
            - Present lists of technologies or achievements without narrative context
            - Describe the candidate externally (third-person framing)
            
            #### Required framing:

            Every sentence should follow:

            > “I did X, which relates to Y requirement in the role.”

            ### Skill Gap Handling Rule

            When required capabilities are not explicitly present in the input resume:
            - Do NOT fabricate experience
            - Do NOT imply direct proficiency

            Instead:
            - Explicitly frame as indirect exposure or transferable capability
            - Immediately connect to a closely related, verifiable experience
            - Keep the gap discussion concise (one sentence only)

            #### Skill Gap Granularity Constraint

            The gap statement must:
            - remain at the capability level, not specific internal processes
            - avoid overly narrow or administrative details

            Prefer:
            - “direct people management ownership”

            Avoid:
            - “formal annual reviews or compensation decisions”

            ### Closing Sentence Constraint

            The final sentence of the second paragraph must:
            - Be forward-looking
            - Describe expected contribution or impact
            - Relate directly to role responsibilities
            - Avoid introducing new skills or repeating prior examples

            ### Closing Impact Precision Rule

            The final sentence must:
            - reference one concrete mechanism of impact, such as:
              * shared frameworks
              * cross-team enablement
              * system consistency
              * development velocity
            AND
            - describe how that impact occurs

            Prefer:
            - “enable X by doing Y”

            Avoid:
            - general improvement statements without mechanism
            
            #### Constraint:

            Generic descriptors (e.g., “high-impact systems”, “large-scale platforms”) are insufficient unless grounded in specific context.

            ### Structural Clarity Requirement

            Ensure:
            - Paragraph 1 = intent + alignment
            - Paragraph 2 = evidence + gap + forward-looking close

            Do NOT merge these functions into a single blended narrative.

            ### Narrative Consistency Rule

            Maintain consistent first-person perspective and avoid shifts into:
            - resume summary voice
            - organizational description voice
            - external evaluator tone

            All statements must remain grounded in personal action and contribution.

            ### Anti-Generic Alignment Rule

            Do NOT use vague alignment phrases such as:
            - “aligned with my experience”
            - “closely aligned with my background”
            - “throughout my career”

            Instead:
            - explicitly name 1–2 concrete areas of experience or responsibility

            ### Sentence Complexity Constraint

            Each sentence must express one primary idea.

            Avoid:
            - multiple “which…” or “that…” clauses in a single sentence
            - combining multiple experience-to-requirement mappings in one sentence

            Prefer:
            - shorter, declarative sentences with clear focus

            ### Mapping Language Elimination Rule (Strict)

            Do NOT use transitional justification phrases such as:
            - “which reflects”
            - “which demonstrates”
            - “which highlights”

            Experience should be stated directly and confidently, without explanation.

            ### Experience Ordering Rule

            In Paragraph 2:
            - Present experiences in order of relevance to the job description, not chronology
            - The first experience must reflect the most critical responsibility of the role

            Deprioritize:
            - less relevant or indirect examples

            ### Experience Focus Consistency Rule

            Each experience sentence must focus on one type of signal:

            - leadership / influence
            OR
            - system design / architecture
            OR
            - scale / performance

            Avoid mixing multiple signal types in one sentence unless tightly integrated.

            ### Closing Sentence Simplicity Rule

            The final sentence must:
            - focus on one primary contribution theme
            - avoid listing multiple abstract qualities

            Prefer:
            - a single clear impact statement tied to the role

            Avoid:
            - stacking multiple traits (e.g., “judgment, execution, thinking”)

            ### Natural Language Constraint

            The cover letter must read as natural professional writing, not a structured mapping exercise.

            Avoid:
            - repetitive sentence patterns
            - explicit “requirement mapping” phrasing in every sentence

            Not every sentence must explicitly reference the job description, as long as alignment is clear overall.

            ### Primary Alignment Constraint (Paragraph 1)

            Paragraph 1 must emphasize one dominant responsibility or capability from the job description.
            - Secondary concepts may be mentioned, but must be subordinate
            - Avoid listing more than two parallel concepts

            The reader should clearly understand the primary focus of alignment after the first sentence.

            ### Assertion Over Explanation Rule

            Do NOT explain how experience relates to the role using phrases like:
            - “this relates to”
            - “this speaks to”
            - “this supports”

            Instead:
            - state experience and its context directly
            - allow relevance to be self-evident

            ### Semantic Redundancy Constraint

            Avoid repeating the same core concept (e.g., platform, architecture, frameworks) across multiple sentences unless:
            - each instance introduces new information

            Prefer:
            - introducing a new dimension (scale, impact, domain, outcome)

            ## Output Format (Markdown Resume)

            Output the resume in Markdown:
            - Line 1: candidate's name (becomes Title style)
            - Lines before the first # heading: contact / location lines (become Subtitle style)
            - # for top-level section headings (e.g. # Professional History)
            - ## for job/role title + organization + date lines (e.g. ## Janitor • 2015–2026 Beverly Hills High School.)
            - ### for any sub-headings if needed
            - **...** for key phrases to emphasize (Emphasis character style will be applied)
            - [display text](url) for hyperlinks — preserve URLs from the original resume exactly
            - Write each achievement or bullet point on its own line
            - Do NOT use bullet characters (-, *, •) — each line becomes a separate paragraph
            - Use blank lines between sections for readability
            - Do not use code fences, HTML, or OOXML

            ## Final Output

            Respond with a JSON object only — no markdown, no explanation — using this exact schema:

            {
              "resumeMarkdown": "Markdown string",
              "coverLetterText": "paragraph1\n\nparagraph2",
              "whyApplyText": "1-2 sentences"
            }

            ## Output Rules

            - must be valid JSON 
            - contain only the keys specified
            - no additional keys

            ### resumeMarkdown

            - full tailored resume in Markdown as described above
            - preserve structure and factual integrity
            - Ensure the resume adheres to the selected Length Control Mode
            - Ensure proportionality or constraints are respected
            - Revise if necessary to comply with all rules above
            
            ### coverLetterText

            - must have exactly 2 paragraphs separated by a blank line (\n\n).

            ### whyApplyText

            - 1-2 sentences.
            - reference:
              * organization, product, or mission if present
              * one relevant skill or experience
              * one concrete detail from the job description
            - keep concise and natural
            - avoid generic phrases like "I am excited to apply" or "This role aligns with my career goals"
            - emphasize skills and experience most relevant to the job description.

            ## Integrity Constraints

            - Do not invent:
              * skills
              * tools or systems
              * roles
              * organizations
              * certifications
              * achievements
            - Only reference capabilities explicitly present in the resume
            - Related concepts may be described, but not claimed as direct experience
            """;

        var additionalSection = additionalKeywords is { Count: > 0 }
            ? $"\n\nAdditional Keywords Confirmed by Candidate:\n{string.Join("\n", additionalKeywords.Select(k => $"- {k}"))}"
            : "";

        string raw;
        if (!string.IsNullOrEmpty(evaluation.AiResponseId))
        {
            // Turn 1 already supplied the Markdown resume and job description.
            // Append additional keywords if the user selected any.
            var continuationText = "Generate the tailored job application materials. Respond in JSON." + additionalSection;
            (_, raw) = await ExecuteWithRetryAsync(
                () => ContinueResponseAsync(modelId, apiKey, instructions, evaluation.AiResponseId, continuationText),
                "Generating materials…", onProgress);
        }
        else
        {
            // Fallback: no prior conversation — supply full context.
            var gapsSummary = evaluation.Gaps.Count > 0
                ? string.Join("\n- ", evaluation.Gaps)
                : "None identified";

            var userText = $"""
                Job Description
                Company: {job.CompanyName}
                Title: {job.JobTitle}
                Details:
                {job.JobDetails}

                Match Evaluation
                Score: {evaluation.Score}
                Gaps:
                - {gapsSummary}

                Original Resume (Markdown):
                {resumeMarkdown}{additionalSection}

                Respond in JSON.
                """;

            (_, raw) = await ExecuteWithRetryAsync(
                () => CreateResponseWithTextAsync(modelId, apiKey, instructions, userText),
                "Generating materials…", onProgress);
        }

        GeneratedMaterials result;
        try
        {
            result = JsonSerializer.Deserialize<GeneratedMaterials>(raw, _json)
                     ?? throw new InvalidOperationException("Empty response");
        }
        catch
        {
            throw new InvalidOperationException($"Could not parse AI response: {raw}");
        }

        result.ResumeMarkdown = result.ResumeMarkdown.Trim();
        return result;
    }

    // ── Instruction builders ───────────────────────────────────────────────────

    private static string BuildLengthControlSection(int sourcePageCount, int targetPageCount)
    {
        if (targetPageCount > sourcePageCount)
            return "No length constraints are applied. You may expand content as needed " +
                   "while maintaining relevance, clarity, and factual accuracy. Avoid unnecessary verbosity.";

        if (targetPageCount == 1)
            return """
                ### Length Control Mode: Single-Page Resume (Canonical Form)

                This is a strict constraint.

                - Include only the most relevant and recent experience (typically last 8–10 years)
                - Limit Experience to 3–5 roles
                - Limit each role to 2–3 achievement lines
                - Skills: concise and highly relevant (~10–12 entries)
                - Summary: 2–3 sentences

                Pruning rules:
                - Remove older or less relevant roles entirely
                - Remove low-impact or redundant achievements
                - Merge related achievements where appropriate

                If needed, remove content rather than exceed limits.
                """;

        if (targetPageCount == sourcePageCount)
            return """
                ### Length Control Mode: Fixed-Length (Zero Growth)

                - Do NOT increase content volume, roles, bullets, or Skills size

                Substitution rules:
                - Any added keyword must replace or compress existing content
                - Replace weaker terms instead of appending
                - Adding a skill requires removing or merging another
                - Expanding a bullet requires shortening/removing another

                Focus on higher keyword density and reduced redundancy.
                """;

        // targetPageCount < sourcePageCount (and not 1)
        int ratioPercent = (int)Math.Round((double)targetPageCount / sourcePageCount * 100);
        return $"""
            ### Length Control Mode: Ratio-Based Scaling

            Target ratio: ~{ratioPercent}%

            - Scale roles, bullets, and Skills proportionally
            - Remove lowest-relevance content first
            - Preserve structure and balance

            Prioritize:
            1. Required skills
            2. Relevant recent experience
            3. Impact

            Prefer removing low-value content over weakening clarity.
            """;
    }

    // ── Responses API ─────────────────────────────────────────────────────────

    // New conversation — text only.
    private Task<(string ResponseId, string Content)> CreateResponseWithTextAsync(
        string modelId, string apiKey, string instructions, string userText)
    {
        var body = new
        {
            model = modelId,
            instructions,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[] { new { type = "input_text", text = userText } }
                }
            },
            text  = new { format = new { type = "json_object" } },
            store = true,
        };
        return CallResponsesApiAsync(body, apiKey);
    }

    // Continuation turn — text only.
    private Task<(string ResponseId, string Content)> ContinueResponseAsync(
        string modelId, string apiKey, string instructions,
        string previousResponseId, string userText)
    {
        var body = new
        {
            model = modelId,
            previous_response_id = previousResponseId,
            instructions,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[] { new { type = "input_text", text = userText } }
                }
            },
            text  = new { format = new { type = "json_object" } },
            store = true,
        };
        return CallResponsesApiAsync(body, apiKey);
    }

    private async Task<(string ResponseId, string Content)> CallResponsesApiAsync(
        object body, string apiKey)
    {
        // Inject stream:true — SSE streaming means HttpClient.Timeout only races against
        // the initial connection, not body delivery, preventing long Turn 2 timeouts.
        var payload = JsonNode.Parse(JsonSerializer.Serialize(body, _json))!.AsObject();
        payload["stream"] = true;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        // HttpRequestException from SendAsync is inherently retryable — let it propagate.
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var status  = (int)response.StatusCode;
            var errBody = await response.Content.ReadAsStringAsync();

            if (RetryableHttpCodes.Contains(status))
            {
                // Respect Retry-After header (sent by OpenAI on 429).
                TimeSpan? retryAfter = null;
                if (response.Headers.RetryAfter?.Delta is { } delta)
                    retryAfter = delta;
                else if (response.Headers.RetryAfter?.Date is { } date && date > DateTimeOffset.UtcNow)
                    retryAfter = date - DateTimeOffset.UtcNow;

                throw new RetryableApiException(status, $"OpenAI API error {status}: {errBody}", retryAfter);
            }

            throw new InvalidOperationException($"OpenAI API error {status}: {errBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..].Trim();
            if (data == "[DONE]") break;

            var ev = JsonNode.Parse(data);
            switch (ev?["type"]?.GetValue<string>())
            {
                case "response.completed":
                {
                    var resp = ev["response"]
                               ?? throw new InvalidOperationException("Missing response object in response.completed event");
                    var responseId = resp["id"]?.GetValue<string>()
                                     ?? throw new InvalidOperationException("Missing response id");
                    var output  = resp["output"]?.AsArray();
                    var msg     = output?.FirstOrDefault(n => n?["type"]?.GetValue<string>() == "message");
                    var content = msg?["content"]?[0]?["text"]?.GetValue<string>()
                                  ?? throw new InvalidOperationException("Unexpected Responses API response shape");
                    return (responseId, content);
                }
                case "error":
                    throw new InvalidOperationException(
                        $"OpenAI streaming error: {ev["message"]?.GetValue<string>() ?? data}");
            }
        }

        // Stream closed without response.completed — transient; eligible for retry.
        throw new TruncatedStreamException();
    }
}
