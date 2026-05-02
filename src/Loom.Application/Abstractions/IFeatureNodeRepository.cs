using Loom.Domain.Nodes;

namespace Loom.Application.Abstractions;

public interface IFeatureNodeRepository
{
    Task<FeatureNode?> GetAsync(NodeId id, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureNode>> GetChildrenAsync(NodeId parentId, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureNode>> GetRootsAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(FeatureNode node, CancellationToken ct = default);
    void Remove(FeatureNode node);
}
