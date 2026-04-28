namespace Simply.JobApplication.Tests.M3;

// M3-2: NewOrganizationPage — name validation, save, navigation guard.
public class NewOrganizationPageTests : BunitContext
{
    private async Task<IRenderedComponent<NewOrganizationPage>> Render(IIndexedDbService? db = null)
    {
        this.AddAppServices(db);
        var cut = Render<NewOrganizationPage>();
        await cut.WaitForStateAsync(() => !cut.FindAll(".spinner-border").Any(),
            TimeSpan.FromSeconds(2));
        return cut;
    }

    [Fact]
    public async Task NewOrgPage_WhenNameMatchesExistingOrg_ShowsInlineError()
    {
        var existing = new Organization { Id = "o1", Name = "Acme Corp" };
        var db = new TestIndexedDbBuilder().WithOrganizations(existing).Build();
        var cut = await Render(db);

        cut.Find("input.form-control").Input("Acme Corp");

        cut.WaitForAssertion(() =>
            Assert.Contains("already exists", cut.Markup));
    }

    [Fact]
    public async Task NewOrgPage_WhenNameMatchesExistingOrg_DisablesSave()
    {
        var existing = new Organization { Id = "o1", Name = "Acme Corp" };
        var db = new TestIndexedDbBuilder().WithOrganizations(existing).Build();
        var cut = await Render(db);

        cut.Find("input.form-control").Input("Acme Corp");

        cut.WaitForAssertion(() =>
        {
            var saveBtn = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Save");
            Assert.NotNull(saveBtn);
            Assert.NotNull(saveBtn.GetAttribute("disabled"));
        });
    }

    [Fact]
    public async Task NewOrgPage_NameValidation_IsCaseInsensitive()
    {
        var existing = new Organization { Id = "o1", Name = "Acme Corp" };
        var db = new TestIndexedDbBuilder().WithOrganizations(existing).Build();
        var cut = await Render(db);

        cut.Find("input.form-control").Input("acme corp");

        cut.WaitForAssertion(() => Assert.Contains("already exists", cut.Markup));
    }

    [Fact]
    public async Task NewOrgPage_WhenNameUnique_EnablesSave()
    {
        var existing = new Organization { Id = "o1", Name = "Acme Corp" };
        var db = new TestIndexedDbBuilder().WithOrganizations(existing).Build();
        var cut = await Render(db);

        cut.Find("input.form-control").Input("Beta LLC");

        cut.WaitForAssertion(() =>
        {
            var saveBtn = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Save");
            Assert.NotNull(saveBtn);
            Assert.Null(saveBtn.GetAttribute("disabled"));
        });
    }

    [Fact]
    public async Task NewOrgPage_Save_CreatesOrgAndNavigatesToDetail()
    {
        var db  = new TestIndexedDbBuilder().Build();
        var cut = await Render(db);

        cut.Find("input.form-control").Input("New Company");
        var saveBtn = cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        await saveBtn.ClickAsync(new());

        await db.Received(1).SaveOrganizationAsync(
            Arg.Is<Organization>(o => o.Name == "New Company"));

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("/organizations/", nav.Uri);
    }

    [Fact]
    public async Task NewOrgPage_WhenFieldsFilled_NavigationGuardActive()
    {
        var cut = await Render();

        cut.Find("input.form-control").Input("My Org");
        cut.WaitForAssertion(() =>
        {
            var inv = JSInterop.Invocations.Where(i => i.Identifier == "sjaSetBeforeUnload").ToList();
            Assert.Contains(inv, i => i.Arguments.Any(a => a is true));
        });
    }

    [Fact]
    public async Task NewOrgPage_WhenNoFieldsFilled_NavigationGuardNotActive()
    {
        var cut = await Render();

        // Wait for initial render with no dirty fields
        cut.WaitForAssertion(() =>
        {
            var inv = JSInterop.Invocations.Where(i => i.Identifier == "sjaSetBeforeUnload").ToList();
            // Either no invocations at all, or the last call passed false
            if (inv.Any())
                Assert.Contains(inv.Last().Arguments, a => a is false);
        });
    }
}
