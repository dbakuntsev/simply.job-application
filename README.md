# Simply Job Application

AI-powered resume tailoring and cover letter generation that runs entirely in your browser. Paste a job description, upload your resume, get a match score with ATS keyword suggestions, then generate a tailored DOCX resume and cover letter — all via your own OpenAI API key. No server. No data leaves your browser.

**[Live Demo](https://dbakuntsev.github.io/simply.job-application/)**

---

## How It Works

1. **Evaluate** — Paste a job description and select your resume. The AI scores how well you match the role, surfaces your strongest qualifications, and flags gaps.
2. **Add Keywords** — Review AI-suggested ATS keywords grouped by category. Confirm the ones you genuinely have, add your own, and optionally re-run the evaluation to see the updated score.
3. **Generate** — Download a tailored resume (DOCX), a two-paragraph cover letter (DOCX), and a concise "why apply" summary — all optimised for the specific role.

All AI calls go directly from your browser to the OpenAI API using your own key. Nothing is routed through any server operated by this application.

---

## Features

- Match score (0–100) with strengths and gaps
- ATS keyword suggestions grouped by category
- Tailored resume exported as DOCX — hyperlinks and formatting preserved
- Cover letter and "why apply" summary
- Resume file library for reuse across multiple applications
- Session history with full evaluation and generated materials
- All data stored in browser IndexedDB — nothing persisted server-side
- Configurable AI model (GPT-4.1, GPT-4.1 mini, GPT-5.4, GPT-5.4 mini)

---

## Getting Started

### Prerequisites

- An [OpenAI API key](https://platform.openai.com/api-keys)
- A resume in DOCX format

### Using the Hosted Version

1. Open the [live demo](https://dbakuntsev.github.io/simply.job-application/)
2. Go to **Settings** and enter your OpenAI API key
3. Go to **Evaluate & Generate**, upload your resume, paste a job description, and click **Evaluate**

### Running Locally

```bash
git clone https://github.com/dbakuntsev/simply.job-application.git
cd simply.job-application
dotnet run
```

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## Privacy

| Data | Where it goes |
|------|---------------|
| Resume, job descriptions, evaluations, generated files | Browser IndexedDB only — never leaves your device |
| OpenAI API key | Browser local storage only |
| AI prompts and resume content | Sent directly to OpenAI API using your key, subject to [OpenAI's data policy](https://openai.com/policies/api-data-usage-policies) |

---

## Tech Stack

- [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) (.NET 8)
- [Bootstrap 5](https://getbootstrap.com/) with [Cosmo theme](https://bootswatch.com/cosmo/)
- [OpenAI Responses API](https://platform.openai.com/docs/api-reference/responses) with server-sent events streaming
- Browser IndexedDB for local persistence
- GitHub Actions for release packaging and GitHub Pages deployment

---

## Project Structure

```
Pages/
  WelcomePage.razor          Landing page
  EvaluateAndGeneratePage.razor  Main three-step workflow
  HistoryPage.razor          Session history list
  HistoryDetailPage.razor    Session detail view
  FilesLibraryPage.razor     Resume file library
  SettingsPage.razor         API key and model configuration

Services/
  IndexedDbService.cs        IndexedDB read/write
  DocxService.cs             DOCX → Markdown extraction and generation
  AI/
    IAiProvider.cs           Provider interface
    OpenAi/OpenAiProvider.cs OpenAI implementation
```

---

## License

[MIT](LICENSE) © 2026 Dmitry Bakuntsev
