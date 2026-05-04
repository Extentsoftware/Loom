using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;

namespace Loom.Application.Features;

public sealed class FeatureService(
    IProjectRepository projects,
    IFeatureNodeRepository nodes,
    IRunRepository runs,
    IDomainEventCollector events,
    IUnitOfWork uow,
    ISystemClock clock) : IFeatureService
{
    public async Task<Project> CreateProjectAsync(Slug slug, string name, string? description, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        var existing = await projects.GetBySlugAsync(slug.Value, ct);
        if (existing is not null)
        {
            throw new DomainException($"Project with slug '{slug.Value}' already exists.");
        }
        var t = now ?? clock.UtcNow;
        var p = Project.Create(slug, name, t);
        if (!string.IsNullOrWhiteSpace(description))
        {
            p.UpdateDescription(description, t);
        }
        await projects.AddAsync(p, ct);
        await uow.SaveChangesAsync(ct);
        return p;
    }

    public async Task RenameProjectAsync(Guid projectId, string newName, CancellationToken ct = default)
    {
        var project = await projects.GetAsync(projectId, ct)
            ?? throw new DomainException($"Project {projectId} not found.");
        project.Rename(newName, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projects.GetAsync(projectId, ct)
            ?? throw new DomainException($"Project {projectId} not found.");
        project.Archive(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projects.GetAsync(projectId, ct)
            ?? throw new DomainException($"Project {projectId} not found.");
        project.Unarchive(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<FeatureNode> CreateRootNodeAsync(
        Guid projectId,
        Slug slug,
        NodeType type,
        string title,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var project = await projects.GetAsync(projectId, ct)
            ?? throw new DomainException($"Project {projectId} not found.");
        var node = FeatureNode.Create(project.Id, parentId: null, slug, type, title, ownerId, clock.UtcNow);
        await nodes.AddAsync(node, ct);
        events.Add(new NodeCreated(node.Id, project.Id, ParentId: null, type, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
        return node;
    }

    public async Task<FeatureNode> CreateChildNodeAsync(
        NodeId parentId,
        Slug slug,
        NodeType type,
        string title,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var parent = await nodes.GetAsync(parentId, ct)
            ?? throw new DomainException($"Parent node {parentId} not found.");
        var node = FeatureNode.Create(parent.ProjectId, parentId, slug, type, title, ownerId, clock.UtcNow);
        await nodes.AddAsync(node, ct);
        events.Add(new NodeCreated(node.Id, parent.ProjectId, parentId, type, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
        return node;
    }

    public async Task RenameAsync(NodeId nodeId, string title, Guid actorId = default, CancellationToken ct = default)
    {
        var node = await GetOrThrow(nodeId, ct);
        node.Rename(title, clock.UtcNow);
        events.Add(new NodeUpdated(node.Id, node.ProjectId, clock.UtcNow, NodeEditKind.Title, actorId));
        await uow.SaveChangesAsync(ct);
    }

    public async Task SetIntentAsync(NodeId nodeId, string? intent, Guid actorId = default, CancellationToken ct = default)
    {
        var node = await GetOrThrow(nodeId, ct);
        node.SetIntent(intent, clock.UtcNow);
        events.Add(new NodeUpdated(node.Id, node.ProjectId, clock.UtcNow, NodeEditKind.Intent, actorId));
        await uow.SaveChangesAsync(ct);
    }

    public async Task ReplaceHypothesesAsync(NodeId nodeId, IReadOnlyList<Hypothesis> hypotheses, Guid actorId = default, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(hypotheses);
        var node = await GetOrThrow(nodeId, ct);
        node.ReplaceHypotheses(hypotheses, clock.UtcNow);
        events.Add(new NodeUpdated(node.Id, node.ProjectId, clock.UtcNow, NodeEditKind.Hypotheses, actorId));
        await uow.SaveChangesAsync(ct);
    }

    public async Task ReplaceOutcomesAsync(NodeId nodeId, IReadOnlyList<Outcome> outcomes, Guid actorId = default, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var node = await GetOrThrow(nodeId, ct);
        node.ReplaceOutcomes(outcomes, clock.UtcNow);
        events.Add(new NodeUpdated(node.Id, node.ProjectId, clock.UtcNow, NodeEditKind.Outcomes, actorId));
        await uow.SaveChangesAsync(ct);
    }

    public async Task ReplaceConstraintsAsync(NodeId nodeId, IReadOnlyList<Constraint> constraints, Guid actorId = default, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        var node = await GetOrThrow(nodeId, ct);
        node.ReplaceConstraints(constraints, clock.UtcNow);
        events.Add(new NodeUpdated(node.Id, node.ProjectId, clock.UtcNow, NodeEditKind.Constraints, actorId));
        await uow.SaveChangesAsync(ct);
    }

    public async Task ReplaceOpenQuestionsAsync(NodeId nodeId, IReadOnlyList<string> questions, Guid actorId = default, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var node = await GetOrThrow(nodeId, ct);
        node.ReplaceOpenQuestions(questions, clock.UtcNow);
        events.Add(new NodeUpdated(node.Id, node.ProjectId, clock.UtcNow, NodeEditKind.OpenQuestions, actorId));
        await uow.SaveChangesAsync(ct);
    }

    public async Task ReplaceStakeholdersAsync(NodeId nodeId, IReadOnlyList<Stakeholder> stakeholders, Guid actorId = default, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stakeholders);
        var node = await GetOrThrow(nodeId, ct);
        node.ReplaceStakeholders(stakeholders, clock.UtcNow);
        events.Add(new NodeUpdated(node.Id, node.ProjectId, clock.UtcNow, NodeEditKind.Stakeholders, actorId));
        await uow.SaveChangesAsync(ct);
    }

    public async Task ApplyDiscoveryAsync(NodeId nodeId, DiscoveryAcceptance acceptance, Guid acceptedBy, CancellationToken ct = default)
    {
        var node = await GetOrThrow(nodeId, ct);
        node.ApplyDiscovery(acceptance, clock.UtcNow);
        events.Add(new DiscoveryAccepted(node.Id, node.ProjectId, acceptedBy, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
    }

    public async Task AdvancePhaseAsync(NodeId nodeId, NodePhase target, CancellationToken ct = default)
    {
        var node = await GetOrThrow(nodeId, ct);
        var from = node.Phase;
        node.AdvancePhase(target, clock.UtcNow);
        events.Add(new NodePhaseAdvanced(node.Id, from, target, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
    }

    public async Task<NodeTreeView> GetTreeAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projects.GetAsync(projectId, ct)
            ?? throw new DomainException($"Project {projectId} not found.");

        var allNodes = await nodes.GetByProjectAsync(projectId, ct);
        var nodesById = allNodes.ToDictionary(n => n.Id);
        var childrenByParent = allNodes
            .Where(n => n.ParentId.HasValue)
            .GroupBy(n => n.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.CreatedAt).ToList());

        var now = clock.UtcNow;

        // Build status signals once per node using the runs cache.
        var allRuns = new List<Domain.Runs.Run>();
        foreach (var n in allNodes)
        {
            allRuns.AddRange(await runs.GetByNodeAsync(n.Id, ct));
        }
        var runsByNode = allRuns
            .GroupBy(r => r.NodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        IReadOnlyList<NodeTreeRow> Build(IReadOnlyList<FeatureNode> set) =>
            set.Select(n =>
            {
                IReadOnlyList<FeatureNode> directChildren = childrenByParent.TryGetValue(n.Id, out var c) ? c : [];
                var renderedChildren = Build(directChildren);
                runsByNode.TryGetValue(n.Id, out var nodeRuns);
                var status = StatusSignalsCalculator.Compute(n, directChildren, nodeRuns ?? [], now);
                return new NodeTreeRow(n.Id, n.ParentId, n.Slug.Value, n.Title, n.Type, n.Phase, status, renderedChildren);
            }).ToList();

        var roots = allNodes.Where(n => !n.ParentId.HasValue).OrderBy(n => n.CreatedAt).ToList();
        return new NodeTreeView(project.Id, project.Name, Build(roots));
    }

    public async Task<IReadOnlyList<NodeSearchHit>> SearchAsync(string query, Guid? projectId, int take = 25, CancellationToken ct = default)
    {
        var hits = await nodes.SearchAsync(query, projectId, take, ct);
        return [.. hits.Select(n => new NodeSearchHit(
            n.Id, n.ProjectId, n.Slug.Value, n.Title, n.Intent, n.Type, n.Phase, n.UpdatedAt))];
    }

    public async Task<FeatureWorkspaceView?> GetWorkspaceAsync(NodeId nodeId, CancellationToken ct = default)
    {
        var node = await nodes.GetAsync(nodeId, ct);
        if (node is null)
        {
            return null;
        }
        var children = await nodes.GetChildrenAsync(nodeId, ct);
        var nodeRuns = await runs.GetByNodeAsync(nodeId, ct);
        var now = clock.UtcNow;
        var status = StatusSignalsCalculator.Compute(node, children, nodeRuns, now);

        var childRows = children
            .Select(c => new NodeTreeRow(
                c.Id, c.ParentId, c.Slug.Value, c.Title, c.Type, c.Phase,
                StatusSignalsCalculator.Compute(c, [], [], now),
                []))
            .ToList();

        var summaries = nodeRuns
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .Select(r => new RunSummary(r.Id, r.Engine, r.State, r.CreatedAt, r.CompletedAt, r.Cost?.UsdAmount))
            .ToList();

        return new FeatureWorkspaceView(
            NodeId: node.Id,
            ProjectId: node.ProjectId,
            ParentId: node.ParentId,
            Slug: node.Slug.Value,
            Title: node.Title,
            Intent: node.Intent,
            Type: node.Type,
            Phase: node.Phase,
            Outcomes: node.Outcomes,
            Hypotheses: node.Hypotheses,
            Constraints: node.Constraints,
            OpenQuestions: node.OpenQuestions,
            Stakeholders: node.Stakeholders,
            Children: childRows,
            RecentRuns: summaries,
            Status: status);
    }

    private async Task<FeatureNode> GetOrThrow(NodeId id, CancellationToken ct)
    {
        return await nodes.GetAsync(id, ct)
            ?? throw new DomainException($"Node {id} not found.");
    }
}
