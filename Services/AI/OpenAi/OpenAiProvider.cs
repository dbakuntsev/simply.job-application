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

    // ── Public interface ──────────────────────────────────────────────────────

    public async Task<MatchEvaluation> EvaluateMatchAsync(
        JobDescription job, string resumeMarkdown, string modelId, string apiKey,
        IReadOnlyList<string>? additionalKeywords = null)
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

        var (responseId, raw) = await CreateResponseWithTextAsync(modelId, apiKey, instructions, userText);

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
        IReadOnlyList<string>? additionalKeywords = null)
    {
        const string instructions = """
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

            The summary should:
            - highlight 2–3 key skills or capabilities aligned with the job description
            - reflect relevant responsibilities or domain context
            - prioritize clarity and keyword relevance

            Do not remove the summary unless it is clearly redundant.
            
            If a professional summary exists, place it immediately after the contact/location lines and before the first # section heading. 

            If content must be shortened, prioritize trimming older experience before removing the summary.

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
            - max 2 printed pages
            - preserve structure and factual integrity

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
            - emphasise skills and experience most relevant to the job description.

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
            (_, raw) = await ContinueResponseAsync(
                modelId, apiKey, instructions, evaluation.AiResponseId, continuationText);
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

            (_, raw) = await CreateResponseWithTextAsync(modelId, apiKey, instructions, userText);
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

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"OpenAI API error {(int)response.StatusCode}: {err}");
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
                    var output = resp["output"]?.AsArray();
                    var msg    = output?.FirstOrDefault(n => n?["type"]?.GetValue<string>() == "message");
                    var content = msg?["content"]?[0]?["text"]?.GetValue<string>()
                                  ?? throw new InvalidOperationException("Unexpected Responses API response shape");
                    return (responseId, content);
                }
                case "error":
                    throw new InvalidOperationException(
                        $"OpenAI streaming error: {ev["message"]?.GetValue<string>() ?? data}");
            }
        }

        throw new InvalidOperationException("Responses API stream ended without a response.completed event");
    }
}
