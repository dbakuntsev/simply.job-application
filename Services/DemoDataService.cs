using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services;

public class DemoDataService : IDemoDataService
{
    private readonly IIndexedDbService _db;
    private readonly IDocxService _docx;

    public DemoDataService(IIndexedDbService db, IDocxService docx)
    {
        _db = db;
        _docx = docx;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<bool> IsDatabaseEmptyAsync()
    {
        if ((await _db.GetAllOrganizationsAsync()).Count > 0) return false;
        if ((await _db.GetAllSessionsAsync()).Count > 0) return false;
        if ((await _db.GetAllBaseResumesAsync()).Count > 0) return false;
        return true;
    }

    public async Task LoadDemoDataAsync()
    {
        await _db.ClearAllUserDataAsync();

        var now = DateTime.UtcNow;

        // ── Base Resume ───────────────────────────────────────────────────────
        var baseResumeDocxBytes = _docx.GenerateCoverLetterDocx(ResumeMarkdown);

        var resumeId        = Guid.NewGuid().ToString();
        var resumeVersionId = Guid.NewGuid().ToString();

        await _db.SaveBaseResumeAsync(new BaseResume
        {
            Id        = resumeId,
            Version   = 1,
            Name      = "Jordan Lee — Software Engineer",
            CreatedAt = now.AddDays(-120),
            UpdatedAt = now.AddDays(-120),
        });
        await _db.SaveBaseResumeVersionAsync(new BaseResumeVersion
        {
            Id             = resumeVersionId,
            Version        = 1,
            ResumeId       = resumeId,
            VersionNumber  = 1,
            FileDataBase64 = Convert.ToBase64String(baseResumeDocxBytes),
            FileName       = "Jordan_Lee_Resume.docx",
            Notes          = null,
            UploadedAt     = now.AddDays(-120),
        });

        // ── Organizations ─────────────────────────────────────────────────────
        var apexId      = Guid.NewGuid().ToString();
        var cloudNovaId = Guid.NewGuid().ToString();
        var dataPulseId = Guid.NewGuid().ToString();

        await _db.SaveOrganizationAsync(new Organization
        {
            Id          = apexId,
            Version     = 1,
            Name        = "Apex Systems",
            Description = "Apex Systems is a leading enterprise software company specializing in cloud-based workforce management and business automation solutions, trusted by Fortune 500 companies across 30+ countries.",
            Industry    = "Technology",
            Size        = "5,000–10,000",
            Website     = "https://apexsystems.example.com",
            LinkedIn    = "https://linkedin.com/company/apexsystems",
            CreatedAt   = now.AddDays(-90),
            UpdatedAt   = now.AddDays(-90),
        });
        await _db.SaveOrganizationAsync(new Organization
        {
            Id          = cloudNovaId,
            Version     = 1,
            Name        = "CloudNova",
            Description = "CloudNova provides cloud infrastructure and developer tooling for modern engineering teams. Known for its developer-first culture and best-in-class managed Kubernetes platform.",
            Industry    = "Technology",
            Size        = "500–1,000",
            Website     = "https://cloudnova.example.com",
            LinkedIn    = "https://linkedin.com/company/cloudnova",
            CreatedAt   = now.AddDays(-90),
            UpdatedAt   = now.AddDays(-90),
        });
        await _db.SaveOrganizationAsync(new Organization
        {
            Id          = dataPulseId,
            Version     = 1,
            Name        = "DataPulse Analytics",
            Description = "DataPulse Analytics builds real-time data pipelines and analytics tooling for e-commerce and retail brands. Series A startup with aggressive growth plans.",
            Industry    = "Technology",
            Size        = "50–200",
            Website     = "https://datapulse.example.com",
            LinkedIn    = "https://linkedin.com/company/datapulse-analytics",
            CreatedAt   = now.AddDays(-85),
            UpdatedAt   = now.AddDays(-85),
        });

        // ── Contacts ──────────────────────────────────────────────────────────
        var sarahId   = Guid.NewGuid().ToString();
        var michaelId = Guid.NewGuid().ToString();
        var emilyId   = Guid.NewGuid().ToString();
        var jamesId   = Guid.NewGuid().ToString();

        await _db.SaveContactAsync(new Contact
        {
            Id             = sarahId,
            Version        = 1,
            OrganizationId = apexId,
            FullName       = "Sarah Chen",
            Title          = "Technical Recruiter",
            Email          = "s.chen@apexsystems.example.com",
            Phone          = "(512) 555-0142",
            CreatedAt      = now.AddDays(-88),
            UpdatedAt      = now.AddDays(-88),
        });
        await _db.SaveContactAsync(new Contact
        {
            Id             = michaelId,
            Version        = 1,
            OrganizationId = apexId,
            FullName       = "Michael Torres",
            Title          = "Engineering Manager",
            Email          = "m.torres@apexsystems.example.com",
            CreatedAt      = now.AddDays(-88),
            UpdatedAt      = now.AddDays(-88),
        });
        await _db.SaveContactAsync(new Contact
        {
            Id             = emilyId,
            Version        = 1,
            OrganizationId = cloudNovaId,
            FullName       = "Emily Roberts",
            Title          = "Talent Acquisition",
            Email          = "e.roberts@cloudnova.example.com",
            CreatedAt      = now.AddDays(-87),
            UpdatedAt      = now.AddDays(-87),
        });
        await _db.SaveContactAsync(new Contact
        {
            Id             = jamesId,
            Version        = 1,
            OrganizationId = dataPulseId,
            FullName       = "James Kim",
            Title          = "CTO",
            Email          = "j.kim@datapulse.example.com",
            Phone          = "(512) 555-0231",
            CreatedAt      = now.AddDays(-83),
            UpdatedAt      = now.AddDays(-83),
        });

        // ── Opportunities ─────────────────────────────────────────────────────
        var apexSfeId   = Guid.NewGuid().ToString();
        var apexStaffId = Guid.NewGuid().ToString();
        var cnFsdId     = Guid.NewGuid().ToString();
        var dpSeId      = Guid.NewGuid().ToString();
        var dpBeId      = Guid.NewGuid().ToString();

        await _db.SaveOpportunityAsync(new Opportunity
        {
            Id                      = apexSfeId,
            Version                 = 1,
            OrganizationId          = apexId,
            Role                    = "Senior Frontend Engineer",
            Stage                   = OpportunityStage.Interview,
            WorkArrangement         = WorkArrangement.Hybrid,
            CompensationRange       = "$145,000–$175,000",
            PostingUrls             = ["https://apexsystems.example.com/careers/senior-frontend-engineer"],
            RoleDescription         = RdApexSfe,
            RequiredQualifications  = ReqApexSfe,
            PreferredQualifications = PrefApexSfe,
            CreatedAt               = now.AddDays(-78),
            UpdatedAt               = now.AddDays(-63),
        });
        await _db.SaveOpportunityAsync(new Opportunity
        {
            Id                      = apexStaffId,
            Version                 = 1,
            OrganizationId          = apexId,
            Role                    = "Staff Software Engineer",
            Stage                   = OpportunityStage.Applied,
            WorkArrangement         = WorkArrangement.Remote,
            CompensationRange       = "$175,000–$210,000",
            PostingUrls             = ["https://apexsystems.example.com/careers/staff-software-engineer"],
            RoleDescription         = RdApexStaff,
            RequiredQualifications  = ReqApexStaff,
            PreferredQualifications = PrefApexStaff,
            CreatedAt               = now.AddDays(-70),
            UpdatedAt               = now.AddDays(-63),
        });
        await _db.SaveOpportunityAsync(new Opportunity
        {
            Id                      = cnFsdId,
            Version                 = 1,
            OrganizationId          = cloudNovaId,
            Role                    = "Full-Stack Developer",
            Stage                   = OpportunityStage.Offer,
            WorkArrangement         = WorkArrangement.Remote,
            CompensationRange       = "$130,000–$160,000",
            PostingUrls             = ["https://cloudnova.example.com/jobs/full-stack-developer"],
            RoleDescription         = RdCnFsd,
            RequiredQualifications  = ReqCnFsd,
            PreferredQualifications = PrefCnFsd,
            CreatedAt               = now.AddDays(-60),
            UpdatedAt               = now.AddDays(-21),
        });
        await _db.SaveOpportunityAsync(new Opportunity
        {
            Id                      = dpSeId,
            Version                 = 1,
            OrganizationId          = dataPulseId,
            Role                    = "Software Engineer",
            Stage                   = OpportunityStage.Rejected,
            WorkArrangement         = WorkArrangement.OnSite,
            CompensationRange       = "$110,000–$135,000",
            PostingUrls             = ["https://datapulse.example.com/careers"],
            RoleDescription         = RdDpSe,
            RequiredQualifications  = ReqDpSe,
            PreferredQualifications = PrefDpSe,
            CreatedAt               = now.AddDays(-65),
            UpdatedAt               = now.AddDays(-28),
        });
        await _db.SaveOpportunityAsync(new Opportunity
        {
            Id                      = dpBeId,
            Version                 = 1,
            OrganizationId          = dataPulseId,
            Role                    = "Backend Engineer",
            Stage                   = OpportunityStage.Open,
            WorkArrangement         = WorkArrangement.Hybrid,
            CompensationRange       = "$120,000–$145,000",
            PostingUrls             = ["https://datapulse.example.com/careers"],
            RoleDescription         = RdDpBe,
            RequiredQualifications  = ReqDpBe,
            PreferredQualifications = PrefDpBe,
            CreatedAt               = now.AddDays(-25),
            UpdatedAt               = now.AddDays(-20),
        });

        // ── Contact–Opportunity Roles ──────────────────────────────────────────
        await _db.SaveRoleAsync(new ContactOpportunityRole { ContactId = sarahId,   OpportunityId = apexSfeId,   Version = 1, Roles = ["Recruiter"] });
        await _db.SaveRoleAsync(new ContactOpportunityRole { ContactId = michaelId, OpportunityId = apexSfeId,   Version = 1, Roles = ["Hiring Manager"] });
        await _db.SaveRoleAsync(new ContactOpportunityRole { ContactId = sarahId,   OpportunityId = apexStaffId, Version = 1, Roles = ["Recruiter"] });
        await _db.SaveRoleAsync(new ContactOpportunityRole { ContactId = emilyId,   OpportunityId = cnFsdId,     Version = 1, Roles = ["Recruiter"] });
        await _db.SaveRoleAsync(new ContactOpportunityRole { ContactId = jamesId,   OpportunityId = dpSeId,      Version = 1, Roles = ["Hiring Manager", "Interviewer"] });
        await _db.SaveRoleAsync(new ContactOpportunityRole { ContactId = jamesId,   OpportunityId = dpBeId,      Version = 1, Roles = ["Hiring Manager"] });

        // ── Sessions, Artifacts & Correspondence ──────────────────────────────

        // 1 — Apex Systems / Senior Frontend Engineer
        var (apexSfeTailoredId, apexSfeCoverId) = await CreateArtifactsAsync(
            TrApexSfe, CoverLetterApexSfe, baseResumeDocxBytes, "Apex_Systems_Senior_Frontend_Engineer");
        var apexSfeSessionId = Guid.NewGuid().ToString();
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id                           = apexSfeSessionId,
            Version                      = 1,
            CreatedAt                    = now.AddDays(-75),
            OrganizationId               = apexId,
            OrganizationNameSnapshot     = "Apex Systems",
            OpportunityId                = apexSfeId,
            OpportunityRoleSnapshot      = "Senior Frontend Engineer",
            Role                         = "Senior Frontend Engineer",
            RoleDescription              = RdApexSfe,
            BaseResumeVersionId          = resumeVersionId,
            BaseResumeNameSnapshot       = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore                   = "Strong Match",
            MatchStrengths               = new()
            {
                "7+ years of React and TypeScript experience directly satisfies the role's core requirement",
                "Led enterprise dashboard rebuild serving 500K+ users demonstrating large-scale frontend engineering",
                "Established shared component library across three product teams — aligns with design system responsibilities",
                "Mentorship and code review experience supports the team leadership expectations",
                "40% load time improvement demonstrates the performance optimization depth the role requires",
            },
            MatchGaps                    = new()
            {
                "No explicit mention of micro-frontend architecture or module federation",
                "AWS experience is primarily backend-focused; frontend deployment and CDN experience not documented",
                "WCAG accessibility compliance work not explicitly listed",
            },
            AdditionalKeywords           = new()
            {
                new SuggestedKeyword { Category = "Architecture",   Keyword = "micro-frontend" },
                new SuggestedKeyword { Category = "Accessibility",  Keyword = "WCAG 2.1" },
                new SuggestedKeyword { Category = "Frontend",       Keyword = "module federation" },
                new SuggestedKeyword { Category = "Tools",          Keyword = "Storybook" },
            },
            WhyApplyText                 = WhyApexSfe,
            CoverLetterText              = CoverLetterApexSfe,
            TailoredResumeFileId         = apexSfeTailoredId,
            CoverLetterFileId            = apexSfeCoverId,
            ArtifactsGenerated           = true,
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexSfeId,
            Type = CorrespondenceType.ResumeSubmitted, OccurredAt = now.AddDays(-75),
            ContactId = sarahId, LinkedSessionId = apexSfeSessionId, CoverLetterSubmitted = true,
            CreatedAt = now.AddDays(-75), UpdatedAt = now.AddDays(-75),
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexSfeId,
            Type = CorrespondenceType.Email, Direction = CorrespondenceDirection.Incoming,
            OccurredAt = now.AddDays(-70), ContactId = sarahId,
            Subject = "Application Received — Senior Frontend Engineer at Apex Systems",
            Body    = "Hi Jordan,\n\nThank you for applying to the Senior Frontend Engineer role at Apex Systems. We received your application and our team is reviewing it now.\n\nWe'd love to schedule a 30-minute call to learn more about your background. Are you available sometime this week or next? Let me know a few times that work for you.\n\nBest,\nSarah Chen\nTechnical Recruiter, Apex Systems",
            CreatedAt = now.AddDays(-70), UpdatedAt = now.AddDays(-70),
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexSfeId,
            Type = CorrespondenceType.PhoneCall, Direction = CorrespondenceDirection.Outgoing,
            OccurredAt = now.AddDays(-65), ContactId = sarahId,
            Body = "30-minute introductory call with Sarah Chen. Discussed background, the role requirements, and engineering culture at Apex. Sarah mentioned the team is currently 8 engineers with plans to grow to 12. Strong mutual interest — she indicated she would advance the application to a technical review with Michael Torres.",
            CreatedAt = now.AddDays(-65), UpdatedAt = now.AddDays(-65),
        });

