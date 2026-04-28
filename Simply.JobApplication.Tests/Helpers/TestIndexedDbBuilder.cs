namespace Simply.JobApplication.Tests.Helpers;

public class TestIndexedDbBuilder
{
    private readonly IIndexedDbService _db = Substitute.For<IIndexedDbService>();

    public TestIndexedDbBuilder()
    {
        _db.GetSchemaVersionAsync().Returns(Task.FromResult(2));
        _db.GetSettingsAsync().Returns(Task.FromResult(new AppSettings()));
        _db.GetStoreBytesAsync(Arg.Any<string[]>()).Returns(Task.FromResult(new Dictionary<string, long>()));

        _db.GetAllOrganizationsAsync().Returns(Task.FromResult(new List<Organization>()));
        _db.GetOrganizationAsync(Arg.Any<string>()).Returns(Task.FromResult<Organization?>(null));
        _db.GetContactsCountPerOrganizationAsync().Returns(Task.FromResult(new Dictionary<string, int>()));

        _db.GetContactsByOrganizationAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<Contact>()));
        _db.GetContactAsync(Arg.Any<string>()).Returns(Task.FromResult<Contact?>(null));

        _db.GetRolesByOpportunityAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<ContactOpportunityRole>()));
        _db.GetRolesByContactAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<ContactOpportunityRole>()));
        _db.GetRoleAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult<ContactOpportunityRole?>(null));

        _db.GetAllOpportunitiesAsync().Returns(Task.FromResult(new List<Opportunity>()));
        _db.GetOpportunitiesByOrganizationAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<Opportunity>()));
        _db.GetOpportunityAsync(Arg.Any<string>()).Returns(Task.FromResult<Opportunity?>(null));

        _db.GetHistoryByOpportunityAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<OpportunityFieldHistory>()));

        _db.GetCorrespondenceByOpportunityAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<Correspondence>()));
        _db.GetCorrespondenceAsync(Arg.Any<string>()).Returns(Task.FromResult<Correspondence?>(null));
        _db.GetCorrespondenceFileCountAsync(Arg.Any<string>()).Returns(Task.FromResult(0));

        _db.GetFilesByCorrespondenceAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<CorrespondenceFile>()));
        _db.GetCorrespondenceFileAsync(Arg.Any<string>()).Returns(Task.FromResult<CorrespondenceFile?>(null));

        _db.GetAllBaseResumesAsync().Returns(Task.FromResult(new List<BaseResume>()));
        _db.GetBaseResumeAsync(Arg.Any<string>()).Returns(Task.FromResult<BaseResume?>(null));

        _db.GetVersionsByResumeAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<BaseResumeVersion>()));
        _db.GetBaseResumeVersionAsync(Arg.Any<string>()).Returns(Task.FromResult<BaseResumeVersion?>(null));

        _db.GetLookupValuesAsync(Arg.Any<string>()).Returns(Task.FromResult(new List<LookupValue>()));

        _db.GetAllSessionsAsync().Returns(Task.FromResult(new List<SessionRecord>()));
        _db.GetSessionAsync(Arg.Any<string>()).Returns(Task.FromResult<SessionRecord?>(null));

        _db.GetOrganizationProjectionsAsync().Returns(Task.FromResult(new List<OrganizationProjection>()));
        _db.GetOpportunityProjectionsAsync().Returns(Task.FromResult(new List<OpportunityProjection>()));

        _db.SaveCorrespondenceWithFilesAsync(
            Arg.Any<Correspondence>(), Arg.Any<List<CorrespondenceFile>>(), Arg.Any<List<string>>())
           .Returns(Task.FromResult(VersionedWriteResult.Success));

        _db.VersionedWriteAsync<Organization>(Arg.Any<string>(), Arg.Any<Organization>(), Arg.Any<string[]?>())
           .Returns(Task.FromResult(VersionedWriteResult.Success));
        _db.VersionedWriteAsync<Opportunity>(Arg.Any<string>(), Arg.Any<Opportunity>(), Arg.Any<string[]?>())
           .Returns(Task.FromResult(VersionedWriteResult.Success));
        _db.VersionedWriteAsync<Correspondence>(Arg.Any<string>(), Arg.Any<Correspondence>(), Arg.Any<string[]?>())
           .Returns(Task.FromResult(VersionedWriteResult.Success));
        _db.VersionedWriteAsync<Contact>(Arg.Any<string>(), Arg.Any<Contact>(), Arg.Any<string[]?>())
           .Returns(Task.FromResult(VersionedWriteResult.Success));
        _db.VersionedWriteAsync<BaseResume>(Arg.Any<string>(), Arg.Any<BaseResume>(), Arg.Any<string[]?>())
           .Returns(Task.FromResult(VersionedWriteResult.Success));
    }

    public TestIndexedDbBuilder WithOrganizations(params Organization[] orgs)
    {
        _db.GetAllOrganizationsAsync().Returns(Task.FromResult(orgs.ToList()));
        return this;
    }

    public TestIndexedDbBuilder WithOrganization(Organization org)
    {
        _db.GetOrganizationAsync(org.Id).Returns(Task.FromResult<Organization?>(org));
        return this;
    }

    public TestIndexedDbBuilder WithOpportunities(params Opportunity[] opps)
    {
        _db.GetAllOpportunitiesAsync().Returns(Task.FromResult(opps.ToList()));
        foreach (var g in opps.GroupBy(o => o.OrganizationId))
            _db.GetOpportunitiesByOrganizationAsync(g.Key).Returns(Task.FromResult(g.ToList()));
        return this;
    }

    public TestIndexedDbBuilder WithOpportunity(Opportunity opp)
    {
        _db.GetOpportunityAsync(opp.Id).Returns(Task.FromResult<Opportunity?>(opp));
        return this;
    }

    public TestIndexedDbBuilder WithContactCounts(Dictionary<string, int> counts)
    {
        _db.GetContactsCountPerOrganizationAsync().Returns(Task.FromResult(counts));
        return this;
    }

    public TestIndexedDbBuilder WithContacts(string orgId, params Contact[] contacts)
    {
        _db.GetContactsByOrganizationAsync(orgId).Returns(Task.FromResult(contacts.ToList()));
        foreach (var c in contacts)
            _db.GetContactAsync(c.Id).Returns(Task.FromResult<Contact?>(c));
        return this;
    }

    public TestIndexedDbBuilder WithRolesByOpportunity(string oppId, params ContactOpportunityRole[] roles)
    {
        _db.GetRolesByOpportunityAsync(oppId).Returns(Task.FromResult(roles.ToList()));
        return this;
    }

    public TestIndexedDbBuilder WithBaseResumes(params BaseResume[] resumes)
    {
        _db.GetAllBaseResumesAsync().Returns(Task.FromResult(resumes.ToList()));
        foreach (var r in resumes)
            _db.GetBaseResumeAsync(r.Id).Returns(Task.FromResult<BaseResume?>(r));
        return this;
    }

    public TestIndexedDbBuilder WithResumeVersions(string resumeId, params BaseResumeVersion[] versions)
    {
        _db.GetVersionsByResumeAsync(resumeId).Returns(Task.FromResult(versions.ToList()));
        foreach (var v in versions)
            _db.GetBaseResumeVersionAsync(v.Id).Returns(Task.FromResult<BaseResumeVersion?>(v));
        return this;
    }

    public TestIndexedDbBuilder WithCorrespondence(string oppId, params Correspondence[] items)
    {
        _db.GetCorrespondenceByOpportunityAsync(oppId).Returns(Task.FromResult(items.ToList()));
        foreach (var c in items)
            _db.GetCorrespondenceAsync(c.Id).Returns(Task.FromResult<Correspondence?>(c));
        return this;
    }

    public TestIndexedDbBuilder WithFiles(string corrId, params CorrespondenceFile[] files)
    {
        _db.GetFilesByCorrespondenceAsync(corrId).Returns(Task.FromResult(files.ToList()));
        _db.GetCorrespondenceFileCountAsync(corrId).Returns(Task.FromResult(files.Length));
        return this;
    }

    public TestIndexedDbBuilder WithLookupValues(string tableName, params LookupValue[] values)
    {
        _db.GetLookupValuesAsync(tableName).Returns(Task.FromResult(values.ToList()));
        return this;
    }

    public TestIndexedDbBuilder WithSchemaVersion(int version)
    {
        _db.GetSchemaVersionAsync().Returns(Task.FromResult(version));
        return this;
    }

    public TestIndexedDbBuilder WithHistory(string oppId, params OpportunityFieldHistory[] entries)
    {
        _db.GetHistoryByOpportunityAsync(oppId).Returns(Task.FromResult(entries.ToList()));
        return this;
    }

    public TestIndexedDbBuilder WithSessions(params SessionRecord[] sessions)
    {
        _db.GetAllSessionsAsync().Returns(Task.FromResult(sessions.ToList()));
        foreach (var s in sessions)
            _db.GetSessionAsync(s.Id).Returns(Task.FromResult<SessionRecord?>(s));
        return this;
    }

    public TestIndexedDbBuilder WithOrganizationProjections(params OrganizationProjection[] projs)
    {
        _db.GetOrganizationProjectionsAsync().Returns(Task.FromResult(projs.ToList()));
        return this;
    }

    public TestIndexedDbBuilder WithOpportunityProjections(params OpportunityProjection[] projs)
    {
        _db.GetOpportunityProjectionsAsync().Returns(Task.FromResult(projs.ToList()));
        return this;
    }

    public TestIndexedDbBuilder WithSettings(AppSettings settings)
    {
        _db.GetSettingsAsync().Returns(Task.FromResult(settings));
        return this;
    }

    public IIndexedDbService Build() => _db;
}
