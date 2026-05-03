using LibGit2Sharp;
using Loom.Application.Abstractions;
using Loom.Domain.Fragments;

namespace Loom.Integrations.GitSync;

/// <summary>
/// LibGit2Sharp-based GitSync. Walks the project's effective fragment set,
/// compiles to CLAUDE.md + .cursorrules, writes them into the working
/// copy, and commits if anything changed. Push is opt-in via
/// <see cref="GitSyncOptions.Push"/>; remote credentials are read from
/// the standard libgit2 config (the host process's git identity).
/// </summary>
public sealed class LibGit2GitSyncService(
    IFragmentRepository fragments,
    IProjectRepository projects,
    ISystemClock clock) : IGitSyncService
{
    public async Task<GitSyncResult> SyncAsync(
        Guid? projectId,
        string workingCopyPath,
        GitSyncOptions options,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingCopyPath);
        ArgumentNullException.ThrowIfNull(options);

        var globals = await fragments.ListGlobalAsync(ct);
        var projectFragments = projectId is Guid pid
            ? await fragments.ListByProjectAsync(pid, ct)
            : (IReadOnlyList<Fragment>)[];
        var combined = globals.Concat(projectFragments).ToList();

        var projectName = "global";
        if (projectId is Guid p && await projects.GetAsync(p, ct) is { } proj)
        {
            projectName = proj.Name;
        }

        var compiled = FragmentRulesCompiler.Compile(combined, projectName);

        var written = new List<string>();
        var claudePath = Path.Combine(workingCopyPath, FragmentRulesCompiler.ClaudeFile);
        var cursorPath = Path.Combine(workingCopyPath, FragmentRulesCompiler.CursorFile);
        if (await WriteIfChangedAsync(claudePath, compiled, ct))
        {
            written.Add(FragmentRulesCompiler.ClaudeFile);
        }
        if (await WriteIfChangedAsync(cursorPath, compiled, ct))
        {
            written.Add(FragmentRulesCompiler.CursorFile);
        }

        if (written.Count == 0)
        {
            return new GitSyncResult(Changed: false, CommitSha: null, FragmentCount: combined.Count, WrittenFiles: []);
        }

        // Commit + optional push.
        using var repo = new Repository(workingCopyPath);
        Commands.Stage(repo, FragmentRulesCompiler.ClaudeFile);
        Commands.Stage(repo, FragmentRulesCompiler.CursorFile);

        var sig = new Signature(options.AuthorName, options.AuthorEmail, clock.UtcNow);
        var commit = repo.Commit(options.CommitMessage, sig, sig, new CommitOptions { AllowEmptyCommit = false });

        if (options.Push)
        {
            var remote = repo.Network.Remotes.FirstOrDefault();
            if (remote is not null)
            {
                repo.Network.Push(remote, repo.Head.CanonicalName);
            }
        }

        return new GitSyncResult(
            Changed: true,
            CommitSha: commit.Sha,
            FragmentCount: combined.Count,
            WrittenFiles: written);
    }

    private static async Task<bool> WriteIfChangedAsync(string path, string content, CancellationToken ct)
    {
        if (File.Exists(path))
        {
            var existing = await File.ReadAllTextAsync(path, ct);
            if (string.Equals(existing, content, StringComparison.Ordinal))
            {
                return false;
            }
        }
        await File.WriteAllTextAsync(path, content, ct);
        return true;
    }
}
