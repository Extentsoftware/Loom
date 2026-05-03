namespace Loom.Integrations.GitSync;

/// <summary>
/// Compiles the project's effective fragment set into <c>CLAUDE.md</c> and
/// <c>.cursorrules</c> in a target git repository, then commits and pushes.
/// Triggered by the FragmentVersionPublished domain event in Phase 3+;
/// callable on-demand from the Settings page in Phase 2.
/// </summary>
public interface IGitSyncService
{
    /// <summary>
    /// Compile + write the rule files to a working copy at <paramref name="workingCopyPath"/>.
    /// Idempotent: if no fragments have changed since the last run, no files
    /// are written and no commit is made. Returns a summary describing what
    /// happened.
    /// </summary>
    Task<GitSyncResult> SyncAsync(
        Guid? projectId,
        string workingCopyPath,
        GitSyncOptions options,
        CancellationToken ct = default);
}

public sealed record GitSyncOptions(
    string CommitMessage,
    string AuthorName,
    string AuthorEmail,
    bool Push);

public sealed record GitSyncResult(
    bool Changed,
    string? CommitSha,
    int FragmentCount,
    IReadOnlyList<string> WrittenFiles);
