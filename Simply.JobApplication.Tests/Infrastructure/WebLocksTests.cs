namespace Simply.JobApplication.Tests.Infrastructure;

// M1-8: Web Locks — verify lock names are passed to the correct JS function.
public class WebLocksTests
{
    private static (IndexedDbService svc, IJSObjectReference module) MakeService()
    {
        var js     = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));
        module.InvokeAsync<string>("lockedVersionedWrite", Arg.Any<object[]?>())
              .Returns(new ValueTask<string>("success"));
        module.InvokeAsync<string>("versionedWrite", Arg.Any<object[]?>())
              .Returns(new ValueTask<string>("success"));
        return (new IndexedDbService(js), module);
    }

    [Fact]
    public async Task AcquireLock_InvokesNavigatorLocksRequest_WithCorrectLockName()
    {
        var (svc, module) = MakeService();
        var lockNames = new[] { "sja-org" };

        await svc.VersionedWriteAsync("organizations", new Organization(), lockNames);

        await module.Received(1).InvokeAsync<string>(
            "lockedVersionedWrite",
            Arg.Is<object[]?>(a => a != null && ((object[])a[0])[0].ToString() == "sja-org"));
    }

    [Fact]
    public async Task InsertWithUniquenessConstraint_AcquiresTopLevelEntityLock()
    {
        var (svc, module) = MakeService();
        // A uniqueness-constraint lock uses the top-level store lock, e.g. "sja-org"
        await svc.VersionedWriteAsync("organizations", new Organization(), ["sja-org"]);

        await module.Received(1).InvokeAsync<string>("lockedVersionedWrite", Arg.Any<object[]?>());
    }

    [Fact]
    public async Task UpdateEntity_AcquiresFullParentChainThenEntityLock()
    {
        var (svc, module) = MakeService();
        // Updating a contact acquires org lock then contact lock: ["sja-org:<id>", "sja-contact:<id>"]
        var lockChain = new[] { "sja-org:org-1", "sja-contact:c-1" };
        await svc.VersionedWriteAsync("contacts", new Contact(), lockChain);

        await module.Received(1).InvokeAsync<string>(
            "lockedVersionedWrite",
            Arg.Is<object[]?>(a => a != null &&
                ((object[])a[0]).Length == 2 &&
                ((object[])a[0])[0].ToString()!.StartsWith("sja-org") &&
                ((object[])a[0])[1].ToString()!.StartsWith("sja-contact")));
    }
}
