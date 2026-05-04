using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Application.Features;

public interface IFeatureService
{
    Task<Project> CreateProjectAsync(Slug slug, string name, string? description, DateTimeOffset? now = null, CancellationToken ct = default);

    /// <summary>
    /// Rename an existing project. Throws DomainException if the project
    /// is missing or the name is empty.
    /// </summary>
    Task RenameProjectAsync(Guid projectId, string newName, CancellationToken ct = default);

    /// <summary>
    /// Archive a project. Idempotent at the UI level (the "Archive"
    /// button is hidden for already-archived projects); the domain
    /// throws if called on an already-archived project.
    /// </summary>
    Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Restore a previously-archived project.
    /// </summary>
    Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default);

    Task<FeatureNode> CreateRootNodeAsync(
        Guid projectId,
        Slug slug,
        NodeType type,
        string title,
        Guid ownerId,
        CancellationToken ct = default);

    Task<FeatureNode> CreateChildNodeAsync(
        NodeId parentId,
        Slug slug,
        NodeType type,
        string title,
        Guid ownerId,
        CancellationToken ct = default);

    /// <summary>
    /// Rename the node's title. <paramref name="actorId"/> is recorded on
    /// the emitted NodeUpdated event for audit. Pass Guid.Empty when the
    /// caller has no user context (e.g. system / agent edits).
    /// </summary>
    Task RenameAsync(NodeId nodeId, string title, Guid actorId = default, CancellationToken ct = default);
    Task SetIntentAsync(NodeId nodeId, string? intent, Guid actorId = default, CancellationToken ct = default);
    Task ApplyDiscoveryAsync(NodeId nodeId, DiscoveryAcceptance acceptance, Guid acceptedBy, CancellationToken ct = default);
    Task AdvancePhaseAsync(NodeId nodeId, NodePhase target, CancellationToken ct = default);

    /// <summary>
    /// Replace the node's hypothesis set. Used by the Feature Workspace
    /// hypothesis editor. Each Hypothesis is shaped (If, Then, Because).
    /// </summary>
    Task ReplaceHypothesesAsync(NodeId nodeId, IReadOnlyList<Hypothesis> hypotheses, Guid actorId = default, CancellationToken ct = default);

    /// <summary>Replace the node's outcome set.</summary>
    Task ReplaceOutcomesAsync(NodeId nodeId, IReadOnlyList<Outcome> outcomes, Guid actorId = default, CancellationToken ct = default);

    /// <summary>Replace the node's constraint set.</summary>
    Task ReplaceConstraintsAsync(NodeId nodeId, IReadOnlyList<Constraint> constraints, Guid actorId = default, CancellationToken ct = default);

    /// <summary>Replace the node's open-questions list.</summary>
    Task ReplaceOpenQuestionsAsync(NodeId nodeId, IReadOnlyList<string> questions, Guid actorId = default, CancellationToken ct = default);

    /// <summary>Replace the node's stakeholder list.</summary>
    Task ReplaceStakeholdersAsync(NodeId nodeId, IReadOnlyList<Stakeholder> stakeholders, Guid actorId = default, CancellationToken ct = default);

    Task<NodeTreeView> GetTreeAsync(Guid projectId, CancellationToken ct = default);
    Task<FeatureWorkspaceView?> GetWorkspaceAsync(NodeId nodeId, CancellationToken ct = default);

    /// <summary>
    /// Keyword search across node title + intent, optionally scoped to a
    /// project. Returns lightweight summaries suitable for the MCP
    /// search_nodes tool and the global search box.
    /// </summary>
    Task<IReadOnlyList<NodeSearchHit>> SearchAsync(string query, Guid? projectId, int take = 25, CancellationToken ct = default);
}

public sealed record NodeSearchHit(
    NodeId NodeId,
    Guid ProjectId,
    string Slug,
    string Title,
    string? Intent,
    NodeType Type,
    NodePhase Phase,
    DateTimeOffset UpdatedAt);
