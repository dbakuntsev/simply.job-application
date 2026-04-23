using Simply.JobApplication.Models;

namespace Simply.JobApplication.Services;

public static class SessionDisplayHelper
{
    public record EntityDisplay(string Text, string? Url);

    public static EntityDisplay ResolveOrg(
        SessionRecord s,
        Dictionary<string, OrganizationProjection> orgMap)
    {
        if (s.OrganizationId is null)
            return new(s.OrganizationNameSnapshot, null);
        if (!orgMap.TryGetValue(s.OrganizationId, out var org))
            return new($"(deleted: {s.OrganizationNameSnapshot})", null);
        var text = org.Name == s.OrganizationNameSnapshot
            ? org.Name
            : $"{org.Name} (formerly: {s.OrganizationNameSnapshot})";
        return new(text, $"/organizations/{s.OrganizationId}");
    }

    public static EntityDisplay ResolveOpp(
        SessionRecord s,
        Dictionary<string, OpportunityProjection> oppMap)
    {
        if (s.OpportunityId is null)
            return new("", null);
        if (!oppMap.TryGetValue(s.OpportunityId, out var opp))
            return new($"(deleted: {s.OpportunityRoleSnapshot})", null);
        var text = opp.Role == s.OpportunityRoleSnapshot
            ? opp.Role
            : $"{opp.Role} (formerly: {s.OpportunityRoleSnapshot})";
        return new(text, $"/opportunities/{s.OpportunityId}");
    }

    public static string ResolveResumeName(SessionRecord s, string? currentResumeName)
    {
        var vStr = $"(v{s.BaseResumeVersionNumberSnapshot})";
        if (currentResumeName is null)
            return $"(deleted: {s.BaseResumeNameSnapshot}) {vStr}";
        if (currentResumeName == s.BaseResumeNameSnapshot)
            return $"{currentResumeName} {vStr}";
        return $"{currentResumeName} (formerly: {s.BaseResumeNameSnapshot}) {vStr}";
    }
}
