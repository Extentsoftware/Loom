using Loom.Domain.Runs;

namespace Loom.Application.Abstractions;

public interface ITranscriptRepository
{
    Task<Transcript?> GetAsync(TranscriptId id, CancellationToken ct = default);
    Task<Transcript?> GetByRunAsync(RunId runId, CancellationToken ct = default);
    Task AddAsync(Transcript transcript, CancellationToken ct = default);
}
