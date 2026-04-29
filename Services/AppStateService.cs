namespace Simply.JobApplication.Services;

public class AppStateService
{
    // ── Pre-Evaluate state preservation ──────────────────────────────────────
    // Saved on navigation away when no session has been written this page session.
    // Restored on return to E&G page (unless contextual nav overrides).
    public string? PreservedOrgText { get; set; }
    public string? PreservedLinkedOrgId { get; set; }
    public string? PreservedLinkedOrgName { get; set; }
    public string? PreservedLinkedOppId { get; set; }
    public string? PreservedLinkedOppRole { get; set; }
    public string? PreservedLinkedOppRoleDesc { get; set; }
    public string? PreservedRole { get; set; }
    public string? PreservedRoleDescription { get; set; }
    public string? PreservedResumeId { get; set; }   // BaseResume.Id only; version resolved fresh on return

    public bool HasPreservedState => PreservedOrgText is not null;

    public void ClearPreservedState()
    {
        PreservedOrgText = null;
        PreservedLinkedOrgId = null;
        PreservedLinkedOrgName = null;
        PreservedLinkedOppId = null;
        PreservedLinkedOppRole = null;
        PreservedLinkedOppRoleDesc = null;
        PreservedRole = null;
        PreservedRoleDescription = null;
        PreservedResumeId = null;
    }

    // ── Contextual navigation (from Opportunity Detail "E&G" button) ─────────
    // When set, E&G page overrides any preserved pre-evaluate state with these values.
    public string? ContextualOrgId { get; set; }
    public string? ContextualOrgName { get; set; }
    public string? ContextualOppId { get; set; }
    public string? ContextualOppRole { get; set; }
    public string? ContextualOppRoleDesc { get; set; }

    public bool HasContextualNav => ContextualOrgId is not null;

    public void ClearContextualNav()
    {
        ContextualOrgId = null;
        ContextualOrgName = null;
        ContextualOppId = null;
        ContextualOppRole = null;
        ContextualOppRoleDesc = null;
    }
}
