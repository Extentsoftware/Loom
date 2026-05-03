using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Application.Features;

public interface IFeatureService
{
    Task<Project> CreateProjectAsync(Slug slug, string name, string? description, DateTimeOffset? now = null, CancellationToken ct = default);

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

    Task RenameAsync(NodeId nodeId, string title, CancellationToken ct = default);
    Task SetIntentAsync(NodeId nodeId, string? intent, CancellationToken ct = default);
    Task ApplyDiscoveryAsync(NodeId nodeId, DiscoveryAcceptance acceptance, Guid acceptedBy, CancellationToken ct = default);
    Task AdvancePhaseAsync(NodeId nodeId, NodePhase target, CancellationToken ct = default);

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