        // 2 — Apex Systems / Staff Software Engineer
        var (apexStaffTailoredId, apexStaffCoverId) = await CreateArtifactsAsync(
            TrApexStaff, CoverLetterApexStaff, baseResumeDocxBytes, "Apex_Systems_Staff_Software_Engineer");
        var apexStaffSessionId = Guid.NewGuid().ToString();
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id                           = apexStaffSessionId,
            Version                      = 1,
            CreatedAt                    = now.AddDays(-63),
            OrganizationId               = apexId,
            OrganizationNameSnapshot     = "Apex Systems",
            OpportunityId                = apexStaffId,
            OpportunityRoleSnapshot      = "Staff Software Engineer",
            Role                         = "Staff Software Engineer",
            RoleDescription              = RdApexStaff,
            BaseResumeVersionId          = resumeVersionId,
            BaseResumeNameSnapshot       = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore                   = "Good",
            MatchStrengths               = new()
            {
                "Led a cross-team microservices migration — demonstrates Staff-level technical leadership scope",
                "Architectural decision-making and standards-setting experience matches the role's mandate",
                "Product partnership and stakeholder alignment experience is explicitly sought",
                "Mentorship of junior engineers shows investment in the broader team's growth",
            },
            MatchGaps                    = new()
            {
                "8+ years requirement; candidate has 7 years — slight gap against the stated threshold",
                "Limited documented proficiency in multiple backend languages beyond TypeScript/Node.js",
                "No experience in enterprise SaaS, HR technology, or workforce management domains",
                "Cross-team leadership was informal; no official Staff or Principal title held previously",
            },
            AdditionalKeywords           = new()
            {
                new SuggestedKeyword { Category = "Languages",     Keyword = "Go" },
                new SuggestedKeyword { Category = "Architecture",  Keyword = "distributed systems" },
                new SuggestedKeyword { Category = "Domain",        Keyword = "enterprise SaaS" },
                new SuggestedKeyword { Category = "Leadership",    Keyword = "technical strategy" },
            },
            WhyApplyText                 = WhyApexStaff,
            CoverLetterText              = CoverLetterApexStaff,
            TailoredResumeFileId         = apexStaffTailoredId,
            CoverLetterFileId            = apexStaffCoverId,
            ArtifactsGenerated           = true,
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexStaffId,
            Type = CorrespondenceType.ResumeSubmitted, OccurredAt = now.AddDays(-63),
            ContactId = sarahId, LinkedSessionId = apexStaffSessionId, CoverLetterSubmitted = true,
            CreatedAt = now.AddDays(-63), UpdatedAt = now.AddDays(-63),
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexStaffId,
            Type = CorrespondenceType.Email, Direction = CorrespondenceDirection.Incoming,
            OccurredAt = now.AddDays(-61), ContactId = sarahId,
            Subject = "Application Received — Staff Software Engineer at Apex Systems",
            Body    = "Hi Jordan,\n\nThank you for your application to the Staff Software Engineer position. We have received your materials and will be in touch within the next two weeks.\n\nBest,\nSarah Chen\nTechnical Recruiter, Apex Systems",
            CreatedAt = now.AddDays(-61), UpdatedAt = now.AddDays(-61),
        });

        // 3 — CloudNova / Full-Stack Developer
        var (cnFsdTailoredId, cnFsdCoverId) = await CreateArtifactsAsync(
            TrCnFsd, CoverLetterCnFsd, baseResumeDocxBytes, "CloudNova_Full_Stack_Developer");
        var cnFsdSessionId = Guid.NewGuid().ToString();
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id                           = cnFsdSessionId,
            Version                      = 1,
            CreatedAt                    = now.AddDays(-55),
            OrganizationId               = cloudNovaId,
            OrganizationNameSnapshot     = "CloudNova",
            OpportunityId                = cnFsdId,
            OpportunityRoleSnapshot      = "Full-Stack Developer",
            Role                         = "Full-Stack Developer",
            RoleDescription              = RdCnFsd,
            BaseResumeVersionId          = resumeVersionId,
            BaseResumeNameSnapshot       = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore                   = "Strong Match",
            MatchStrengths               = new()
            {
                "Full-stack background across React and Node.js maps directly to the stated scope",
                "Production Docker and Kubernetes experience satisfies the containerization requirement",
                "Developer tooling work at TechCorp is directly relevant to CloudNova's product domain",
                "PostgreSQL, MongoDB, and Redis experience covers both SQL and NoSQL requirements",
                "TypeScript proficiency aligns with the stated preference",
            },
            MatchGaps                    = new()
            {
                "No open source contributions listed",
                "Kubernetes operator or controller patterns not specifically mentioned",
                "No prior experience at a developer tooling or infrastructure company",
            },
            AdditionalKeywords           = new()
            {
                new SuggestedKeyword { Category = "Cloud Native",  Keyword = "Kubernetes operators" },
                new SuggestedKeyword { Category = "Open Source",   Keyword = "GitHub contributions" },
                new SuggestedKeyword { Category = "Tools",         Keyword = "Helm" },
                new SuggestedKeyword { Category = "Backend",       Keyword = "gRPC" },
            },
            WhyApplyText                 = WhyCnFsd,
            CoverLetterText              = CoverLetterCnFsd,
            TailoredResumeFileId         = cnFsdTailoredId,
            CoverLetterFileId            = cnFsdCoverId,
            ArtifactsGenerated           = true,
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = cnFsdId,
            Type = CorrespondenceType.ResumeSubmitted, OccurredAt = now.AddDays(-55),
            ContactId = emilyId, LinkedSessionId = cnFsdSessionId, CoverLetterSubmitted = true,
            CreatedAt = now.AddDays(-55), UpdatedAt = now.AddDays(-55),
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = cnFsdId,
            Type = CorrespondenceType.VideoCall, OccurredAt = now.AddDays(-42), ContactId = emilyId,
            Body = "60-minute technical screening with two CloudNova engineers. Covered system design (distributed cache invalidation), JavaScript event loop internals, and React rendering behavior. Also discussed the developer platform roadmap and engineering culture. Strong mutual interest indicated at the end of the call.",
            CreatedAt = now.AddDays(-42), UpdatedAt = now.AddDays(-42),
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = cnFsdId,
            Type = CorrespondenceType.Email, Direction = CorrespondenceDirection.Incoming,
            OccurredAt = now.AddDays(-21), ContactId = emilyId,
            Subject = "Offer Letter — Full-Stack Developer, CloudNova",
            Body    = "Hi Jordan,\n\nWe are thrilled to extend an offer for the Full-Stack Developer position at CloudNova!\n\nPlease find the attached offer letter with compensation details, benefits, and proposed start date. We would love to have you join on the first of next month if that works for you.\n\nLet us know if you have any questions — we are excited to have you on the team.\n\nBest,\nEmily Roberts\nTalent Acquisition, CloudNova",
            CreatedAt = now.AddDays(-21), UpdatedAt = now.AddDays(-21),
        });

        // 4 — DataPulse Analytics / Software Engineer
        var (dpSeTailoredId, dpSeCoverId) = await CreateArtifactsAsync(
            TrDpSe, CoverLetterDpSe, baseResumeDocxBytes, "DataPulse_Software_Engineer");
        var dpSeSessionId = Guid.NewGuid().ToString();
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id                           = dpSeSessionId,
            Version                      = 1,
            CreatedAt                    = now.AddDays(-55),
            OrganizationId               = dataPulseId,
            OrganizationNameSnapshot     = "DataPulse Analytics",
            OpportunityId                = dpSeId,
            OpportunityRoleSnapshot      = "Software Engineer",
            Role                         = "Software Engineer",
            RoleDescription              = RdDpSe,
            BaseResumeVersionId          = resumeVersionId,
            BaseResumeNameSnapshot       = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore                   = "Fair",
            MatchStrengths               = new()
            {
                "Backend API experience in Python and Node.js satisfies the core language requirement",
                "High-volume event API work at Velocity Labs maps to the throughput-oriented pipeline context",
                "Strong SQL proficiency and PostgreSQL experience aligns with the data layer requirements",
                "Comfort in fast-paced environments with direct team collaboration fits the startup culture",
            },
            MatchGaps                    = new()
            {
                "No direct stream processing experience (Kafka, Kinesis, or equivalent platforms)",
                "Data pipeline development is limited relative to the API-focused backend work documented",
                "No exposure to dbt, Apache Spark, or similar data transformation tools listed as preferred",
                "E-commerce or retail analytics domain background not present",
            },
            AdditionalKeywords           = new()
            {
                new SuggestedKeyword { Category = "Streaming",  Keyword = "Apache Kafka" },
                new SuggestedKeyword { Category = "Data",       Keyword = "Apache Spark" },
                new SuggestedKeyword { Category = "Data",       Keyword = "dbt" },
                new SuggestedKeyword { Category = "Language",   Keyword = "Go" },
            },
            WhyApplyText                 = WhyDpSe,
            CoverLetterText              = CoverLetterDpSe,
            TailoredResumeFileId         = dpSeTailoredId,
            CoverLetterFileId            = dpSeCoverId,
            ArtifactsGenerated           = true,
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = dpSeId,
            Type = CorrespondenceType.ResumeSubmitted, OccurredAt = now.AddDays(-55),
            LinkedSessionId = dpSeSessionId,
            CreatedAt = now.AddDays(-55), UpdatedAt = now.AddDays(-55),
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = dpSeId,
            Type = CorrespondenceType.Email, Direction = CorrespondenceDirection.Incoming,
            OccurredAt = now.AddDays(-28), ContactId = jamesId,
            Subject = "Re: Application — Software Engineer, DataPulse Analytics",
            Body    = "Hi Jordan,\n\nThank you for your interest in DataPulse Analytics. After careful review, we have decided to move forward with candidates whose experience more closely aligns with our real-time data pipeline stack.\n\nWe appreciate the time and effort you put into your application and wish you all the best in your search.\n\nBest,\nJames Kim\nCTO, DataPulse Analytics",
            CreatedAt = now.AddDays(-28), UpdatedAt = now.AddDays(-28),
        });

        // 5 — DataPulse Analytics / Backend Engineer
        var (dpBeTailoredId, dpBeCoverId) = await CreateArtifactsAsync(
            TrDpBe, CoverLetterDpBe, baseResumeDocxBytes, "DataPulse_Backend_Engineer");
        var dpBeSessionId = Guid.NewGuid().ToString();
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id                           = dpBeSessionId,
            Version                      = 1,
            CreatedAt                    = now.AddDays(-20),
            OrganizationId               = dataPulseId,
            OrganizationNameSnapshot     = "DataPulse Analytics",
            OpportunityId                = dpBeId,
            OpportunityRoleSnapshot      = "Backend Engineer",
            Role                         = "Backend Engineer",
            RoleDescription              = RdDpBe,
            BaseResumeVersionId          = resumeVersionId,
            BaseResumeNameSnapshot       = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore                   = "Good",
            MatchStrengths               = new()
            {
                "3+ years of REST API development in Python and Node.js directly satisfies the core requirement",
                "Production PostgreSQL and Redis experience matches the required database stack exactly",
                "AWS deployment and operations background aligns with the cloud platform requirement",
                "On-call rotation and incident post-mortem experience demonstrates the reliability focus the role expects",
                "Startup-adjacent experience and preference for autonomy fits the cultural expectations",
            },
            MatchGaps                    = new()
            {
                "Real-time and streaming data systems experience not explicitly documented",
                "Terraform or Pulumi infrastructure-as-code experience not listed",
                "No prior experience at a company with fewer than 50 employees; previous roles at mid-size organizations",
            },
            AdditionalKeywords           = new()
            {
                new SuggestedKeyword { Category = "Infrastructure",  Keyword = "Terraform" },
                new SuggestedKeyword { Category = "Streaming",       Keyword = "Amazon Kinesis" },
                new SuggestedKeyword { Category = "Observability",   Keyword = "Datadog" },
                new SuggestedKeyword { Category = "Backend",         Keyword = "FastAPI" },
            },
            WhyApplyText                 = WhyDpBe,
            CoverLetterText              = CoverLetterDpBe,
            TailoredResumeFileId         = dpBeTailoredId,
            CoverLetterFileId            = dpBeCoverId,
            ArtifactsGenerated           = true,
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = dpBeId,
            Type = CorrespondenceType.ResumeSubmitted, OccurredAt = now.AddDays(-20),
            ContactId = jamesId, LinkedSessionId = dpBeSessionId, CoverLetterSubmitted = true,
            CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-20),
        });
        await _db.SaveCorrespondenceAsync(new Correspondence
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = dpBeId,
            Type = CorrespondenceType.Email, Direction = CorrespondenceDirection.Outgoing,
            OccurredAt = now.AddDays(-16), ContactId = jamesId,
            Subject = "Following Up — Backend Engineer Application",
            Body    = "Hi James,\n\nI wanted to follow up on the Backend Engineer application I submitted last week. I am genuinely excited about the opportunity to join the DataPulse team and contribute to the platform engineering work.\n\nPlease do not hesitate to reach out if you need any additional information. I am happy to connect at a time that works for you.\n\nBest,\nJordan Lee",
            CreatedAt = now.AddDays(-16), UpdatedAt = now.AddDays(-16),
        });

        // ── Opportunity Field History ──────────────────────────────────────────
        // OldValue is the stage numeric value before the change, serialized as a JSON number string.
        // Open=0, Applied=3, Interview=4, Offer=6, Rejected=8

        // Apex/SFE: Open → Applied → Interview
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexSfeId,
            ChangedAt = now.AddDays(-75), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "0" }],
        });
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexSfeId,
            ChangedAt = now.AddDays(-63), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "3" }],
        });

        // Apex/Staff: Open → Applied
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = apexStaffId,
            ChangedAt = now.AddDays(-63), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "0" }],
        });

        // CloudNova/FSD: Open → Applied → Interview → Offer
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = cnFsdId,
            ChangedAt = now.AddDays(-55), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "0" }],
        });
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = cnFsdId,
            ChangedAt = now.AddDays(-42), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "3" }],
        });
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = cnFsdId,
            ChangedAt = now.AddDays(-21), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "4" }],
        });

        // DataPulse/SE: Open → Applied → Rejected
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = dpSeId,
            ChangedAt = now.AddDays(-55), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "0" }],
        });
        await _db.SaveHistoryEntryAsync(new OpportunityFieldHistory
        {
            Id = Guid.NewGuid().ToString(), Version = 1, OpportunityId = dpSeId,
            ChangedAt = now.AddDays(-28), Source = HistorySource.StageQuickEdit,
            Changes = [new FieldChange { FieldName = "Stage", OldValue = "3" }],
        });

        // ── Ad-hoc Sessions ────────────────────────────────────────────────────
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id = Guid.NewGuid().ToString(), Version = 1, CreatedAt = now.AddDays(-14),
            OrganizationId = null, OrganizationNameSnapshot = "Nexus Technologies",
            OpportunityId = null, OpportunityRoleSnapshot = "",
            Role = "Software Engineer",
            RoleDescription = "Senior Software Engineer role for aerospace defense contractors. Requires 10+ years of experience, proficiency in C and C++, real-time operating systems (RTOS), and security clearance eligibility.",
            BaseResumeVersionId = resumeVersionId,
            BaseResumeNameSnapshot = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore = "Poor",
            MatchStrengths = new() { "General software engineering background provides a foundational programming overlap" },
            MatchGaps = new()
            {
                "Role requires 10+ years of experience; candidate has 7 — significant gap against the threshold",
                "C and C++ proficiency listed as required; neither is present in candidate's background",
                "Embedded real-time operating systems (RTOS) experience required but not applicable to candidate",
                "Security clearance eligibility cannot be assessed from the resume",
            },
            AdditionalKeywords = new()
            {
                new SuggestedKeyword { Category = "Languages", Keyword = "C++" },
                new SuggestedKeyword { Category = "Systems",   Keyword = "RTOS" },
                new SuggestedKeyword { Category = "Security",  Keyword = "Security Clearance" },
            },
            ArtifactsGenerated = false,
        });
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id = Guid.NewGuid().ToString(), Version = 1, CreatedAt = now.AddDays(-10),
            OrganizationId = null, OrganizationNameSnapshot = "ByteForge Inc",
            OpportunityId = null, OpportunityRoleSnapshot = "",
            Role = "Backend Developer",
            RoleDescription = "Backend Developer for a Web3 platform building decentralized finance applications. Requires Solidity and blockchain protocol experience (Ethereum, Solana) and smart contract development skills. Python or Node.js for off-chain services.",
            BaseResumeVersionId = resumeVersionId,
            BaseResumeNameSnapshot = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore = "Fair",
            MatchStrengths = new()
            {
                "Node.js and Python backend experience aligns with the off-chain service development requirement",
                "REST API development background maps to the off-chain API layer responsibilities",
                "AWS experience satisfies the cloud infrastructure requirement",
            },
            MatchGaps = new()
            {
                "No blockchain or Web3 development background present in resume",
                "Solidity and smart contract development required but not documented",
                "No cryptography or distributed ledger protocol experience listed",
            },
            AdditionalKeywords = new()
            {
                new SuggestedKeyword { Category = "Blockchain", Keyword = "Solidity" },
                new SuggestedKeyword { Category = "Blockchain", Keyword = "Ethereum" },
                new SuggestedKeyword { Category = "Web3",       Keyword = "Smart Contracts" },
            },
            ArtifactsGenerated = false,
        });
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id = Guid.NewGuid().ToString(), Version = 1, CreatedAt = now.AddDays(-7),
            OrganizationId = null, OrganizationNameSnapshot = "Orbital Systems",
            OpportunityId = null, OpportunityRoleSnapshot = "",
            Role = "Full-Stack Developer",
            RoleDescription = "Full-Stack Developer for satellite ground station software. Requires proficiency in C and Rust, experience with real-time operating systems, familiarity with DO-178C safety-critical standards, and security clearance eligibility.",
            BaseResumeVersionId = resumeVersionId,
            BaseResumeNameSnapshot = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore = "Poor",
            MatchStrengths = new() { "Full-stack web development experience broadly maps to the developer title" },
            MatchGaps = new()
            {
                "C and Rust proficiency required; neither appears in candidate's background",
                "Real-time operating systems (RTOS) experience is required but not present",
                "Safety-critical systems standards (DO-178C) background not applicable to candidate",
                "Aerospace domain knowledge not documented",
                "Security clearance eligibility cannot be assessed from the resume",
            },
            AdditionalKeywords = new()
            {
                new SuggestedKeyword { Category = "Languages",  Keyword = "Rust" },
                new SuggestedKeyword { Category = "Systems",    Keyword = "RTOS" },
                new SuggestedKeyword { Category = "Standards",  Keyword = "DO-178C" },
            },
            ArtifactsGenerated = false,
        });
        await _db.SaveSessionAsync(new SessionRecord
        {
            Id = Guid.NewGuid().ToString(), Version = 1, CreatedAt = now.AddDays(-4),
            OrganizationId = null, OrganizationNameSnapshot = "Cascade Labs",
            OpportunityId = null, OpportunityRoleSnapshot = "",
            Role = "Platform Engineer",
            RoleDescription = "Platform Engineer to build and operate the internal developer platform at a cloud infrastructure company. Strong Kubernetes operations experience required along with infrastructure as code (Terraform or Pulumi) and an SRE mindset.",
            BaseResumeVersionId = resumeVersionId,
            BaseResumeNameSnapshot = "Jordan Lee — Software Engineer",
            BaseResumeVersionNumberSnapshot = 1,
            MatchScore = "Fair",
            MatchStrengths = new()
            {
                "Docker and Kubernetes production experience directly satisfies the container orchestration requirement",
                "AWS deployment and operations background aligns with the cloud infrastructure responsibilities",
                "CI/CD pipeline experience (GitHub Actions) matches the automation tooling requirement",
            },
            MatchGaps = new()
            {
                "Role is platform and infrastructure-focused; candidate's primary background is application development",
                "Terraform or Pulumi infrastructure-as-code experience not documented in resume",
                "No experience operating Kubernetes clusters at scale as a platform team responsibility",
            },
            AdditionalKeywords = new()
            {
                new SuggestedKeyword { Category = "Infrastructure", Keyword = "Terraform" },
                new SuggestedKeyword { Category = "Platform",       Keyword = "Platform Engineering" },
                new SuggestedKeyword { Category = "Observability",  Keyword = "Prometheus" },
            },
            ArtifactsGenerated = false,
        });
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<(string tailoredId, string coverId)> CreateArtifactsAsync(
        string tailoredMarkdown, string coverLetterText, byte[] baseResumeDocxBytes, string namePrefix)
    {
        var tailoredBytes = _docx.GenerateResumeDocx(baseResumeDocxBytes, tailoredMarkdown);
        var coverBytes    = _docx.GenerateCoverLetterDocx(coverLetterText);

        var tailored = new StoredFile
        {
            Id         = Guid.NewGuid().ToString(),
            Name       = $"{namePrefix}_Tailored_Resume.docx",
            DataBase64 = Convert.ToBase64String(tailoredBytes),
            LastUsedAt = DateTime.UtcNow,
            SessionCount = 1,
        };
        var cover = new StoredFile
        {
            Id         = Guid.NewGuid().ToString(),
            Name       = $"{namePrefix}_Cover_Letter.docx",
            DataBase64 = Convert.ToBase64String(coverBytes),
            LastUsedAt = DateTime.UtcNow,
            SessionCount = 1,
        };

        await _db.SaveFileAsync(tailored);
        await _db.SaveFileAsync(cover);

        return (tailored.Id, cover.Id);
    }

    // ── Static Content ────────────────────────────────────────────────────────

    // Base Resume
    private const string ResumeMarkdown = """
        Jordan Lee

        jordan.lee@email.com · (555) 248-7391 · Austin, TX · linkedin.com/in/jordanlee

        Software engineer with 7+ years of experience building scalable web applications and distributed systems. Experienced in cloud infrastructure, API development, and technical leadership in Agile engineering teams.

        # Skills & Abilities

        **Languages:** TypeScript, JavaScript, Python, SQL, HTML/CSS
        **Frameworks:** React, Node.js, Express, Next.js, Django
        **Cloud & Infrastructure:** AWS (EC2, S3, Lambda, RDS), Docker, Kubernetes
        **Databases:** PostgreSQL, MySQL, MongoDB, Redis
        **Tools & Practices:** Git, GitHub Actions, CI/CD, Agile/Scrum, REST APIs, GraphQL

        # Professional History

        ## Senior Software Engineer — TechCorp, Austin, TX (Jan 2021 – Present)

        Led development of a customer-facing dashboard serving 500K+ monthly active users, reducing load time by 40% through code splitting and caching
        Architected a microservices migration improving deployment frequency from monthly to daily releases
        Mentored 3 junior engineers and conducted code reviews for a team of 8
        Collaborated with product and design to define technical requirements for 4 major feature launches

        ## Software Engineer — Velocity Labs, Austin, TX (Mar 2018 – Dec 2020)

        Built and maintained RESTful APIs consumed by iOS and Android apps with 200K+ users
        Implemented automated testing suite raising code coverage from 42% to 87%
        Reduced AWS infrastructure costs by 25% through right-sizing and autoscaling
        Participated in on-call rotation and led post-mortems for 3 major incidents

        # Education

        ## Bachelor of Science, Computer Science — University of Texas at Austin (May 2018)

        GPA: 3.6 / 4.0
        """;

    // ── Role Descriptions ─────────────────────────────────────────────────────

    private const string RdApexSfe = """
        We are looking for a Senior Frontend Engineer to join our core product team at Apex Systems. You will architect and build complex UI features for our flagship workforce management platform, serving over 2,000 enterprise clients worldwide.

        **Responsibilities**
        - Design and implement scalable React component libraries and a unified design system
        - Collaborate with product managers and UX designers to deliver polished, accessible user experiences
        - Optimize application performance for data-heavy enterprise views and complex state management
        - Lead code reviews and mentor junior and mid-level engineers
        - Contribute to technical roadmap planning and architectural decisions

        **Required Qualifications**
        - 5+ years of professional frontend development experience
        - Expert-level proficiency in React and TypeScript
        - Strong understanding of browser performance optimization and web accessibility (WCAG 2.1)
        - Experience with REST APIs and GraphQL
        - Familiarity with testing frameworks such as Jest and React Testing Library

        **Preferred Qualifications**
        - Experience with micro-frontend architecture or module federation
        - Knowledge of AWS or similar cloud platforms
        - Background in enterprise SaaS products
        - Experience mentoring or leading frontend engineers
        """;

    private const string RdApexStaff = """
        Apex Systems is looking for a Staff Software Engineer to lead high-impact technical initiatives across our platform engineering organization. This is an individual contributor leadership role for a senior engineer ready to drive architecture at scale while staying hands-on.

        **Responsibilities**
        - Define and drive technical strategy across multiple engineering teams
        - Lead complex, cross-functional projects from design through delivery
        - Partner with product and engineering leadership to align technical decisions with business goals
        - Establish and evolve engineering best practices, coding standards, and architecture guidelines
        - Mentor senior and mid-level engineers across the organization

        **Required Qualifications**
        - 8+ years of software engineering experience
        - Demonstrated experience designing and delivering distributed systems at scale
        - Proficiency in at least two backend languages (Python, Java, Go, or Node.js)
        - Strong command of system design, relational and non-relational databases, and cloud infrastructure

        **Preferred Qualifications**
        - Experience in enterprise SaaS, HR technology, or workforce management
        - Prior experience in a Staff, Principal, or Architect-level engineering role
        - Track record of driving organization-wide technical improvements and standards
        """;

    private const string RdCnFsd = """
        CloudNova is hiring a Full-Stack Developer to help build and evolve our developer-facing platform. You will work across the entire stack — from polished React interfaces to performant Node.js services — and have a direct hand in the experience of thousands of developers who rely on CloudNova every day.

        **Responsibilities**
        - Build new features across the CloudNova platform: dashboard, REST APIs, and internal tooling
        - Work closely with infrastructure and DevRel teams to surface developer metrics and operational insights
        - Write clean, maintainable code with comprehensive test coverage
        - Participate actively in design and architecture discussions

        **Required Qualifications**
        - 3+ years of full-stack development experience
        - Proficiency in React and Node.js (TypeScript strongly preferred)
        - Experience with both SQL and NoSQL databases
        - Comfort with containerization and orchestration (Docker, Kubernetes)

        **Preferred Qualifications**
        - Experience building developer tools, SDKs, or infrastructure products
        - Familiarity with Kubernetes controller or operator patterns
        - Open source contributions are a plus
        """;

    private const string RdDpSe = """
        DataPulse Analytics is looking for a Software Engineer to join our small, fast-moving team. You will help build the data ingestion and processing pipelines that power our real-time analytics product for e-commerce and retail brands.

        **Responsibilities**
        - Build and maintain event ingestion pipelines processing millions of records per day
        - Develop internal APIs and tooling consumed by the analytics dashboard
        - Collaborate directly with the CTO and product lead on feature design and architecture
        - Optimize pipeline throughput and reduce end-to-end processing latency

        **Required Qualifications**
        - 2+ years of software engineering experience
        - Experience with Python or Go for backend service development
        - Familiarity with data streaming technologies (Apache Kafka, Amazon Kinesis, or similar)
        - Solid SQL proficiency

        **Preferred Qualifications**
        - Experience in a startup or high-growth engineering environment
        - Background in e-commerce, retail, or digital marketing analytics
        - Exposure to data tools such as dbt, Apache Spark, or similar
        """;

    private const string RdDpBe = """
        DataPulse Analytics is growing its backend engineering team and looking for a Backend Engineer to own the API layer and help scale our platform. You will have significant autonomy, a direct line to the founding team, and a real opportunity to shape technical direction at a Series A startup.

        **Responsibilities**
        - Design and build RESTful APIs serving the customer-facing analytics dashboard
        - Develop reliable services handling high-throughput event data ingestion and aggregation
        - Work directly with the founders to shape product and technical direction
        - Implement observability, monitoring, and alerting for production systems

        **Required Qualifications**
        - 3+ years of backend engineering experience
        - Proficiency in Python and/or Node.js
        - Production experience with PostgreSQL and Redis
        - Hands-on experience with AWS or GCP

        **Preferred Qualifications**
        - Experience with real-time or streaming data systems
        - Familiarity with infrastructure as code (Terraform or Pulumi)
        - Prior startup experience
        """;

    // ── Required & Preferred Qualifications ──────────────────────────────────

    private static readonly string[] ReqApexSfe =
    [
        "5+ years of professional frontend development experience",
        "Expert-level proficiency in React and TypeScript",
        "Strong understanding of browser performance optimization and web accessibility (WCAG 2.1)",
        "Experience with REST APIs and GraphQL",
        "Familiarity with testing frameworks such as Jest and React Testing Library",
    ];
    private static readonly string[] PrefApexSfe =
    [
        "Experience with micro-frontend architecture or module federation",
        "Knowledge of AWS or similar cloud platforms",
        "Background in enterprise SaaS products",
        "Experience mentoring or leading frontend engineers",
    ];

    private static readonly string[] ReqApexStaff =
    [
        "8+ years of software engineering experience",
        "Demonstrated experience designing and delivering distributed systems at scale",
        "Proficiency in at least two backend languages (Python, Java, Go, or Node.js)",
        "Strong command of system design, relational and non-relational databases, and cloud infrastructure",
    ];
    private static readonly string[] PrefApexStaff =
    [
        "Experience in enterprise SaaS, HR technology, or workforce management",
        "Prior experience in a Staff, Principal, or Architect-level engineering role",
        "Track record of driving organization-wide technical improvements and standards",
    ];

    private static readonly string[] ReqCnFsd =
    [
        "3+ years of full-stack development experience",
        "Proficiency in React and Node.js (TypeScript strongly preferred)",
        "Experience with both SQL and NoSQL databases",
        "Comfort with containerization and orchestration (Docker, Kubernetes)",
    ];
    private static readonly string[] PrefCnFsd =
    [
        "Experience building developer tools, SDKs, or infrastructure products",
        "Familiarity with Kubernetes controller or operator patterns",
        "Open source contributions are a plus",
    ];

    private static readonly string[] ReqDpSe =
    [
        "2+ years of software engineering experience",
        "Experience with Python or Go for backend service development",
        "Familiarity with data streaming technologies (Apache Kafka, Amazon Kinesis, or similar)",
        "Solid SQL proficiency",
    ];
    private static readonly string[] PrefDpSe =
    [
        "Experience in a startup or high-growth engineering environment",
        "Background in e-commerce, retail, or digital marketing analytics",
        "Exposure to data tools such as dbt, Apache Spark, or similar",
    ];

    private static readonly string[] ReqDpBe =
    [
        "3+ years of backend engineering experience",
        "Proficiency in Python and/or Node.js",
        "Production experience with PostgreSQL and Redis",
        "Hands-on experience with AWS or GCP",
    ];
    private static readonly string[] PrefDpBe =
    [
        "Experience with real-time or streaming data systems",
        "Familiarity with infrastructure as code (Terraform or Pulumi)",
        "Prior startup experience",
    ];

    // ── Why Apply Texts ───────────────────────────────────────────────────────

    private const string WhyApexSfe = """
        Building the component library and design system for a workforce management platform serving 2,000+ enterprise clients is where 7 years of React and TypeScript specialization translates into lasting product impact. The performance constraints, accessibility requirements, and data density of enterprise-grade interfaces make this a more demanding frontend engineering problem than most roles offer.
        """;

    private const string WhyApexStaff = """
        Apex Systems' scale and the explicit cross-team mandate of the Staff role are the right environment for the technical strategy and architecture work I have been driving informally for the past two years — the combination of organizational authority and hands-on delivery that the role describes is where I expect to contribute most effectively.
        """;

    private const string WhyCnFsd = """
        CloudNova's developer platform is the product domain I have been working toward, and the full-stack scope of this role — owning both the React dashboard and the Node.js APIs that surface developer metrics — maps directly to my strongest experience across both layers. Building tools that engineering teams rely on daily, in a Kubernetes-native infrastructure context, is where production full-stack depth produces the most direct value.
        """;

    private const string WhyDpSe = """
        DataPulse's event ingestion pipelines processing millions of records daily map directly to the high-volume API work I built at Velocity Labs — and working on the pipeline architecture with direct access to the CTO at Series A stage is where backend engineers develop the sharpest instincts fastest.
        """;

    private const string WhyDpBe = """
        The Backend Engineer role at DataPulse is an opportunity to design the RESTful API layer and event ingestion architecture at the stage when those decisions still carry long-term consequences, backed by production experience with PostgreSQL, Redis, and AWS that satisfies the role's core requirements directly.
        """;

    // ── Cover Letters ─────────────────────────────────────────────────────────

    private const string CoverLetterApexSfe = """
        The Senior Frontend Engineer role at Apex Systems is a direct fit for the technical depth I have built over seven years specializing in React and TypeScript for complex, high-traffic web products. Building and maintaining a design system for a platform serving 2,000+ enterprise clients — with the data density, accessibility constraints, and multi-tenant configuration that implies — is the kind of frontend engineering challenge I find most worth doing well.

        At TechCorp, I led the rebuild of a customer-facing dashboard serving 500K+ monthly active users, cutting load time by 40% through code splitting, caching architecture, and component redesign. I also designed a shared component library adopted across three product teams, establishing UI consistency and reducing duplicated work across the organization. I have not worked with micro-frontend module federation directly, though the component isolation and team-boundary thinking it addresses are problems I have engaged with through the library work. I expect to contribute most immediately to the performance-critical enterprise views and component architecture that the team is scaling.
        """;

    private const string CoverLetterApexStaff = """
        The Staff Software Engineer role at Apex Systems is defined by driving technical strategy across multiple teams while staying hands-on — a scope I have been operating in for the past two years at TechCorp, without the formal title. The opportunity to do that work with organizational mandate, at Apex's scale, is the step I am ready to take.

        The most directly relevant work is a six-month microservices migration I led spanning four engineering teams, moving deployment frequency from monthly to daily and establishing team-level service ownership. I also defined the coding standards and architectural guidelines the organization now uses as a baseline. The stated threshold is eight years of experience; I have seven, but the scope of cross-team technical leadership I have operated at for the past two years more closely reflects Staff-level delivery than individual contribution. I expect to take on the cross-team architecture and standards-setting responsibilities from the first month while staying close to implementation.
        """;

    private const string CoverLetterCnFsd = """
        CloudNova's developer platform is the product domain I have been working toward, and the full-stack scope of this role — owning both the React dashboard and the Node.js APIs that surface developer metrics — is how I prefer to operate. Building tools that engineering teams depend on daily requires the reliability and attention to developer ergonomics I have developed across the full stack, and the Kubernetes-native infrastructure context is a direction I am actively deepening.

        At TechCorp, I built internal developer tooling that reduced deployment pipeline configuration time by 60%, adopted across all engineering teams. At Velocity Labs, I owned the Node.js API layer serving 200K+ mobile users end-to-end, including containerized deployment with Docker and AWS. I have used Kubernetes in production for service deployment but have not worked with controller or operator patterns. I expect to contribute most directly to the dashboard and API features that surface developer metrics, and to build deeper Kubernetes platform knowledge in the first months on the team.
        """;

    private const string CoverLetterDpSe = """
        DataPulse's event ingestion pipelines processing millions of records daily are a close match for the backend work I have focused on most — building high-throughput RESTful APIs in Node.js and Python at Velocity Labs, where throughput and reliability under mobile-client load were the primary engineering constraints. The opportunity to work directly with the CTO on pipeline architecture and product decisions at Series A stage is also where I expect to develop the sharpest instincts fastest.

        At Velocity Labs, I designed and maintained the core APIs serving 200K+ mobile users and participated in on-call rotation for a system processing high daily event volumes. At TechCorp, I built backend services supporting a platform at 500K+ monthly users, with a focus on throughput optimization, caching, and API reliability. I have not worked directly with Apache Kafka or Amazon Kinesis — my streaming systems exposure has been adjacent rather than operational. I expect to become productive on the ingestion pipeline quickly given how directly the role's throughput and latency requirements map to the backend systems I have built.
        """;

    private const string CoverLetterDpBe = """
        The Backend Engineer role at DataPulse aligns directly with my most recent technical work — Python and Node.js APIs, PostgreSQL and Redis in production, and AWS deployment with a reliability and observability focus. Designing the API layer and event ingestion architecture at the stage when those decisions carry long-term consequences is the kind of backend work I have been developing toward.

        At Velocity Labs, I owned the Node.js API layer serving 200K+ mobile users end-to-end, handled schema design and query optimization in PostgreSQL, and implemented a Redis caching layer that reduced average API response time by 35%. I was also primary on-call for 12 months and led post-mortems for three major incidents, which built strong instincts around keeping distributed systems stable under production load. I have not worked with real-time streaming systems or Terraform directly, though the infrastructure-as-code concepts are familiar from AWS deployment work. I expect to take ownership of the API layer quickly and grow into the streaming and infrastructure responsibilities as the platform scales.
        """;

    // ── Tailored Resumes ──────────────────────────────────────────────────────

    private const string TrApexSfe = """
        Jordan Lee

        jordan.lee@email.com · (555) 248-7391 · Austin, TX · linkedin.com/in/jordanlee

        Senior frontend engineer with 7 years of experience building enterprise-scale web applications. Specializes in React and TypeScript component architecture, performance optimization, and design system development. Led rebuild of a dashboard serving 500K+ monthly active users with a 40% load time improvement. Drives frontend standards through shared component libraries and technical mentorship across engineering teams.

        # Skills & Abilities

        **Frontend:** TypeScript, React, Next.js, JavaScript, HTML/CSS, GraphQL, REST APIs
        **Testing:** Jest, React Testing Library, Pytest
        **Cloud & Infrastructure:** AWS (EC2, S3, Lambda, RDS), Docker, Kubernetes
        **Databases:** PostgreSQL, MySQL, MongoDB, Redis
        **Practices:** Git, GitHub Actions, CI/CD, Agile/Scrum, Web Accessibility (WCAG), Technical Mentorship

        # Professional History

        ## Senior Software Engineer — TechCorp, Austin, TX (Jan 2021 – Present)

        Led rebuild of a customer-facing React/TypeScript dashboard serving 500K+ monthly active users, reducing load time by 40% through code splitting, caching optimization, and component architecture redesign
        Designed and implemented a shared component library adopted by three product teams, establishing consistent UI patterns and accessibility compliance across the platform
        Architected a microservices migration improving deployment frequency from monthly to daily releases
        Mentored 3 junior engineers and conducted code reviews for a team of 8

        ## Software Engineer — Velocity Labs, Austin, TX (Mar 2018 – Dec 2020)

        Built React-based web interfaces and RESTful APIs consumed by iOS and Android apps with 200K+ users
        Implemented automated testing suite (Jest + React Testing Library) raising code coverage from 42% to 87%
        Reduced AWS infrastructure costs by 25% through right-sizing and autoscaling
        Participated in on-call rotation and led post-mortems for 3 major incidents

        # Education

        ## Bachelor of Science, Computer Science — University of Texas at Austin (May 2018)

        GPA: 3.6 / 4.0
        """;

    private const string TrApexStaff = """
        Jordan Lee

        jordan.lee@email.com · (555) 248-7391 · Austin, TX · linkedin.com/in/jordanlee

        Software engineer with 7 years of experience leading distributed systems and cross-team technical initiatives. Specializes in driving cross-functional architecture, engineering standards, and large-scale system migrations. Led a six-month migration spanning four teams, moving deployment frequency from monthly to daily releases. Partners with product and engineering leadership to set technical direction while remaining hands-on through delivery.

        # Skills & Abilities

        **Languages:** TypeScript, JavaScript, Python, SQL
        **Architecture & Systems:** Microservices, REST APIs, GraphQL, Distributed Systems, System Design
        **Cloud & Infrastructure:** AWS (EC2, S3, Lambda, RDS), Docker, Kubernetes
        **Databases:** PostgreSQL, MySQL, MongoDB, Redis
        **Frameworks:** React, Node.js, Express, Next.js, Django
        **Practices:** Git, GitHub Actions, CI/CD, Agile/Scrum, Technical Mentorship, Architecture Review

        # Professional History

        ## Senior Software Engineer — TechCorp, Austin, TX (Jan 2021 – Present)

        Led a six-month company-wide microservices migration spanning four engineering teams, increasing deployment frequency from monthly to daily releases and establishing team-level service ownership
        Defined and enforced coding standards and architectural guidelines adopted across the engineering organization
        Partnered with product and design leadership to define technical requirements for 4 major feature launches
        Mentored 3 junior engineers and regularly conducted cross-team code reviews for a team of 8
        Built customer-facing dashboard serving 500K+ monthly active users, achieving 40% load time improvement

        ## Software Engineer — Velocity Labs, Austin, TX (Mar 2018 – Dec 2020)

        Built and maintained RESTful APIs consumed by iOS and Android apps with 200K+ users
        Implemented automated testing suite raising code coverage from 42% to 87%
        Reduced AWS infrastructure costs by 25% through right-sizing and autoscaling
        Led post-mortems for 3 major incidents and drove remediation plans to completion

        # Education

        ## Bachelor of Science, Computer Science — University of Texas at Austin (May 2018)

        GPA: 3.6 / 4.0
        """;

    private const string TrCnFsd = """
        Jordan Lee

        jordan.lee@email.com · (555) 248-7391 · Austin, TX · linkedin.com/in/jordanlee

        Full-stack software engineer with 7 years of experience building developer-facing tools and customer-facing platforms. Specializes in end-to-end product development across React interfaces and Node.js services. Delivered internal developer tooling reducing deployment configuration time by 60%; owned the Node.js API layer serving 200K+ mobile users. Production Kubernetes and Docker deployment experience with a strong emphasis on system observability and developer experience.

        # Skills & Abilities

        **Full-Stack:** TypeScript, React, Node.js, Next.js, Express, Python, Django
        **APIs & Protocols:** REST APIs, GraphQL, WebSockets
        **Cloud & DevOps:** AWS (EC2, S3, Lambda, RDS), Docker, Kubernetes, GitHub Actions, CI/CD
        **Databases:** PostgreSQL, MySQL, MongoDB, Redis
        **Practices:** Git, Agile/Scrum, Test-Driven Development, Observability, Incident Response

        # Professional History

        ## Senior Software Engineer — TechCorp, Austin, TX (Jan 2021 – Present)

        Built internal developer tooling that reduced deployment pipeline configuration time by 60%, adopted across all engineering teams
        Led development of a customer-facing React/TypeScript dashboard serving 500K+ monthly users with 40% load time improvement
        Architected a microservices migration on AWS using Docker, improving deployment cadence from monthly to daily
        Mentored 3 junior engineers and conducted code reviews for a team of 8

        ## Software Engineer — Velocity Labs, Austin, TX (Mar 2018 – Dec 2020)

        Owned the Node.js API layer serving iOS and Android mobile apps with 200K+ users end-to-end
        Containerized services with Docker and managed deployments on AWS EC2 and ECS
        Implemented automated testing suite raising code coverage from 42% to 87%
        Participated in on-call rotation and led incident post-mortems for 3 major outages

        # Education

        ## Bachelor of Science, Computer Science — University of Texas at Austin (May 2018)

        GPA: 3.6 / 4.0
        """;

    private const string TrDpSe = """
        Jordan Lee

        jordan.lee@email.com · (555) 248-7391 · Austin, TX · linkedin.com/in/jordanlee

        Software engineer with 7 years of experience in backend API development and high-volume event processing. Specializes in Python and Node.js service development, REST API architecture, and throughput-optimized systems. Supported platforms serving 500K+ monthly users and built APIs processing high volumes of mobile events daily. Comfortable operating in fast-paced environments with direct collaboration with technical and product leadership.

        # Skills & Abilities

        **Languages:** Python, TypeScript, JavaScript, SQL
        **Backend & Data:** Node.js, Express, Django, REST APIs, PostgreSQL, Redis, MongoDB
        **Cloud & Infrastructure:** AWS (EC2, S3, Lambda, RDS), Docker, Kubernetes
        **Practices:** Git, GitHub Actions, CI/CD, Agile/Scrum, On-Call Operations, Incident Response

        # Professional History

        ## Senior Software Engineer — TechCorp, Austin, TX (Jan 2021 – Present)

        Built and optimized backend services for a platform serving 500K+ monthly active users, with focus on throughput, caching, and API reliability
        Architected a microservices migration on AWS improving deployment frequency from monthly to daily releases
        Collaborated directly with product and engineering leadership to define technical requirements for major features
        Mentored 3 junior engineers and conducted code reviews for a team of 8

        ## Software Engineer — Velocity Labs, Austin, TX (Mar 2018 – Dec 2020)

        Designed and maintained RESTful APIs in Node.js and Python serving iOS and Android mobile clients with 200K+ users
        Implemented automated testing suite (Jest + Pytest) raising code coverage from 42% to 87%
        Reduced AWS infrastructure costs by 25% through right-sizing and autoscaling
        Participated in on-call rotation and led post-mortems for 3 major production incidents

        # Education

        ## Bachelor of Science, Computer Science — University of Texas at Austin (May 2018)

        GPA: 3.6 / 4.0
        """;

    private const string TrDpBe = """
        Jordan Lee

        jordan.lee@email.com · (555) 248-7391 · Austin, TX · linkedin.com/in/jordanlee

        Backend software engineer with 7 years of experience designing and operating RESTful APIs and distributed services. Specializes in Python and Node.js API development with PostgreSQL, Redis, and AWS as the primary production stack. Reduced average API response time by 35% through Redis caching and cut infrastructure costs by 25% through right-sizing and autoscaling. Strong emphasis on reliability, on-call operations, and post-mortem-driven improvements.

        # Skills & Abilities

        **Backend:** Node.js, Python, Express, Django, REST APIs, PostgreSQL, Redis, MongoDB
        **Cloud & Infrastructure:** AWS (EC2, S3, Lambda, RDS, ElastiCache), Docker, Kubernetes
        **Observability & Reliability:** On-call operations, incident management, post-mortems, monitoring
        **Languages:** TypeScript, JavaScript, Python, SQL
        **Practices:** Git, GitHub Actions, CI/CD, Agile/Scrum, Infrastructure as Code

        # Professional History

        ## Senior Software Engineer — TechCorp, Austin, TX (Jan 2021 – Present)

        Architected and operated high-throughput backend services on AWS supporting 500K+ monthly users, with PostgreSQL and Redis as the primary data and caching layers
        Led a microservices migration that increased deployment frequency from monthly to daily, improving system reliability and team velocity
        Defined backend coding standards and architecture guidelines adopted across the engineering organization
        Mentored 3 junior engineers and conducted code reviews for a team of 8

        ## Software Engineer — Velocity Labs, Austin, TX (Mar 2018 – Dec 2020)

        Designed and owned RESTful APIs in Node.js and Python serving 200K+ mobile users; managed schema design and query optimization in PostgreSQL
        Implemented Redis caching layer reducing average API response time by 35%
        Reduced AWS infrastructure costs by 25% through right-sizing EC2 instances and configuring autoscaling policies
        Primary on-call engineer for 12 months; led post-mortems for 3 major incidents and drove remediation to completion

        # Education

        ## Bachelor of Science, Computer Science — University of Texas at Austin (May 2018)

        GPA: 3.6 / 4.0
        """;
}
