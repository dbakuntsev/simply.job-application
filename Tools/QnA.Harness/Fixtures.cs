namespace Simply.JobApplication.Tools.QnA.Harness;

// A fixture is one fictitious applicant + employer + role triple.
// Resume Markdown follows the convention OpenAiProvider expects:
//   line 1 = name (Title)
//   next 1-2 lines = contact (Subtitle)
//   '#' = top-level section, '##' = role title line.
internal sealed record Fixture(
    string Key,                 // short slug used in session ids and file names
    string DisplayName,         // human-readable, used in summary.md
    string Domain,              // free-form, e.g. "Software / fintech SaaS"
    string OrgName,
    string OrgDescription,
    string RoleName,
    string RoleDescription,
    string TailoredResumeMarkdown);

internal static class Fixtures
{
    // NOTE: declared after Software/Events so static-field initialization
    // order doesn't leave the array holding nulls.
    public static IReadOnlyList<Fixture> All => _all;

    public static Fixture? FindByKey(string key)
        => All.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    // ── Fixture 1: technical ─────────────────────────────────────────────────
    public static Fixture Software { get; } = new(
        Key:        "software",
        DisplayName:"Senior Backend Engineer @ LedgerLoop",
        Domain:     "Software / fintech SaaS",
        OrgName:    "LedgerLoop",
        OrgDescription: """
            LedgerLoop is a Series-B fintech SaaS company building a reconciliation platform
            used by mid-market accounting teams. The product ingests transaction feeds from
            banks and ERP systems and produces audit-ready ledgers. The engineering team is
            ~40 people, distributed across North America, organized into small product squads
            that own services end-to-end.
            """,
        RoleName:   "Senior Backend Engineer (C# / .NET)",
        RoleDescription: """
            We are hiring a Senior Backend Engineer to own design and delivery of services
            powering our reconciliation pipeline. You will work in C# / .NET 8, on Azure,
            with PostgreSQL and Kafka. You will partner with product and data engineering
            to ship features used by paying customers daily.

            Responsibilities:
            - Design, build, and operate C# / .NET services that ingest and reconcile
              high-volume financial transaction data.
            - Own services end-to-end: design, implementation, deployment, on-call.
            - Improve reliability, throughput, and observability of the reconciliation
              pipeline.
            - Mentor mid-level engineers and contribute to engineering standards.

            Required qualifications:
            - 6+ years building production backend services in C# / .NET.
            - Strong PostgreSQL experience including query tuning.
            - Experience with event-driven architectures (Kafka, RabbitMQ, or similar).
            - Comfortable owning services on Azure (App Service, Functions, or AKS).

            Preferred:
            - Prior fintech, payments, or accounting-domain experience.
            - Experience leading a service migration or modernization.
            - Familiarity with audit, SOC 2, or regulated data handling.

            Logistics:
            - Remote within the United States or Canada.
            - Eligibility to work in the US or Canada is required; we do not sponsor at this time.
            - Compensation: competitive base + equity, discussed during recruiter screen.
            """,
        TailoredResumeMarkdown: """
            Dana Whitfield
            Toronto, ON · dana.whitfield@example.com · linkedin.com/in/dana-whitfield-example

            Senior backend engineer with eight years building production C# / .NET services. Specialization in event-driven reconciliation and high-volume data pipelines on Azure. Most recently owned the rebuild of a settlement service that processed 18M transactions per month for a payments platform.

            # Skills
            C# / .NET 8, ASP.NET Core, PostgreSQL, Kafka, RabbitMQ, Azure (App Service, Functions, AKS), Docker, OpenTelemetry, SQL query tuning, service-level reliability

            # Experience

            ## Senior Backend Engineer • 2023–Present • Northwind Payments
            Owned design and delivery of the settlement reconciliation service rebuild on .NET 8 and Azure Functions.
            Drove a migration from a monolithic SQL Server job to an event-driven pipeline on Kafka, raising daily settlement throughput by 4x.
            Tuned PostgreSQL query plans for the new ledger store, reducing p95 reconciliation latency from 9s to 1.4s.
            Acted as on-call lead for the payments squad; mentored two mid-level engineers.

            ## Backend Engineer • 2020–2023 • Northwind Payments
            Built C# / .NET microservices for transaction ingestion and merchant settlement.
            Led the introduction of OpenTelemetry across six services, replacing ad-hoc logging.
            Partnered with the data team to design schemas for daily settlement audit trails.

            ## Software Engineer • 2017–2020 • Crestmark Software
            Developed ASP.NET Core APIs for a logistics SaaS product used by ~120 enterprise customers.
            Owned the deployment pipeline migration from Jenkins to Azure DevOps.

            # Education
            B.Sc. Computer Science, University of Toronto, 2017

            # Authorization
            Authorized to work in Canada; eligible to work in the United States.
            """);

