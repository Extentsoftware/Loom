using Loom.Application.Abstractions;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Tests.Fakes;

/// <summary>
/// In-memory repository fakes for application-service tests. Sufficient for
/// the operations Phase 1A services actually exercise; absent methods will
/// throw NotImplementedException to flag accidental coupling.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public int TransactionsBegun { get; private set; }
    public int TransactionsCommitted { get; private set; }
    public int TransactionsRolledBack { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.FromResult(0);
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        TransactionsBegun++;
        return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(this));
    }

    private sealed class FakeTransaction(FakeUnitOfWork owner) : IUnitOfWorkTransaction
    {
        private bool _settled;
        public Task CommitAsync(CancellationToken ct = default)
        {
            if (!_settled) { owner.TransactionsCommitted++; _settled = true; }
            return Task.CompletedTask;
        }
        public Task RollbackAsync(CancellationToken ct = default)
        {
            if (!_settled) { owner.TransactionsRolledBack++; _settled = true; }
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class FakeSystemClock(DateTimeOffset? fixedNow = null) : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = fixedNow ?? new DateTimeOffset(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
}

public sealed class FakeDomainEventCollector : IDomainEventCollector
{
    private readonly List<IDomainEvent> _events = [];
    public IReadOnlyList<IDomainEvent> Recorded => _events.AsReadOnly();
    public void Add(IDomainEvent evt) => _events.Add(evt);
    public IReadOnlyList<IDomainEvent> Drain()
    {
        var s = _events.ToArray();
        _events.Clear();
        return s;
    }
}

public sealed class FakeProjectRepository : IProjectRepository
{
    private readonly Dictionary<Guid, Project> _byId = [];
    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _byId[project.Id] = project;
        return Task.CompletedTask;
    }
    public Task<Project?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_byId.TryGetValue(id, out var p) ? p : null);
    public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        Task.FromResult<Project?>(_byId.Values.FirstOrDefault(p => p.Slug.Value == slug));
    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Project>>(_byId.Values.Where(p => !p.IsArchived).ToList());
    public Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Project>>(_byId.Values.ToList());
}

public sealed class FakeFeatureNodeRepository : IFeatureNodeRepository
{
    private readonly Dictionary<NodeId, FeatureNode> _byId = [];
    public Task AddAsync(FeatureNode node, CancellationToken ct = default)
    {
        _byId[node.Id] = node;
        return Task.CompletedTask;
    }
    public void Remove(FeatureNode node) => _byId.Remove(node.Id);
    public Task<FeatureNode?> GetAsync(NodeId id, CancellationToken ct = default) =>
        Task.FromResult(_byId.TryGetValue(id, out var n) ? n : null);
    public Task<IReadOnlyList<FeatureNode>> GetChildrenAsync(NodeId parentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FeatureNode>>(_byId.Values.Where(n => n.ParentId == parentId).OrderBy(n => n.CreatedAt).ToList());
    public Task<IReadOnlyList<FeatureNode>> GetRootsAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FeatureNode>>(_byId.Values.Where(n => n.ProjectId == projectId && n.ParentId is null).OrderBy(n => n.CreatedAt).ToList());
    public Task<IReadOnlyList<FeatureNode>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FeatureNode>>(_byId.Values.Where(n => n.ProjectId == projectId).OrderBy(n => n.CreatedAt).ToList());

    public Task<IReadOnlyList<FeatureNode>> SearchAsync(string query, Guid? projectId, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<FeatureNode>>([]);
        }
        var q = query.Trim();
        var matches = _byId.Values
            .Where(n => projectId is null || n.ProjectId == projectId.Value)
            .Where(n =>
                n.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (n.Intent ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.UpdatedAt)
            .Take(take <= 0 ? 25 : take)
            .ToList();
        return Task.FromResult<IReadOnlyList<FeatureNode>>(matches);
    }
}

public sealed class FakeFragmentRepository : IFragmentRepository
{
    public Dictionary<FragmentId, Fragment> ById { get; } = [];

