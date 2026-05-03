using Loom.Application.Abstractions;
using Loom.Domain.BudgetControl;

namespace Loom.Application.BudgetControl;

public sealed class BudgetService(
    IProjectBudgetRepository budgets,
    IUnitOfWork uow,
    ISystemClock clock) : IBudgetService
{
    public async Task<bool> TryReserveAsync(Guid projectId, decimal estimatedUsd, CancellationToken ct = default)
    {
        var budget = await EnsureAsync(projectId, ct);
        return budget.TryReserve(estimatedUsd, clock.UtcNow);
    }

    public async Task RecordSpendAsync(Guid projectId, decimal usd, CancellationToken ct = default)
    {
        if (usd <= 0m)
        {
            return;
        }
        var budget = await EnsureAsync(projectId, ct);
        budget.RecordSpend(usd, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<ProjectBudget> EnsureAsync(Guid projectId, CancellationToken ct = default)
    {
        var existing = await budgets.GetByProjectAsync(projectId, ct);
        if (existing is not null)
        {
            return existing;
        }
        var fresh = ProjectBudget.Create(projectId, dailyCapUsd: null, clock.UtcNow);
        await budgets.AddAsync(fresh, ct);
        await uow.SaveChangesAsync(ct);
        return fresh;
    }

    public async Task SetDailyCapAsync(Guid projectId, decimal? dailyCapUsd, CancellationToken ct = default)
    {
        var budget = await EnsureAsync(projectId, ct);
        budget.SetDailyCap(dailyCapUsd, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }
}
