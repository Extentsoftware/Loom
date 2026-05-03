using FluentAssertions;
using LibGit2Sharp;
using Loom.Application.Tests.Fakes;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Integrations.GitSync;
using Xunit;

namespace Loom.Integrations.Tests;

/// <summary>
/// Round-trips GitSync against a temp git repository on disk. No network;
/// the test creates the repo, runs the sync, asserts files written + commit
/// made, then runs the sync again and asserts no second commit (idempotent).
/// </summary>
public sealed class GitSyncTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly string _workingCopy;
    private readonly string _bareRemote;

    public GitSyncTests()
    {
        _workingCopy = Path.Combine(Path.GetTempPath(), $"loom-gitsync-wc-{Guid.NewGuid():N}");
        _bareRemote = Path.Combine(Path.GetTempPath(), $"loom-gitsync-bare-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_workingCopy);
        Repository.Init(_bareRemote, isBare: true);
        Repository.Init(_workingCopy);

        // Initial commit so the repo has a HEAD branch.
        using var repo = new Repository(_workingCopy);
        File.WriteAllText(Path.Combine(_workingCopy, ".gitkeep"), "");
        Commands.Stage(repo, ".gitkeep");
        var sig = new Signature("test", "test@example.com", Now);
        repo.Commit("init", sig, sig, new CommitOptions { AllowEmptyCommit = false });
    }

    public void Dispose()
    {
        SafeDelete(_workingCopy);
        SafeDelete(_bareRemote);
    }

    [Fact]
    public async Task SyncAsync_WritesFiles_AndCommitsOnFirstRun()
    {
        var (svc, fragments, projects) = MakeService();
        SeedFragment(fragments, FragmentCategory.Identity, "po-discovery", "you are a PO");
        SeedFragment(fragments, FragmentCategory.Methodology, "definition-of-ready", "feature is ready when...");

        var first = await svc.SyncAsync(
            projectId: null,
            workingCopyPath: _workingCopy,
            options: new GitSyncOptions("loom: sync rules", "Loom Bot", "bot@loom.local", Push: false));

        first.Changed.Should().BeTrue();
        first.WrittenFiles.Should().BeEquivalentTo([FragmentRulesCompiler.ClaudeFile, FragmentRulesCompiler.CursorFile]);
        first.CommitSha.Should().NotBeNull();

        var claudeContent = await File.ReadAllTextAsync(Path.Combine(_workingCopy, FragmentRulesCompiler.ClaudeFile));
        claudeContent.Should().Contain("po-discovery").And.Contain("definition-of-ready");
    }

    [Fact]
    public async Task SyncAsync_IsIdempotent_WhenFragmentsHaventChanged()
    {
        var (svc, fragments, projects) = MakeService();
        SeedFragment(fragments, FragmentCategory.Identity, "po-discovery", "you are a PO");

        var opts = new GitSyncOptions("loom: sync rules", "Loom Bot", "bot@loom.local", Push: false);

        var first = await svc.SyncAsync(null, _workingCopy, opts);
        var second = await svc.SyncAsync(null, _workingCopy, opts);

        first.Changed.Should().BeTrue();
        second.Changed.Should().BeFalse();
        second.CommitSha.Should().BeNull();
        second.WrittenFiles.Should().BeEmpty();
    }

    private static (LibGit2GitSyncService svc, FakeFragmentRepository fragments, FakeProjectRepository projects) MakeService()
    {
        var fragments = new FakeFragmentRepository();
        var projects = new FakeProjectRepository();
        var clock = new FakeSystemClock(Now);
        return (new LibGit2GitSyncService(fragments, projects, clock), fragments, projects);
    }

    private static void SeedFragment(FakeFragmentRepository repo, FragmentCategory category, string key, string content)
    {
        var owner = Guid.NewGuid();
        var f = Fragment.Create(Slug.From(key), category, FragmentScope.Global, scopeId: null, $"{key} title", owner, Now);
        f.PublishVersion(content, new EngineHints(), changeNote: null, authorId: owner, now: Now);
        repo.ById[f.Id] = f;
    }

    private static void SafeDelete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }
        // Git objects are read-only; clear the bit before delete on Windows.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(path, recursive: true);
    }
}
