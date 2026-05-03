using Loom.Domain.Common;

namespace Loom.Domain.BudgetControl;

public readonly record struct ProjectBudgetId(Guid Value) : IEntityId
{
    public static ProjectBudgetId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Per-project rolling budget with a daily $-cap circuit breaker. The
/// design's most important Phase-5 risk control: without these, an outage
/// in the workflow engine that loops a run indefinitely turns into a bill.
///
/// Semantics:
///   • Each project has at most one ProjectBudget (1:1 with Project.Id).
///   • <c>DailyCapUsd</c> is the hard ceiling for combined run cost in a
///     UTC day. Setting it to null disables the circuit breaker.
///   • <c>TodaysSpendUsd</c> rolls over to 0 when the next UTC day starts.
///     The roll-over is lazy: it happens the first time the aggregate is
///     touched on a new day, in <see cref="EnsureCurrentDay"/>.
///   • <see cref="TryReserve"/> is the gate: callers ask "can I spend up
///     to <c>estimated</c> dollars?". If today's spend + estimate exceeds
///     the cap, the call returns false and the run must not be queued.
///   • <see cref="RecordSpend"/> is called when a run completes; it adds
///     the actual cost to today's tally.
/// </summary>
public sealed class ProjectBudget
{
    private ProjectBudget() { } // EF Core

    private ProjectBudget(
        ProjectBudgetId id,
        Guid projectId,
        decimal? dailyCapUsd,
        DateOnly dayAnchor,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        DailyCapUsd = dailyCapUsd;
        DayAnchor = dayAnchor;
        TodaysSpendUsd = 0m;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public ProjectBudgetId Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public decimal? DailyCapUsd { get; private set; }
    public decimal TodaysSpendUsd { get; private set; }
    public DateOnly DayAnchor { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool CircuitBreakerOpen(DateTimeOffset now)
    {
        EnsureCurrentDay(now);
        return DailyCapUsd is decimal cap && TodaysSpendUsd >= cap;
    }

    public static ProjectBudget Create(Guid projectId, decimal? dailyCapUsd, DateTimeOffset now)
    {
        if (projectId == Guid.Empty)
        {
            throw new DomainException("ProjectId is required.");
        }
        if (dailyCapUsd is decimal cap && cap < 0m)
        {
            throw new DomainException("Daily cap cannot be negative.");
        }
        return new ProjectBudget(
            ProjectBudgetId.New(),
            projectId,
            dailyCapUsd,
            DateOnly.FromDateTime(now.UtcDateTime),
            now);
    }

    public void SetDailyCap(decimal? dailyCapUsd, DateTimeOffset now)
    {
        if (dailyCapUsd is decimal cap && cap < 0m)
        {
            throw new DomainException("Daily cap cannot be negative.");
        }
        DailyCapUsd = dailyCapUsd;
        UpdatedAt = now;
    }

    /// <summary>
    /// Try to reserve <paramref name="estimatedUsd"/> against today's
    /// remaining budget. Returns true if the call is allowed. The reservation
    /// is *not* persisted — callers are expected to record actual spend via
    /// <see cref="RecordSpend"/> on completion, and the watchdog cancels
    /// runs that exceed their per-run budget.
    /// </summary>
    public bool TryReserve(decimal estimatedUsd, DateTimeOffset now)
    {
        if (estimatedUsd < 0m)
        {
            throw new DomainException("Estimated cost cannot be negative.");
        }
        EnsureCurrentDay(now);
        if (DailyCapUsd is not decimal cap)
        {
            return true;
        }
        return TodaysSpendUsd + estimatedUsd <= cap;
    }

    public void RecordSpend(decimal usd, DateTimeOffset now)
    {
        if (usd < 0m)
        {
            throw new DomainException("Spend cannot be negative.");
        }
        EnsureCurrentDay(now);
        TodaysSpendUsd += usd;
        UpdatedAt = now;
    }

    private void EnsureCurrentDay(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (today > DayAnchor)
        {
            DayAnchor = today;
            TodaysSpendUsd = 0m;
            UpdatedAt = now;
        }
    }
}
