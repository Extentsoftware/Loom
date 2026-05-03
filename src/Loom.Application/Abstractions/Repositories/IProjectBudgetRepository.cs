using Loom.Domain.BudgetControl;

namespace Loom.Application.Abstractions;

public interface IProjectBudgetRepository
{
    Task<ProjectBudget?> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectBudget>> ListAsync(CancellationToken ct = default);
    Task AddAsync(ProjectBudget budget, CancellationToken ct = default);
}
