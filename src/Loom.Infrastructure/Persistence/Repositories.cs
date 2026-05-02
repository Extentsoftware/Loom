using Loom.Application.Abstractions;
using Loom.Domain.Artifacts;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;

namespace Loom.Infrastructure.Persistence;

public sealed class FeatureNodeRepository(LoomDbContext db) : IFeatureNodeRepository
{
    public Task<FeatureNode?> GetAsync(NodeId id, CancellationToken ct = default) =>
        db.Nodes.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<FeatureNode>> GetChildrenAsync(NodeId parentId, CancellationToken ct = default) =>
        await db.Nodes.Where(n => n.ParentId == parentId).OrderBy(n => n.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<FeatureNode>> GetRootsAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Nodes
            .Where(n => n.ProjectId == projectId && n.ParentId == null)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

    public Task AddAsync(FeatureNode node, CancellationToken ct = default)
    {
        db.Nodes.Add(node);
        return Task.CompletedTask;
    }

    public void Remove(FeatureNode node) => db.Nodes.Remove(node);
}

public sealed class ProjectRepository(LoomDbContext db) : IProjectRepository
{
    public Task<Project?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var s = Domain.Common.Slug.From(slug);
        return db.Projects.FirstOrDefaultAsync(p => p.Slug == s, ct);
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        await db.Projects.OrderBy(p => p.Name).ToListAsync(ct);

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        db.Projects.Add(project);
        return Task.CompletedTask;
    }
}

public sealed class FragmentRepository(LoomDbContext db) : IFragmentRepository
{
    public Task<Fragment?> GetAsync(FragmentId id, CancellationToken ct = default) =>
        db.Fragments.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<Fragment?> GetByKeyAsync(string key, FragmentScope scope, Guid? scopeId, CancellationToken ct = default)
    {
        var k = Domain.Common.Slug.From(key);
        return db.Fragments.FirstOrDefaultAsync(
            f => f.Key == k && f.Scope == scope && f.ScopeId == scopeId, ct);
    }

    public async Task<IReadOnlyList<Fragment>> ListByCategoryAsync(FragmentCategory category, CancellationToken ct = default) =>
        await db.Fragments.Where(f => f.Category == category).OrderBy(f => f.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<Fragment>> ListGlobalAsync(CancellationToken ct = default) =>
        await db.Fragments.Where(f => f.Scope == FragmentScope.Global).OrderBy(f => f.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<Fragment>> ListByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Fragments
            .Where(f => f.Scope == FragmentScope.Project && f.ScopeId == projectId)
            .OrderBy(f => f.Title)
            .ToListAsync(ct);

    public Task AddAsync(Fragment fragment, CancellationToken ct = default)
    {
        db.Fragments.Add(fragment);
        return Task.CompletedTask;
    }
}

public sealed class RunRepository(LoomDbContext db) : IRunRepository
{
    public Task<Run?> GetAsync(RunId id, CancellationToken ct = default) =>
        db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Run>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        await db.Runs.Where(r => r.NodeId == nodeId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Runs
            .Where(r => r.State == RunState.Queued || r.State == RunState.Running || r.State == RunState.PausedForHuman)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task AddAsync(Run run, CancellationToken ct = default)
    {
        db.Runs.Add(run);
        return Task.CompletedTask;
    }
}

public sealed class ArtifactRepository(LoomDbContext db) : IArtifactRepository
{
    public Task<Artifact?> GetAsync(ArtifactId id, CancellationToken ct = default) =>
        db.Artifacts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Artifact>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        await db.Artifacts.Where(a => a.NodeId == nodeId).OrderByDescending(a => a.UpdatedAt).ToListAsync(ct);

    public Task AddAsync(Artifact artifact, CancellationToken ct = default)
    {
        db.Artifacts.Add(artifact);
        return Task.CompletedTask;
    }
}
