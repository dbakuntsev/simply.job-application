using System.Text.Json;
using Microsoft.JSInterop;
using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services;

public class IndexedDbService : IIndexedDbService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Version token is replaced at build time by the InjectDependencyVersion MSBuild target.
    private const string _moduleUrl = "./js/indexeddb.js?v=271E3EE133DFD305D8B31C943A768726B196FB362A0FF99C5CC5B579840AF56D";

    public IndexedDbService(IJSRuntime js) => _js = js;

    private async Task<IJSObjectReference> ModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", _moduleUrl);
        return _module;
    }

    // ── Settings ────────────────────────────────────────────────────────────

    public async Task<AppSettings> GetSettingsAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getSetting", "appSettings");
        if (string.IsNullOrEmpty(raw)) return new AppSettings();
        return JsonSerializer.Deserialize<AppSettings>(raw, _jsonOpts) ?? new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var m = await ModuleAsync();
        var json = JsonSerializer.Serialize(settings, _jsonOpts);
        var obj = JsonSerializer.Deserialize<JsonElement>(json);
        await m.InvokeVoidAsync("setSetting", "appSettings", obj);
    }

    // ── Sessions ─────────────────────────────────────────────────────────────

    public async Task<List<SessionRecord>> GetAllSessionsAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getAllSessions");
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<SessionRecord>>(raw, _jsonOpts) ?? new();
    }

    public async Task<SessionRecord?> GetSessionAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getSession", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<SessionRecord>(raw, _jsonOpts);
    }

    public async Task SaveSessionAsync(SessionRecord session)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(session, _jsonOpts);
        await m.InvokeVoidAsync("saveSession", obj);
    }

    public async Task DeleteSessionAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteSession", id);
    }

    public async Task ClearSessionsAsync()
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("clearSessions");
    }

    // ── Files ────────────────────────────────────────────────────────────────

    public async Task<List<StoredFile>> GetAllFilesAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getAllFiles");
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<StoredFile>>(raw, _jsonOpts) ?? new();
    }

    public async Task<List<StoredFileMeta>> GetAllFileMetaAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getAllFileMeta");
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<StoredFileMeta>>(raw, _jsonOpts) ?? new();
    }

    public async Task<StoredFile?> GetFileAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getFile", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<StoredFile>(raw, _jsonOpts);
    }

    public async Task SaveFileAsync(StoredFile file)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(file, _jsonOpts);
        await m.InvokeVoidAsync("saveFile", obj);
    }

    public async Task DeleteFileAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteFile", id);
    }

    public async Task ClearFilesAsync()
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("clearFiles");
    }

    public async Task DownloadFileAsync(string fileName, byte[] data)
    {
        var m = await ModuleAsync();
        var base64 = Convert.ToBase64String(data);
        await m.InvokeVoidAsync("downloadBlob", fileName, base64);
    }

    // ── Schema version ───────────────────────────────────────────────────────

    public async Task<int> GetSchemaVersionAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getSetting", "schemaVersion");
        if (string.IsNullOrEmpty(raw)) return 0;
        var elem = JsonSerializer.Deserialize<JsonElement>(raw);
        return elem.ValueKind == JsonValueKind.Number ? elem.GetInt32() : 0;
    }

    public async Task SetSchemaVersionAsync(int version)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("setSetting", "schemaVersion", version);
    }

    // ── Lookup tables ────────────────────────────────────────────────────────

    public async Task<List<LookupValue>> GetLookupValuesAsync(string tableName)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getLookupValues", tableName);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<LookupValue>>(raw, _jsonOpts) ?? new();
    }

    public async Task AddLookupValueAsync(string tableName, LookupValue value)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(value, _jsonOpts);
        await m.InvokeVoidAsync("addLookupValue", tableName, obj);
    }

    // ── Organizations ────────────────────────────────────────────────────────

    public async Task<List<Organization>> GetAllOrganizationsAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getAllOrganizations");
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<Organization>>(raw, _jsonOpts) ?? new();
    }

    public async Task<Organization?> GetOrganizationAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getOrganization", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<Organization>(raw, _jsonOpts);
    }

    public async Task SaveOrganizationAsync(Organization org)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(org, _jsonOpts);
        await m.InvokeVoidAsync("saveOrganization", obj);
    }

    public async Task DeleteOrganizationAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteOrganization", id);
    }

    // ── Contacts ─────────────────────────────────────────────────────────────

    public async Task<List<Contact>> GetContactsByOrganizationAsync(string orgId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getContactsByOrganization", orgId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<Contact>>(raw, _jsonOpts) ?? new();
    }

    public async Task<Contact?> GetContactAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getContact", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<Contact>(raw, _jsonOpts);
    }

    public async Task SaveContactAsync(Contact contact)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(contact, _jsonOpts);
        await m.InvokeVoidAsync("saveContact", obj);
    }

    public async Task DeleteContactAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteContact", id);
    }

    // ── ContactOpportunityRoles ───────────────────────────────────────────────

    public async Task<List<ContactOpportunityRole>> GetRolesByOpportunityAsync(string oppId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getRolesByOpportunity", oppId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<ContactOpportunityRole>>(raw, _jsonOpts) ?? new();
    }

    public async Task<List<ContactOpportunityRole>> GetRolesByContactAsync(string contactId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getRolesByContact", contactId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<ContactOpportunityRole>>(raw, _jsonOpts) ?? new();
    }

    public async Task<ContactOpportunityRole?> GetRoleAsync(string contactId, string opportunityId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getRole", contactId, opportunityId);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<ContactOpportunityRole>(raw, _jsonOpts);
    }

    public async Task SaveRoleAsync(ContactOpportunityRole role)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(role, _jsonOpts);
        await m.InvokeVoidAsync("saveRole", obj);
    }

    public async Task DeleteRoleAsync(string contactId, string opportunityId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteRole", contactId, opportunityId);
    }

    public async Task DeleteRolesByContactAsync(string contactId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteRolesByContact", contactId);
    }

    public async Task DeleteRolesByOpportunityAsync(string opportunityId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteRolesByOpportunity", opportunityId);
    }

    // ── Opportunities ─────────────────────────────────────────────────────────

    public async Task<List<Opportunity>> GetOpportunitiesByOrganizationAsync(string orgId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getOpportunitiesByOrganization", orgId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<Opportunity>>(raw, _jsonOpts) ?? new();
    }

    public async Task<List<Opportunity>> GetAllOpportunitiesAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getAllOpportunities");
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<Opportunity>>(raw, _jsonOpts) ?? new();
    }

    public async Task<Opportunity?> GetOpportunityAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getOpportunity", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<Opportunity>(raw, _jsonOpts);
    }

    public async Task SaveOpportunityAsync(Opportunity opportunity)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(opportunity, _jsonOpts);
        await m.InvokeVoidAsync("saveOpportunity", obj);
    }

    public async Task DeleteOpportunityAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteOpportunity", id);
    }

    // ── OpportunityFieldHistory ───────────────────────────────────────────────

    public async Task<List<OpportunityFieldHistory>> GetHistoryByOpportunityAsync(string oppId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getHistoryByOpportunity", oppId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<OpportunityFieldHistory>>(raw, _jsonOpts) ?? new();
    }

    public async Task SaveHistoryEntryAsync(OpportunityFieldHistory entry)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(entry, _jsonOpts);
        await m.InvokeVoidAsync("saveHistoryEntry", obj);
    }

    public async Task DeleteHistoryByOpportunityAsync(string oppId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteHistoryByOpportunity", oppId);
    }

    // ── Correspondence ────────────────────────────────────────────────────────

    public async Task<List<Correspondence>> GetCorrespondenceByOpportunityAsync(string oppId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getCorrespondenceByOpportunity", oppId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<Correspondence>>(raw, _jsonOpts) ?? new();
    }

    public async Task<Correspondence?> GetCorrespondenceAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getCorrespondence", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<Correspondence>(raw, _jsonOpts);
    }

    public async Task SaveCorrespondenceAsync(Correspondence correspondence)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(correspondence, _jsonOpts);
        await m.InvokeVoidAsync("saveCorrespondence", obj);
    }

    public async Task DeleteCorrespondenceAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteCorrespondence", id);
    }

    public async Task DeleteCorrespondenceByOpportunityAsync(string oppId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteCorrespondenceByOpportunity", oppId);
    }

    // ── CorrespondenceFiles ───────────────────────────────────────────────────

    public async Task<List<CorrespondenceFile>> GetFilesByCorrespondenceAsync(string corrId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getFilesByCorrespondence", corrId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<CorrespondenceFile>>(raw, _jsonOpts) ?? new();
    }

    public async Task<CorrespondenceFile?> GetCorrespondenceFileAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getCorrespondenceFile", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<CorrespondenceFile>(raw, _jsonOpts);
    }

    public async Task SaveCorrespondenceFileAsync(CorrespondenceFile file)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(file, _jsonOpts);
        await m.InvokeVoidAsync("saveCorrespondenceFile", obj);
    }

    public async Task DeleteCorrespondenceFileAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteCorrespondenceFile", id);
    }

    public async Task DeleteFilesByCorrespondenceAsync(string corrId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteFilesByCorrespondence", corrId);
    }

    // ── BaseResumes ───────────────────────────────────────────────────────────

    public async Task<List<BaseResume>> GetAllBaseResumesAsync()
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getAllBaseResumes");
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<BaseResume>>(raw, _jsonOpts) ?? new();
    }

    public async Task<BaseResume?> GetBaseResumeAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getBaseResume", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<BaseResume>(raw, _jsonOpts);
    }

    public async Task SaveBaseResumeAsync(BaseResume resume)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(resume, _jsonOpts);
        await m.InvokeVoidAsync("saveBaseResume", obj);
    }

    public async Task DeleteBaseResumeAsync(string id)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteBaseResume", id);
    }

    // ── BaseResumeVersions ────────────────────────────────────────────────────

    public async Task<List<BaseResumeVersion>> GetVersionsByResumeAsync(string resumeId)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getVersionsByResume", resumeId);
        if (string.IsNullOrEmpty(raw)) return new();
        return JsonSerializer.Deserialize<List<BaseResumeVersion>>(raw, _jsonOpts) ?? new();
    }

    public async Task<BaseResumeVersion?> GetBaseResumeVersionAsync(string id)
    {
        var m = await ModuleAsync();
        var raw = await m.InvokeAsync<string?>("getBaseResumeVersion", id);
        if (string.IsNullOrEmpty(raw)) return null;
        return JsonSerializer.Deserialize<BaseResumeVersion>(raw, _jsonOpts);
    }

    public async Task SaveBaseResumeVersionAsync(BaseResumeVersion version)
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(version, _jsonOpts);
        await m.InvokeVoidAsync("saveBaseResumeVersion", obj);
    }

    public async Task DeleteVersionsByResumeAsync(string resumeId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteVersionsByResume", resumeId);
    }

    // ── Cascade deletes ───────────────────────────────────────────────────────

    public async Task DeleteOrganizationCascadeAsync(string orgId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteOrganizationCascade", orgId);
    }

    public async Task DeleteOpportunityCascadeAsync(string oppId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteOpportunityCascade", oppId);
    }

    public async Task DeleteContactCascadeAsync(string contactId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteContactCascade", contactId);
    }

    public async Task DeleteCorrespondenceCascadeAsync(string corrId)
    {
        var m = await ModuleAsync();
        await m.InvokeVoidAsync("deleteCorrespondenceCascade", corrId);
    }

    // ── Versioned write ──────────────────────────────────────────────────────

    // lockNames: ordered lock chain to acquire (outer→inner) per §10.3.
    // Pass null or empty to skip locking (for read-only / non-versioned ops).
    public async Task<VersionedWriteResult> VersionedWriteAsync<T>(
        string storeName, T record, string[]? lockNames = null) where T : IVersioned
    {
        var m = await ModuleAsync();
        var obj = JsonSerializer.SerializeToElement(record, _jsonOpts);
        string result;
        if (lockNames is { Length: > 0 })
            result = await m.InvokeAsync<string>("lockedVersionedWrite", lockNames, storeName, obj);
        else
            result = await m.InvokeAsync<string>("versionedWrite", storeName, obj);
        return result switch
        {
            "success"  => VersionedWriteResult.Success,
            "notFound" => VersionedWriteResult.NotFound,
            _          => VersionedWriteResult.VersionMismatch,
        };
    }

    // ── Retention enforcement ────────────────────────────────────────────────

    public async Task EnforceSessionLimitAsync(int limit)
    {
        var all = await GetAllSessionsAsync();
        var ordered = all.OrderBy(s => s.CreatedAt).ToList();
        while (ordered.Count > limit)
        {
            await DeleteSessionAsync(ordered[0].Id);
            ordered.RemoveAt(0);
        }
    }

    public async Task EnforceFileLimitAsync(int limit)
    {
        var all = await GetAllFileMetaAsync();
        var ordered = all.OrderBy(f => f.LastUsedAt).ToList();
        while (ordered.Count > limit)
        {
            await DeleteFileAsync(ordered[0].Id);
            ordered.RemoveAt(0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
