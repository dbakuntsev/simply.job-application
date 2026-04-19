using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services;

public interface IIndexedDbService
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);

    Task<List<SessionRecord>> GetAllSessionsAsync();
    Task<SessionRecord?> GetSessionAsync(string id);
    Task SaveSessionAsync(SessionRecord session);
    Task DeleteSessionAsync(string id);
    Task ClearSessionsAsync();

    Task<List<StoredFile>> GetAllFilesAsync();
    Task<List<StoredFileMeta>> GetAllFileMetaAsync();
    Task<StoredFile?> GetFileAsync(string id);
    Task SaveFileAsync(StoredFile file);
    Task DeleteFileAsync(string id);
    Task ClearFilesAsync();

    Task DownloadFileAsync(string fileName, byte[] data);
    Task EnforceSessionLimitAsync(int limit);
    Task EnforceFileLimitAsync(int limit);

    Task<VersionedWriteResult> VersionedWriteAsync<T>(string storeName, T record, string[]? lockNames = null) where T : IVersioned;

    Task<int> GetSchemaVersionAsync();
    Task SetSchemaVersionAsync(int version);

    Task<List<LookupValue>> GetLookupValuesAsync(string tableName);
    Task AddLookupValueAsync(string tableName, LookupValue value);

    Task<List<Organization>> GetAllOrganizationsAsync();
    Task<Organization?> GetOrganizationAsync(string id);
    Task SaveOrganizationAsync(Organization org);
    Task DeleteOrganizationAsync(string id);

    Task<Dictionary<string, int>> GetContactsCountPerOrganizationAsync();
    Task<List<Contact>> GetContactsByOrganizationAsync(string orgId);
    Task<Contact?> GetContactAsync(string id);
    Task SaveContactAsync(Contact contact);
    Task DeleteContactAsync(string id);

    Task<List<ContactOpportunityRole>> GetRolesByOpportunityAsync(string oppId);
    Task<List<ContactOpportunityRole>> GetRolesByContactAsync(string contactId);
    Task<ContactOpportunityRole?> GetRoleAsync(string contactId, string opportunityId);
    Task SaveRoleAsync(ContactOpportunityRole role);
    Task DeleteRoleAsync(string contactId, string opportunityId);
    Task DeleteRolesByContactAsync(string contactId);
    Task DeleteRolesByOpportunityAsync(string opportunityId);

    Task<List<Opportunity>> GetOpportunitiesByOrganizationAsync(string orgId);
    Task<List<Opportunity>> GetAllOpportunitiesAsync();
    Task<Opportunity?> GetOpportunityAsync(string id);
    Task SaveOpportunityAsync(Opportunity opportunity);
    Task DeleteOpportunityAsync(string id);

    Task<List<OpportunityFieldHistory>> GetHistoryByOpportunityAsync(string oppId);
    Task SaveHistoryEntryAsync(OpportunityFieldHistory entry);
    Task DeleteHistoryByOpportunityAsync(string oppId);

    Task<List<Correspondence>> GetCorrespondenceByOpportunityAsync(string oppId);
    Task<Correspondence?> GetCorrespondenceAsync(string id);
    Task SaveCorrespondenceAsync(Correspondence correspondence);
    Task DeleteCorrespondenceAsync(string id);
    Task DeleteCorrespondenceByOpportunityAsync(string oppId);

    Task<List<CorrespondenceFile>> GetFilesByCorrespondenceAsync(string corrId);
    Task<CorrespondenceFile?> GetCorrespondenceFileAsync(string id);
    Task SaveCorrespondenceFileAsync(CorrespondenceFile file);
    Task DeleteCorrespondenceFileAsync(string id);
    Task DeleteFilesByCorrespondenceAsync(string corrId);

    Task<List<BaseResume>> GetAllBaseResumesAsync();
    Task<BaseResume?> GetBaseResumeAsync(string id);
    Task SaveBaseResumeAsync(BaseResume resume);
    Task DeleteBaseResumeAsync(string id);

    Task<List<BaseResumeVersion>> GetVersionsByResumeAsync(string resumeId);
    Task<BaseResumeVersion?> GetBaseResumeVersionAsync(string id);
    Task SaveBaseResumeVersionAsync(BaseResumeVersion version);
    Task DeleteVersionsByResumeAsync(string resumeId);

    Task DeleteOrganizationCascadeAsync(string orgId);
    Task DeleteOpportunityCascadeAsync(string oppId);
    Task DeleteContactCascadeAsync(string contactId);
    Task DeleteCorrespondenceCascadeAsync(string corrId);
}
