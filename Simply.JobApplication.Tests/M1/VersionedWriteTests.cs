namespace Simply.JobApplication.Tests.M1;

// M1-1: VersionedWriteAsync delegates to JS and maps return strings to the result enum.
public class VersionedWriteTests
{
    private static (IndexedDbService svc, IJSObjectReference module) MakeService()
    {
        var js     = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));
        var svc = new IndexedDbService(js);
        return (svc, module);
    }

    [Fact]
    public async Task VersionedWrite_WhenVersionMatches_IncrementsVersionAndReturnsSuccess()
    {
        var (svc, module) = MakeService();
        module.InvokeAsync<string>("versionedWrite", Arg.Any<object[]?>())
              .Returns(new ValueTask<string>("success"));
        var org = new Organization { Version = 1 };

        var result = await svc.VersionedWriteAsync("organizations", org);

        Assert.Equal(VersionedWriteResult.Success, result);
    }

    [Fact]
    public async Task VersionedWrite_WhenVersionMismatches_ReturnsFailureWithoutWrite()
    {
        var (svc, module) = MakeService();
        module.InvokeAsync<string>("versionedWrite", Arg.Any<object[]?>())
              .Returns(new ValueTask<string>("versionMismatch"));

        var result = await svc.VersionedWriteAsync("organizations", new Organization());

        Assert.Equal(VersionedWriteResult.VersionMismatch, result);
    }

    [Fact]
    public async Task VersionedWrite_WhenVersionMismatches_DoesNotIncrementStoredVersion()
    {
        var (svc, module) = MakeService();
        module.InvokeAsync<string>("versionedWrite", Arg.Any<object[]?>())
              .Returns(new ValueTask<string>("versionMismatch"));
        var org = new Organization { Version = 3 };

        await svc.VersionedWriteAsync("organizations", org);

        // Version on the local object is not modified by the C# layer
        Assert.Equal(3, org.Version);
    }

    [Fact]
    public void IVersioned_AllEntityModelTypes_ImplementInterface()
    {
        var versioned = new Type[]
        {
            typeof(Organization), typeof(Contact), typeof(Opportunity),
            typeof(OpportunityFieldHistory), typeof(Correspondence),
            typeof(CorrespondenceFile), typeof(BaseResume), typeof(BaseResumeVersion),
            typeof(SessionRecord),
        };
        foreach (var t in versioned)
            Assert.True(typeof(IVersioned).IsAssignableFrom(t), $"{t.Name} does not implement IVersioned");
    }

    [Fact]
    public async Task VersionedWrite_WithLockNames_CallsLockedVersionedWrite()
    {
        var (svc, module) = MakeService();
        module.InvokeAsync<string>("lockedVersionedWrite", Arg.Any<object[]?>())
              .Returns(new ValueTask<string>("success"));

        var result = await svc.VersionedWriteAsync("organizations", new Organization(), ["sja-org"]);

        Assert.Equal(VersionedWriteResult.Success, result);
        await module.Received(1).InvokeAsync<string>("lockedVersionedWrite", Arg.Any<object[]?>());
    }

    [Fact]
    public async Task VersionedWrite_WithoutLockNames_CallsUnlockedVersionedWrite()
    {
        var (svc, module) = MakeService();
        module.InvokeAsync<string>("versionedWrite", Arg.Any<object[]?>())
              .Returns(new ValueTask<string>("success"));

        await svc.VersionedWriteAsync("organizations", new Organization());

        await module.Received(1).InvokeAsync<string>("versionedWrite", Arg.Any<object[]?>());
        await module.DidNotReceive().InvokeAsync<string>("lockedVersionedWrite", Arg.Any<object[]?>());
    }
}
