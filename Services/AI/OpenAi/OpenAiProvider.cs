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
        JobDescription job, string resumeMarkdown, string modelId, string apiKey)
    {
        const string instructions = """
            You are an expert resume analyst.

            The candidate's resume is provided in Markdown format converted from their original DOCX file.
            Heading levels (#, ##) reflect the document's section structure. **Bold** spans indicate
            emphasized phrases. Links preserve the original hyperlinks.

            Evaluate how well the candidate matches the job description. Scoring definition:
            - Excellent: Candidate meets most required qualifications and many preferred ones.
            - Good: Candidate meets the majority of required qualifications but may lack some preferred skills.
            - Fair: Candidate meets some required qualifications but lacks several key requirements.
            - Poor: Candidate lacks most required qualifications.

            Internally identify:
            - required technologies
            - required responsibilities
            - required experience level
            - preferred qualifications
            Then evaluate match.

            Base the evaluation only on information explicitly present in the resume.

            Compare the job description requirements directly against evidence in the resume.

            Treat as requirements only items that describe skills, technologies, years of experience, certifications, or responsibilities required to perform the role.

            Pay special attention to matching programming languages, frameworks, cloud platforms, and core technologies.

            Technologies include programming languages, frameworks, cloud platforms, databases, and developer tools.

            Consider both technology match and responsibility scope when determining the score.

            Consider whether the candidate's experience level aligns with the seniority implied in the job description.

            Consider years of experience, leadership responsibilities, and role titles when evaluating seniority alignment.

            Evaluate whether the candidate's responsibilities reflect similar scope or impact as the role described.

            Consider common synonyms and variations of technologies when comparing resume and job description.

            If the job description lists a single specific programming language, framework, or technology that is explicitly required 
            but there is no evidence of it in the resume, this should be considered a significant gap, and the score should not be higher
            than "Fair".
            
            If the candidate meets most required technologies and responsibilities, the score should not be lower than "Good".

            If the candidate meets most required technologies but lacks some responsibilities, the score should typically be "Good" rather than "Fair".

            Respond with a JSON object only — no markdown, no explanation — using this exact schema:
            {
              "score": "Poor" | "Fair" | "Good" | "Excellent",
              "gaps": ["string", ...],
              "strengths": ["string", ...],
              "isGoodMatch": true | false
            }
            Rules:
            - "gaps" lists specific qualifications, requirements, or experience areas where the candidate is poorly matched.
            - Only list gaps for required qualifications or clearly critical responsibilities.
            - Only report gaps that are explicitly mentioned in the job description.
            - Each gap should reference a specific requirement, technology, or responsibility from the job description.
            - Limit the gap list to the 5 most important gaps.
            - Phrase gaps as absence of evidence in the resume, not definitive lack of skill. Quote the relevant requirement from the job description in each gap.
            - Prioritize gaps in this order:
              * Missing required technologies
              * Missing critical responsibilities
              * Missing required experience level
            - Avoid duplicate or overlapping gaps.
            - "strengths" should identify qualifications or experience that clearly align with the job description requirements.
            - Limit strengths to the 5 most relevant matches.
            - Prefer strengths that correspond directly to technologies or responsibilities mentioned in the job description.
            - Prefer strengths demonstrated in recent roles when possible.
            - Each strength should reference a specific technology, responsibility, or qualification from the job description.
            - "isGoodMatch" must be true only when score is "Good" or "Excellent".
            - Output must contain valid JSON, must match schema exactly and must contain no additional fields.
            """;

        var userText = $"""
            Job Description
            Company: {job.CompanyName}
            Title: {job.JobTitle}
            Details:
            {job.JobDetails}

            Candidate Resume (Markdown):
            {resumeMarkdown}

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
        JobDescription job, string resumeMarkdown, MatchEvaluation evaluation, string modelId, string apiKey)
    {
        const string instructions = """
            You are an expert resume writer and career coach.

            Generate tailored job application materials based on the candidate's resume and the job description.

            Inputs available in this conversation:
            1. Job description text
            2. Original resume in Markdown format (structure and formatting preserved from the original DOCX)

            The Markdown resume uses these conventions:
            - Line 1: candidate name (Title style)
            - Next 1-2 lines: contact / location lines (Subtitle style)
            - If additional paragraph text appears before the first # heading, treat it as the candidate's professional summary.
            - # headings: top-level resume sections
            - ## headings: job title + company + date lines
            - **...** spans: emphasized phrases (Emphasis character style)
            - [text](url): hyperlinks from the original resume

            Internally identify:
            - top 5 required technologies
            - top 3 responsibilities
            - seniority level
            - industry or domain context, such as:
              * fintech
              * healthcare
              * developer tools
              * enterprise SaaS
              * AI/ML
              * cloud infrastructure
              * consumer apps
            Use this context when framing experience. Use domain context to emphasize relevant industry experience when present.

            Before generating outputs, internally determine:
            - job_keywords
            - resume_matching_keywords
            - resume_missing_keywords
            - top_experiences_to_highlight
            - experiences_to_deemphasize
            Then perform rewriting.

            When rewriting bullet points, naturally incorporate matching job keywords where they accurately describe the candidate's experience.

            When rewriting bullet points, prioritize including resume_matching_keywords where they accurately describe the work performed.

            Avoid artificial keyword repetition. Each keyword should appear only where it naturally fits the described work.

            Prioritize terminology used in the job description when describing equivalent experience.

            Prioritize highlighting experience from the last 5–8 years when emphasizing technologies and achievements.

            Prioritize the following order when tailoring:
            1. Explicit required technologies (directly listed in the job description)
            2. Explicit preferred technologies (directly listed in the job description)
            3. Implied technologies from responsibilities
            4. Closely related technologies
            5. General transferable skills

            Technologies must only be counted if explicitly named tools, languages, frameworks, or platforms appear in the job description.

            Technologies include programming languages, frameworks, libraries, cloud platforms, databases, and developer tools.

            Methodologies or concepts (e.g., Agile, distributed systems, REST architecture) should not be counted as technologies.

            Maintain the same high-level section structure as the original resume unless reordering improves relevance.

            Only reorder sections if doing so clearly improves ATS keyword visibility (for example moving Skills above Experience).

            If the original resume contains a professional summary paragraph between the contact information and the first section heading,
            retain this summary in the tailored resume and rewrite it to highlight the most relevant technologies, responsibilities,
            and domain context from the job description. Do not remove the summary unless it is clearly redundant.

            If a professional summary exists, place it immediately after the contact/location lines and before the first # section heading. 
            The summary should be 2–4 concise sentences and include 2–3 key technologies or capabilities that appear in both the resume and 
            the job description.

            Do not remove, change or rephrase:
            - applicant's name
            - contact information
            - company names
            - job titles
            - employment dates

            Do not add seniority modifiers (e.g., Senior, Lead, Principal) to job titles unless they appear in the original resume.

            Do not change degree names, institutions, graduation dates, or certifications.

            Optimize the resume for Applicant Tracking Systems (ATS):
            - Use clear job-relevant keywords
            - Avoid unusual formatting
            - Prefer simple structures
            - Use measurable outcomes when present in the original resume
            - Prefer standard section titles (Skills, Experience, Education)
            - When modifying professional experience:
              * Move the most relevant bullet points to the top of each role
              * Less relevant bullets may be shortened or removed
            - If the resume contains a professional summary or profile section, rewrite it to emphasize the top technologies and responsibilities from the job description.

            If the job description includes skills not present in the resume:
            - Do NOT claim experience with them.
            - Instead emphasize adjacent technologies or transferable experience.

            The cover letter is comprised of 2 paragraphs:
            - Paragraph 1:
              * State interest in the role
              * Mention the company or product
              * Mention a specific element from the job description (technology, product, or responsibility)
              * Reference 1–2 key job requirements
            - Paragraph 2:
              * Highlight 2–3 relevant experiences
              * Briefly address any skill gap with transferable expertise
              * Avoid repeating resume bullet points
              * Focus on impact, alignment with role responsibilities, and enthusiasm for company mission
              * End with a forward-looking statement

            For the tailored resume, output the content in Markdown using these conventions:
            - Line 1: candidate's name (becomes Title style)
            - Lines before the first # heading: contact / location lines (become Subtitle style)
            - # for top-level section headings (e.g. # Professional History)
            - ## for job title + company + date lines (e.g. ## Chief Architect • 2015–2026 TeamFusion, Inc.)
            - ### for any sub-headings if needed
            - **...** for key phrases to emphasize (Emphasis character style will be applied)
            - [display text](url) for hyperlinks — preserve URLs from the original resume exactly
            - Write each achievement or bullet point on its own line
            - Do NOT use bullet characters (-, *, •) — each line becomes a separate paragraph
            - Use blank lines between sections for readability
            - Do not use code fences, HTML, or OOXML

            Respond with a JSON object only — no markdown, no explanation — using this exact schema:
            {
              "resumeMarkdown": "Markdown string",
              "coverLetterText": "paragraph1\n\nparagraph2",
              "whyApplyText": "1-2 sentences"
            }
            Rules:
            - Output must be valid JSON and contain only the keys specified. No additional keys.
            - resumeMarkdown must contain the full tailored resume in Markdown as described above.
            - The tailored resume must be suitable for max 2 printed pages. If content must be shortened to meet the two-page limit, prioritize shortening older job descriptions before removing the professional summary.
            - coverLetterText must have exactly 2 paragraphs separated by a blank line (\n\n).
            - whyApplyText is 1-2 sentences on why this position is a great fit.
            - whyApplyText should:
              * reference the company, mission, product, or technology stack if present
              * mention one relevant skill or experience
              * reference one concrete detail from the job description
              * remain conversational and concise
              * avoid generic phrases like "I am excited to apply" or "This role aligns with my career goals"
            - Emphasise skills and experience most relevant to the job description.
            - Do not invent skills, technologies, job roles, companies, certifications, or achievements not present in the original resume.
              * You may only reference technologies that appear in the resume.
              * You may describe related concepts, but not claim use of specific tools unless present.
            - Only claim experience with technologies that appear explicitly in the resume.
            """;

        string raw;
        if (!string.IsNullOrEmpty(evaluation.AiResponseId))
        {
            // Turn 1 already supplied the Markdown resume and job description.
            // Just ask for generation — no need to resend context.
            (_, raw) = await ContinueResponseAsync(
                modelId, apiKey, instructions, evaluation.AiResponseId,
                "Generate the tailored job application materials. Respond in JSON.");
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
                {resumeMarkdown}

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
