using System.Text.Json;
using Microsoft.JSInterop;
using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services;

public class IndexedDbService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public IndexedDbService(IJSRuntime js) => _js = js;

    private async Task<IJSObjectReference> ModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/indexeddb.js");
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
