using Loom.Domain.BudgetControl;

namespace Loom.Application.BudgetControl;

/// <summary>
/// Per-project budget gate. The application's QueueAsync flow calls
/// <see cref="TryReserveAsync"/> before queuing a Run; if the project's
/// circuit breaker is open the call is rejected and the run is never
/// persisted. <see cref="RecordSpendAsync"/> is called from RunService on
/// completion (and on failure with partial cost) so today's tally tracks
/// reality.
/// </summary>
public interface IBudgetService
{
    /// <summary>
    /// Returns true if the project has budget for an additional <paramref
    /// name="estimatedUsd"/> spend today. False means the circuit breaker
    /// is open — the caller must NOT queue the run. If the project has no
    /// budget row, one is created lazily with no cap (always-true).
    /// </summary>
    Task<bool> TryReserveAsync(Guid projectId, decimal estimatedUsd, CancellationToken ct = default);

    Task RecordSpendAsync(Guid projectId, decimal usd, CancellationToken ct = default);

    Task<ProjectBudget> EnsureAsync(Guid projectId, CancellationToken ct = default);

    Task SetDailyCapAsync(Guid projectId, decimal? dailyCapUsd, CancellationToken ct = default);
}
