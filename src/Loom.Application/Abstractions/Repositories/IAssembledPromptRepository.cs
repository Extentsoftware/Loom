using Loom.Domain.Runs;

namespace Loom.Application.Abstractions;

public interface IAssembledPromptRepository
{
    Task<AssembledPrompt?> GetAsync(AssembledPromptId id, CancellationToken ct = default);
    Task<AssembledPrompt?> GetByRunAsync(RunId runId, CancellationToken ct = default);
    Task AddAsync(AssembledPrompt prompt, CancellationToken ct = default);
}
