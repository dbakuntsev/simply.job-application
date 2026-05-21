using System.Diagnostics;

namespace Simply.JobApplication.Tools.QnA.Harness;

// Source-control anchor for a harness run. Captured once at startup and
// serialized into run-meta.json so an agent reading the artifacts months
// later can correlate them back to the exact commit (and whether the tree
// was dirty at the time, which makes the run non-reproducible from the SHA
// alone). DirtyFiles is truncated to keep run-meta.json small when many
// files are modified — DirtyFileCount is the authoritative count.
internal sealed record GitInfo(
    string                CommitSha,
    string                ShortSha,
    string                Branch,
    bool                  IsDirty,
    int                   DirtyFileCount,
    IReadOnlyList<string> DirtyFiles,
    string                CommitSubject);

internal static class GitProbe
{
    private const int DirtyFileSampleLimit = 25;

    // Returns null when the working directory is not a git repo, when git
    // is unavailable, or when any underlying command fails. The caller must
    // treat null as "no anchor" — equivalent to a dirty tree for gating
    // purposes, because results from an unanchored run cannot be tied back
    // to a commit.
    public static GitInfo? TryCapture(string workingDir)
    {
        try
        {
            var sha = Run(workingDir, "rev-parse HEAD")?.Trim();
            if (string.IsNullOrEmpty(sha)) return null;

            var branch  = Run(workingDir, "rev-parse --abbrev-ref HEAD")?.Trim() ?? "";
            var subject = Run(workingDir, "log -1 --pretty=%s")?.TrimEnd('\r', '\n') ?? "";
            var status  = Run(workingDir, "status --porcelain") ?? "";

            // `git status --porcelain` line format: "XY path" where XY are two
            // status code chars and the path starts at column 3. We strip XY
            // and keep the path for the dirty-file sample.
            var dirtyFiles = status
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Length > 3 ? l[3..].TrimEnd('\r') : l.TrimEnd('\r'))
                .Where(p => p.Length > 0)
                .ToArray();

            var sample = dirtyFiles.Length <= DirtyFileSampleLimit
                ? dirtyFiles
                : dirtyFiles.Take(DirtyFileSampleLimit).ToArray();

            return new GitInfo(
                CommitSha:      sha,
                ShortSha:       sha.Length >= 7 ? sha[..7] : sha,
                Branch:         branch,
                IsDirty:        dirtyFiles.Length > 0,
                DirtyFileCount: dirtyFiles.Length,
                DirtyFiles:     sample,
                CommitSubject:  subject);
        }
        catch
        {
            return null;
        }
    }

    private static string? Run(string workingDir, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        using var p = Process.Start(psi);
        if (p is null) return null;

        var stdout = p.StandardOutput.ReadToEnd();
        // Drain stderr too — leaving it buffered can deadlock long stderr
        // output. We do not surface the text; callers only need ExitCode.
        _ = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(5000))
        {
            try { p.Kill(); } catch { /* ignore */ }
            return null;
        }
        return p.ExitCode == 0 ? stdout : null;
    }
}
