using Loom.Domain.Runs;

namespace Loom.Application.Abstractions;

public interface IRunEventRepository
{
    Task<IReadOnlyList<RunEvent>> GetByRunAsync(RunId runId, CancellationToken ct = default);
    Task<int> GetNextSequenceAsync(RunId runId, CancellationToken ct = default);
    Task AddAsync(RunEvent runEvent, CancellationToken ct = default);
}
