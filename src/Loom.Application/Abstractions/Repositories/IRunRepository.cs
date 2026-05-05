using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Abstractions;

public interface IRunRepository
{
    Task<Run?> GetAsync(RunId id, CancellationToken ct = default);
    Task<IReadOnlyList<Run>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default);
    Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs that are paused for a human gate AND either unassigned or
    /// assigned to <paramref name="userId"/> — i.e. "what's queued for
    /// me?" plus "what could I claim?". Backs the MCP claim path.
    /// </summary>
    Task<IReadOnlyList<Run>> ListClaimableForAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Run run, CancellationToken ct = default);
}
