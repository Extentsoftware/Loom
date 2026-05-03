using Loom.Domain.Common;
using Loom.Domain.Workflows;

namespace Loom.Application.Abstractions;

public interface IWorkflowRepository
{
    Task<Workflow?> GetAsync(WorkflowId id, CancellationToken ct = default);
    Task<Workflow?> GetByKeyAsync(Slug key, int version, CancellationToken ct = default);
    Task<Workflow?> GetCurrentByKeyAsync(Slug key, CancellationToken ct = default);
    Task<IReadOnlyList<Workflow>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Workflow workflow, CancellationToken ct = default);
}