    // ── Fixture 2: non-technical ─────────────────────────────────────────────
    public static Fixture Events { get; } = new(
        Key:        "events",
        DisplayName:"Events & Volunteer Coordinator @ Riverside Arts Council",
        Domain:     "Nonprofit / community arts",
        OrgName:    "Riverside Arts Council",
        OrgDescription: """
            Riverside Arts Council is a regional nonprofit that produces community art
            festivals, education programs for K-12 students, and a small grants program
            for local artists. Annual budget ~ $2.4M; staff of 14; supported by ~200
            seasonal volunteers across the year.
            """,
        RoleName:   "Events & Volunteer Coordinator",
        RoleDescription: """
            We are hiring an Events & Volunteer Coordinator to plan and run the council's
            community events and to grow and support our volunteer program. You will work
            closely with the Programs Director and report into the Executive Director.

            Responsibilities:
            - Plan and execute three annual flagship festivals (each 800–1,500 attendees)
              and ~12 smaller community events per year.
            - Recruit, onboard, schedule, and recognize volunteers across all programs.
            - Coordinate vendors, permits, and venue logistics.
            - Track event budgets and reconcile post-event spend.
            - Represent the council at community partner meetings.

            Required qualifications:
            - 3+ years coordinating public events or volunteer programs.
            - Experience managing event budgets in the $20K–$100K range.
            - Comfort with volunteer management software (e.g., VolunteerLocal, Better Impact).
            - Strong written and verbal communication.

            Preferred:
            - Prior nonprofit experience.
            - Familiarity with grant reporting.
            - Bilingual (English / Spanish).

            Logistics:
            - On-site at our Riverside office, with evening and weekend work for events.
            - Background check required before start date.
            - Salary range $52K–$60K depending on experience.
            """,
        TailoredResumeMarkdown: """
            Morgan Alvarez
            Riverside, CA · morgan.alvarez@example.com · (555) 555-0143

            Events and volunteer coordinator with five years running community-facing programs for arts and education nonprofits. Specialization in multi-day festival logistics and volunteer scheduling at scale. Most recently coordinated 220 volunteers across a three-day outdoor festival serving 4,200 attendees.

            # Skills
            Event planning, volunteer recruitment, volunteer scheduling, vendor coordination, permit and venue logistics, budget tracking, donor stewardship, VolunteerLocal, Better Impact, bilingual English / Spanish

            # Experience

            ## Programs Coordinator • 2023–Present • Mariposa Youth Arts
            Planned and ran the annual Mariposa Family Festival (3 days, 4,200 attendees) including vendors, permits, and 220 volunteers.
            Rebuilt the volunteer onboarding process in Better Impact, reducing average time-to-first-shift from 18 days to 6.
            Tracked event budgets across nine programs totaling $310K annually and reconciled spend with the finance manager.

            ## Volunteer Coordinator • 2020–2023 • Bayside Literacy Project
            Recruited and scheduled ~120 active volunteers across after-school tutoring sites.
            Coordinated quarterly volunteer recognition events with budgets of $4K–$8K each.
            Wrote volunteer narratives that fed quarterly grant reports to the United Way and two private foundations.

            ## Events Assistant • 2018–2020 • Greenleaf Community Center
            Supported planning of monthly community events (~120 attendees each).
            Managed vendor outreach for the annual Greenleaf Holiday Market.

            # Education
            B.A. Communications, California State University, 2018

            # Languages
            English (native), Spanish (professional working proficiency)
            """);

    // Declared at end so the static initializer runs after Software and Events.
    private static readonly Fixture[] _all = { Software, Events };
}
