using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Abstractions;

public interface IRunRepository
{
    Task<Run?> GetAsync(RunId id, CancellationToken ct = default);
    Task<IReadOnlyList<Run>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default);
    Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(Run run, CancellationToken ct = default);
}