    public Task AddAsync(Fragment fragment, CancellationToken ct = default)
    {
        ById[fragment.Id] = fragment;
        return Task.CompletedTask;
    }
    public Task<Fragment?> GetAsync(FragmentId id, CancellationToken ct = default) =>
        Task.FromResult(ById.TryGetValue(id, out var f) ? f : null);
    public Task<Fragment?> GetByKeyAsync(string key, FragmentScope scope, Guid? scopeId, CancellationToken ct = default) =>
        Task.FromResult<Fragment?>(ById.Values.FirstOrDefault(f =>
            f.Key.Value == key && f.Scope == scope && f.ScopeId == scopeId));
    public Task<IReadOnlyList<Fragment>> ListByCategoryAsync(FragmentCategory category, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Fragment>>(ById.Values.Where(f => f.Category == category).ToList());
    public Task<IReadOnlyList<Fragment>> ListGlobalAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Fragment>>(ById.Values.Where(f => f.Scope == FragmentScope.Global).ToList());
    public Task<IReadOnlyList<Fragment>> ListByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Fragment>>(ById.Values.Where(f => f.Scope == FragmentScope.Project && f.ScopeId == projectId).ToList());
    public Task<IReadOnlyList<Fragment>> ListByNodeAsync(Guid nodeId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Fragment>>(ById.Values.Where(f => f.Scope == FragmentScope.Node && f.ScopeId == nodeId).ToList());
}

public sealed class FakeRunRepository : IRunRepository
{
    public Dictionary<RunId, Run> ById { get; } = [];

    public Task AddAsync(Run run, CancellationToken ct = default)
    {
        ById[run.Id] = run;
        return Task.CompletedTask;
    }
    public Task<Run?> GetAsync(RunId id, CancellationToken ct = default) =>
        Task.FromResult(ById.TryGetValue(id, out var r) ? r : null);
    public Task<IReadOnlyList<Run>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Run>>(ById.Values.Where(r => r.NodeId == nodeId).OrderBy(r => r.CreatedAt).ToList());
    public Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Run>>(ById.Values.Where(r => !r.IsTerminal).ToList());
    public Task<IReadOnlyList<Run>> ListClaimableForAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Run>>(ById.Values
            .Where(r => r.State == RunState.PausedForHuman
                && (r.AssigneeUserId is null || r.AssigneeUserId == userId))
            .OrderBy(r => r.CreatedAt)
            .ToList());
}

public sealed class FakeRunEventRepository : IRunEventRepository
{
    public List<RunEvent> All { get; } = [];

    public Task AddAsync(RunEvent runEvent, CancellationToken ct = default)
    {
        All.Add(runEvent);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<RunEvent>> GetByRunAsync(RunId runId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RunEvent>>(All.Where(e => e.RunId == runId).OrderBy(e => e.Sequence).ToList());
    public Task<int> GetNextSequenceAsync(RunId runId, CancellationToken ct = default)
    {
        var max = All.Where(e => e.RunId == runId).Select(e => (int?)e.Sequence).DefaultIfEmpty(null).Max();
        return Task.FromResult((max ?? -1) + 1);
    }
}

public sealed class FakeAssembledPromptRepository : IAssembledPromptRepository
{
    public Dictionary<AssembledPromptId, AssembledPrompt> ById { get; } = [];

    public Task AddAsync(AssembledPrompt prompt, CancellationToken ct = default)
    {
        ById[prompt.Id] = prompt;
        return Task.CompletedTask;
    }
    public Task<AssembledPrompt?> GetAsync(AssembledPromptId id, CancellationToken ct = default) =>
        Task.FromResult(ById.TryGetValue(id, out var p) ? p : null);
    public Task<AssembledPrompt?> GetByRunAsync(RunId runId, CancellationToken ct = default) =>
        Task.FromResult<AssembledPrompt?>(ById.Values.FirstOrDefault(p => p.RunId == runId));
}

public sealed class FakeWorkflowRepository : IWorkflowRepository
{
    public Dictionary<WorkflowId, Workflow> ById { get; } = [];
    public Task AddAsync(Workflow workflow, CancellationToken ct = default)
    {
        ById[workflow.Id] = workflow;
        return Task.CompletedTask;
    }
    public Task<Workflow?> GetAsync(WorkflowId id, CancellationToken ct = default) =>
        Task.FromResult(ById.TryGetValue(id, out var w) ? w : null);
    public Task<Workflow?> GetByKeyAsync(Slug key, int version, CancellationToken ct = default) =>
        Task.FromResult<Workflow?>(ById.Values.FirstOrDefault(w => w.Key == key && w.Version == version));
    public Task<Workflow?> GetCurrentByKeyAsync(Slug key, CancellationToken ct = default) =>
        Task.FromResult<Workflow?>(ById.Values.Where(w => w.Key == key).OrderByDescending(w => w.Version).FirstOrDefault());
    public Task<IReadOnlyList<Workflow>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Workflow>>(ById.Values.ToList());
}

public sealed class FakeArtifactRepository : IArtifactRepository
{
    public Task AddAsync(Artifact artifact, CancellationToken ct = default) => Task.CompletedTask;
    public Task<Artifact?> GetAsync(ArtifactId id, CancellationToken ct = default) => Task.FromResult<Artifact?>(null);
    public Task<IReadOnlyList<Artifact>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Artifact>>([]);
}

public sealed class FakeProjectArtifactService : Loom.Application.Artifacts.IProjectArtifactService
{
    public Task<ProjectArtifact> AttachLinkAsync(Guid projectId, NodeId? nodeId, ProjectArtifactKind kind,
        string label, string url, string? description, Guid? createdByUserId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<ProjectArtifact> AttachFileAsync(Guid projectId, NodeId? nodeId, ProjectArtifactKind kind,
        string label, Loom.Application.Artifacts.ArtifactBundle bundle, string? description,
        Guid? createdByUserId, CancellationToken ct = default) => throw new NotImplementedException();

    public Task<IReadOnlyList<ProjectArtifact>> ListForFeatureAsync(Guid projectId, NodeId? nodeId,
        CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProjectArtifact>>([]);

    public Task RemoveAsync(ProjectArtifactId id, CancellationToken ct = default) => Task.CompletedTask;
}
