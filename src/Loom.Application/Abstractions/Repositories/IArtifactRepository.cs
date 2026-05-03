using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;

namespace Loom.Application.Abstractions;

public interface IArtifactRepository
{
    Task<Artifact?> GetAsync(ArtifactId id, CancellationToken ct = default);
    Task<IReadOnlyList<Artifact>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default);
    Task AddAsync(Artifact artifact, CancellationToken ct = default);
}
