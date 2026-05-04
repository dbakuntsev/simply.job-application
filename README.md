# Simply Opportunity Tracker

A browser-based opportunity management system for structured searches in any sector — jobs, academic positions, contracts, government roles, and more. Track organizations, contacts, and opportunities through every stage of the process. When you are ready to apply, generate tailored application materials using your own AI API key — entirely in your browser, with no data leaving your device.

**[Live App on GitHub Pages](https://dbakuntsev.github.io/simply.job-application/)**

---

## Overview

Simply Opportunity Tracker combines a full opportunity pipeline with an AI-powered Evaluate & Generate workflow:

- **Pipeline management** — Organizations → Contacts → Opportunities → Correspondence, tracked from first contact to final decision.
- **Evaluate & Generate** — Score your fit against any role, identify strengths and gaps, then generate a tailored resume, cover letter, and why-apply summary.
- **Ad-hoc mode** — Use Evaluate & Generate as a standalone tool without creating organization or opportunity records first.
- **Installable PWA** — Install as a desktop or mobile app for offline access and a native app experience.

All data is stored in your browser's IndexedDB. The only external calls are to your chosen AI provider using your own key.

---

## Features

### Opportunity Pipeline
- Organizations with contacts, industry, size, website, and LinkedIn
- Opportunities with stage tracking (Open → Applied → Interview → Offer → Accepted, and more), role description, work arrangement, compensation range, and required / preferred qualifications
- AI-powered qualification extraction from role descriptions
- Full change history per opportunity (every edit recorded with old values)
- Correspondence log per opportunity: emails, phone calls, video calls, interviews, resume submissions, and more — with file attachments and contact associations

### Evaluate & Generate
- Match score (Poor / Fair / Good / Excellent) with strengths, gaps, and ATS keyword suggestions
- Tailored resume exported as DOCX — hyperlinks and formatting preserved
- Two-paragraph cover letter (DOCX) and concise why-apply summary
- Pre-fill from a linked Opportunity, or run in ad-hoc mode with free-text input
- Session history retained indefinitely; browse all past sessions from the History sub-menu

### My Resumes
- Upload and manage named resumes with full version history
- Side-by-side diff viewer to compare any two versions
- Restore any prior version; auto-select latest version in Evaluate & Generate

### Progressive Web App (PWA)
- Install as a desktop or mobile app directly from the browser — no app store required
- Full offline support: all pipeline and history features work without a network connection (AI generation requires a live connection to your AI provider)
- Automatic update notifications: a banner appears when a new version is available; reload applies the update instantly
- Downloads in standalone mode use the File System Access API (`showSaveFilePicker`) when available, with a blob-URL fallback and toast notification

### Data Integrity
- Optimistic locking with version conflict detection across browser tabs
- Cross-tab live updates via BroadcastChannel — all open tabs stay in sync
- Cascade deletes handled in single IndexedDB transactions
- Navigation guards on all edit forms — unsaved changes are never silently lost
- Full data export as a compressed backup (.json.gz); import to restore or migrate to another browser or device

### Privacy
- All data stored in browser IndexedDB only — nothing persisted server-side
- Configurable AI provider (currently OpenAI)
- API key stored in browser local storage only

---

## Getting Started

### Prerequisites

- An API key from a supported AI provider (e.g. [OpenAI](https://platform.openai.com/api-keys))
- A resume in DOCX format

### Using the Hosted Version

1. Open the [live demo](https://dbakuntsev.github.io/simply.job-application/)
2. Go to **Settings** and enter your AI provider API key
3. Go to **My Resumes** and upload your resume in DOCX format
4. Go to **Organizations**, add a target organization, and create an Opportunity
5. Open the Opportunity and click **Evaluate & Generate**

Or skip straight to **Evaluate & Generate** for a quick ad-hoc session.

### Installing as a Desktop App

The app can be installed as a PWA from any Chromium-based browser (Chrome, Edge, Arc) or Safari on iOS/macOS:

- **Chrome / Edge**: click the install icon (⊕) in the address bar, or use the install button on the page
- **Safari on iOS**: tap Share → Add to Home Screen

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
| Organizations, contacts, opportunities, correspondence | Browser IndexedDB only — never leaves your device |
| Resumes, evaluation results, generated files | Browser IndexedDB only |
| AI provider API key | Browser local storage only |
| AI prompts and resume content | Sent directly to your chosen AI provider using your key, subject to that provider's data policy |
| Exported backup files | Downloaded to your local filesystem — never transmitted anywhere |

---

## Tech Stack

- [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) (.NET 8)
- [Bootstrap 5](https://getbootstrap.com/) with [Cosmo theme](https://bootswatch.com/cosmo/)
- [Radzen.Blazor](https://blazor.radzen.com/) — dropdowns, tags, markdown rendering
- [BlazorMonaco](https://github.com/serdarciplak/BlazorMonaco) — markdown editor and diff viewer
- [OpenAI Responses API](https://platform.openai.com/docs/api-reference/responses) with server-sent events streaming
- Browser IndexedDB for local persistence; BroadcastChannel for cross-tab sync; Web Locks for write serialization
- Service Worker (production-only) for offline caching and PWA update flow
- GitHub Actions for release packaging and GitHub Pages deployment

---

## Project Structure

```
Pages/
  WelcomePage.razor              Landing page (smart redirect for returning users)
  OrganizationListPage.razor     Organizations list with search
  NewOrganizationPage.razor      Add organization form
  OrganizationDetailPage.razor   Organization detail with contacts, opportunities, and sessions
  AddOpportunityPage.razor       Add opportunity form
  OpportunityDetailPage.razor    Opportunity detail with correspondence and E&G sessions
  OpportunityListPage.razor      Cross-organization opportunity report
  EvaluateAndGeneratePage.razor  AI match scoring and material generation
  HistoryPage.razor              Session history list
  HistoryDetailPage.razor        Session detail view
  MyResumesPage.razor            Resume library with version management
  SettingsPage.razor             API key, model, storage, and data export/import

Components/
  PwaUpdateBanner.razor          Dismissible banner shown when a new app version is waiting
  MarkdownEditor.razor           Monaco-based markdown editor
  TagPicker.razor                Select2-based tag picker for role labels
  NavigationGuardModal.razor     Unsaved-changes confirmation modal
  ConflictAlertModal.razor       Cross-tab version conflict alert
  DeletionAlertModal.razor       Remote deletion warning modal
  ConfirmDeleteModal.razor       Generic delete confirmation modal
  ResumeDiffModal.razor          Side-by-side DOCX diff viewer

Services/
  IndexedDbService.cs            All IndexedDB reads and writes
  DataSyncService.cs             BroadcastChannel cross-tab notifications
  DocxService.cs                 DOCX ↔ Markdown extraction and generation
  PwaService.cs                  PWA state: install prompt, standalone detection, update detection
  AppStartupService.cs           DB migration, lookup seeding, startup checks
  AI/
    IAiProvider.cs               Provider interface
    OpenAi/OpenAiProvider.cs     OpenAI implementation

wwwroot/
  service-worker.js              Dev stub (no-op install/activate)
  service-worker.published.js    Production SW: precache, offline fallback, update flow
  manifest.webmanifest           PWA manifest (version stamped from GitHub release tag at deploy)
  js/pwa.js                      Install prompt capture, standalone detection, SW update signalling
```

---

## License

[MIT](LICENSE) © 2026 Dmitry Bakuntsev
