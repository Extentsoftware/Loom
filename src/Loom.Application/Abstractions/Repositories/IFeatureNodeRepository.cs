using Loom.Domain.Nodes;

namespace Loom.Application.Abstractions;

public interface IFeatureNodeRepository
{
    Task<FeatureNode?> GetAsync(NodeId id, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureNode>> GetChildrenAsync(NodeId parentId, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureNode>> GetRootsAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureNode>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Phase-2 keyword search across title and intent. Phase 6 supersedes
    /// this with the Elasticsearch-backed memory service; until then, a
    /// case-insensitive LIKE keeps the kickoff demo + MCP search_nodes
    /// honest without an extra dependency.
    /// </summary>
    Task<IReadOnlyList<FeatureNode>> SearchAsync(string query, Guid? projectId, int take, CancellationToken ct = default);

    Task AddAsync(FeatureNode node, CancellationToken ct = default);
    void Remove(FeatureNode node);
}
