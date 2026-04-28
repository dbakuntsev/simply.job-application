namespace Simply.JobApplication.Tests.History;

// M7-1, M7-2, M7-4: HistoryPage — list display, filter, delete, clear ad-hoc, live updates.
public class HistoryPageTests : BunitContext
{
    private async Task<(IRenderedComponent<HistoryPage> cut, AppServiceMocks mocks)>
        RenderWithSessions(IIndexedDbService? db = null)
    {
        var mocks = this.AddAppServices(db);
        var cut   = Render<HistoryPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any());
        return (cut, mocks);
    }

    // ── M7-1: History List display ────────────────────────────────────────────

    [Fact]
    public async Task HistoryList_EmptyState_ShowsMessage()
    {
        var (cut, _) = await RenderWithSessions();
        Assert.Contains("No sessions yet", cut.Markup);
    }

    [Fact]
    public async Task HistoryList_ShowsOrganizationColumn_WithLinkWhenOrgLinked()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OrganizationNameSnapshot = "Acme Corp",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder()
            .WithSessions(session)
            .WithOrganizationProjections(new OrganizationProjection { Id = "o1", Name = "Acme Corp" })
            .Build();

        var (cut, _) = await RenderWithSessions(db);

        Assert.Contains(cut.FindAll("a"), a =>
            (a.GetAttribute("href") ?? "").Contains("organizations/o1"));
    }

    [Fact]
    public async Task HistoryList_ShowsOrganizationColumn_PlainTextWhenAdHoc()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "Freelance Project",
            Role = "Consultant", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderWithSessions(db);

        Assert.Contains("Freelance Project", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("a"), a => a.TextContent.Contains("Freelance Project"));
    }

    [Fact]
    public async Task HistoryList_ShowsOpportunityColumn_BlankWhenNoOpp()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "Ad-hoc",
            OpportunityId = null, Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderWithSessions(db);

        // When OpportunityId is null, no opportunity link is rendered
        Assert.Empty(cut.FindAll("a[href*='opportunities']"));
    }

    [Fact]
    public async Task HistoryList_ShowsMatchScore()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            MatchScore = "Good", Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderWithSessions(db);

        Assert.Contains(cut.FindAll(".badge"), b => b.TextContent.Contains("Good"));
    }

    [Fact]
    public async Task HistoryList_ArtifactsGeneratedColumn_ShowsCheckmark()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            ArtifactsGenerated = true, Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderWithSessions(db);

        Assert.Contains("✓", cut.Markup);
    }

    [Fact]
    public async Task HistoryList_SortedByCreatedAtDescending()
    {
        var older = new SessionRecord
        {
            Id = "s1", Role = "Older Role", OrganizationId = null, OrganizationNameSnapshot = "X",
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var newer = new SessionRecord
        {
            Id = "s2", Role = "Newer Role", OrganizationId = null, OrganizationNameSnapshot = "X",
            CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var db = new TestIndexedDbBuilder().WithSessions(older, newer).Build();

        var (cut, _) = await RenderWithSessions(db);

        var rows = cut.FindAll("tbody tr").ToList();
        Assert.True(rows.Count >= 2);
        Assert.Contains("Newer Role", rows[0].TextContent);
        Assert.Contains("Older Role", rows[1].TextContent);
    }

    [Fact]
    public async Task HistoryList_FilterInput_ShowsOnlyMatchingRows()
    {
        var s1 = new SessionRecord
        {
            Id = "s1", Role = "Frontend Developer", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var s2 = new SessionRecord
        {
            Id = "s2", Role = "Backend Engineer", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var db = new TestIndexedDbBuilder().WithSessions(s1, s2).Build();
        var (cut, _) = await RenderWithSessions(db);

        cut.Find("input[type='search']").Input("Frontend");

        // HighlightMatch wraps the match in <mark> tags, so check row TextContent
        // (AngleSharp strips tags) rather than raw Markup to find the role text.
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            Assert.Single(rows);
            Assert.Contains("Frontend Developer", rows[0].TextContent);
        });
    }

    [Fact]
    public async Task HistoryList_FilterInput_HighlightsMatchedText()
    {
        var session = new SessionRecord
        {
            Id = "s1", Role = "Frontend Developer", OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, _) = await RenderWithSessions(db);

        cut.Find("input[type='search']").Input("Frontend");

        cut.WaitForAssertion(() => Assert.Contains("<mark>Frontend</mark>", cut.Markup));
    }

    [Fact]
    public async Task HistoryList_FilterInput_ExcludesCheckmarkColumns()
    {
        // ArtifactsGenerated=true shows ✓ in the Artifacts column, but filtering
        // by ✓ should NOT match that row (checkmark column is excluded from filter)
        var session = new SessionRecord
        {
            Id = "s1", Role = "Dev", ArtifactsGenerated = true, OrganizationId = null,
            OrganizationNameSnapshot = "X", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, _) = await RenderWithSessions(db);

        cut.Find("input[type='search']").Input("✓");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("tbody tr")));
    }

    // ── M7-2: Session management actions ─────────────────────────────────────

    [Fact]
    public async Task HistoryList_AdHocSession_ShowsDeleteButton()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderWithSessions(db);

        Assert.Contains(cut.FindAll("tbody button.btn-outline-danger"),
            b => b.TextContent.Contains("Delete"));
    }

    [Fact]
    public async Task HistoryList_OrgLinkedSession_NoDeleteButton()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OrganizationNameSnapshot = "Acme",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();

        var (cut, _) = await RenderWithSessions(db);

        Assert.Empty(cut.FindAll("tbody button.btn-outline-danger"));
    }

    [Fact]
    public async Task DeleteAdHocSession_Confirmed_DeletesSessionAndFiles()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            TailoredResumeFileId = "f1", CoverLetterFileId = "f2",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderWithSessions(db);

        await cut.Find("tbody button.btn-outline-danger").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("Delete Session", cut.Markup));

        await cut.Find(".modal .btn-danger").ClickAsync(new());

        await mocks.Db.Received(1).DeleteFileAsync("f1");
        await mocks.Db.Received(1).DeleteFileAsync("f2");
        await mocks.Db.Received(1).DeleteSessionAsync("s1");
    }

    [Fact]
    public async Task ClearAdHocSessions_ShowsConfirmDialog()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, _) = await RenderWithSessions(db);

        // Click "Clear Ad-hoc Sessions" — it's the not-disabled btn-outline-danger in the header
        var clearBtn = cut.FindAll("button").First(b =>
            b.TextContent.Contains("Clear Ad-hoc Sessions") && b.GetAttribute("disabled") is null);
        await clearBtn.ClickAsync(new());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".modal.d-block")));
    }

    [Fact]
    public async Task ClearAdHocSessions_Confirmed_CallsDeleteAdHocSessionsAsync()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderWithSessions(db);

        var clearBtn = cut.FindAll("button").First(b =>
            b.TextContent.Contains("Clear Ad-hoc Sessions") && b.GetAttribute("disabled") is null);
        await clearBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".modal.d-block")));

        await cut.Find(".modal .btn-danger").ClickAsync(new());

        await mocks.Db.Received(1).DeleteAdHocSessionsAsync();
    }

    [Fact]
    public async Task ClearAdHocSessions_Confirmed_DoesNotDeleteOrgLinkedSessions()
    {
        var adHoc = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(adHoc).Build();
        var (cut, mocks) = await RenderWithSessions(db);

        var clearBtn = cut.FindAll("button").First(b =>
            b.TextContent.Contains("Clear Ad-hoc Sessions") && b.GetAttribute("disabled") is null);
        await clearBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".modal.d-block")));
        await cut.Find(".modal .btn-danger").ClickAsync(new());

        // DeleteSessionAsync (single session delete) should NOT be called
        await mocks.Db.DidNotReceive().DeleteSessionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ClearAdHocSessions_Confirmed_BroadcastsBulkNotification()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderWithSessions(db);

        var clearBtn = cut.FindAll("button").First(b =>
            b.TextContent.Contains("Clear Ad-hoc Sessions") && b.GetAttribute("disabled") is null);
        await clearBtn.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".modal.d-block")));
        await cut.Find(".modal .btn-danger").ClickAsync(new());

        var ds = (DataSyncFake)mocks.DataSync;
        Assert.Contains(ds.Broadcasts, b =>
            b.entity == "session" && b.id == null && b.@event == "cleared");
    }

    [Fact]
    public async Task ClearAdHocSessions_DisabledWithTooltip_WhenNoAdHocSessionsExist()
    {
        var orgLinked = new SessionRecord
        {
            Id = "s1", OrganizationId = "o1", OrganizationNameSnapshot = "Acme",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(orgLinked).Build();

        var (cut, _) = await RenderWithSessions(db);

        var clearBtn = cut.FindAll("button").First(b =>
            b.TextContent.Contains("Clear Ad-hoc Sessions"));
        Assert.NotNull(clearBtn.GetAttribute("disabled"));
    }

    // ── M7-4: BroadcastChannel live updates ──────────────────────────────────

    [Fact]
    public async Task HistoryList_OnSessionChanged_RefreshesListSilently()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderWithSessions(db);

        // Update db to return an additional session, then fire the event
        var session2 = new SessionRecord
        {
            Id = "s2", OrganizationId = null, OrganizationNameSnapshot = "Y",
            Role = "Manager", CreatedAt = DateTime.UtcNow.AddMinutes(1)
        };
        db.GetAllSessionsAsync().Returns(Task.FromResult(new List<SessionRecord> { session, session2 }));
        db.GetSessionAsync("s2").Returns(Task.FromResult<SessionRecord?>(session2));

        mocks.DataSync.Raise("session", "s2", "created");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Manager"), TimeSpan.FromSeconds(2));
        Assert.Contains("Manager", cut.Markup);
    }

    [Fact]
    public async Task HistoryList_OnBulkClear_RefreshesFullList()
    {
        var session = new SessionRecord
        {
            Id = "s1", OrganizationId = null, OrganizationNameSnapshot = "X",
            Role = "Dev", CreatedAt = DateTime.UtcNow
        };
        var db = new TestIndexedDbBuilder().WithSessions(session).Build();
        var (cut, mocks) = await RenderWithSessions(db);

        // After bulk clear, db returns empty list
        db.GetAllSessionsAsync().Returns(Task.FromResult(new List<SessionRecord>()));
        mocks.DataSync.Raise("session", null, "cleared");

        await cut.WaitForStateAsync(() => cut.Markup.Contains("No sessions yet"), TimeSpan.FromSeconds(2));
        Assert.Contains("No sessions yet", cut.Markup);
    }
}
