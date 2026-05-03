using FluentAssertions;
using Loom.Domain.BudgetControl;
using Loom.Domain.Common;
using Xunit;

namespace Loom.Domain.Tests.BudgetControl;

public sealed class ProjectBudgetTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 5, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = Day1.AddDays(1);

    [Fact]
    public void TryReserve_returns_true_when_no_cap()
    {
        var b = ProjectBudget.Create(Guid.CreateVersion7(), dailyCapUsd: null, Day1);
        b.TryReserve(100m, Day1).Should().BeTrue();
    }

    [Fact]
    public void TryReserve_blocks_when_estimate_exceeds_remaining()
    {
        var b = ProjectBudget.Create(Guid.CreateVersion7(), dailyCapUsd: 5m, Day1);
        b.RecordSpend(4m, Day1);
        b.TryReserve(2m, Day1).Should().BeFalse();
        b.TryReserve(1m, Day1).Should().BeTrue();
    }

    [Fact]
    public void Spend_rolls_over_at_utc_day_change()
    {
        var b = ProjectBudget.Create(Guid.CreateVersion7(), dailyCapUsd: 5m, Day1);
        b.RecordSpend(4m, Day1);
        b.TodaysSpendUsd.Should().Be(4m);

        // Touching it on the next UTC day resets the counter.
        b.TryReserve(0m, Day2).Should().BeTrue();
        b.TodaysSpendUsd.Should().Be(0m);
    }

    [Fact]
    public void CircuitBreakerOpen_reflects_today_only()
    {
        var b = ProjectBudget.Create(Guid.CreateVersion7(), dailyCapUsd: 5m, Day1);
        b.RecordSpend(5m, Day1);
        b.CircuitBreakerOpen(Day1).Should().BeTrue();
        b.CircuitBreakerOpen(Day2).Should().BeFalse();
    }

    [Fact]
    public void Negative_cap_throws()
    {
        var act = () => ProjectBudget.Create(Guid.CreateVersion7(), -1m, Day1);
        act.Should().Throw<DomainException>();
    }
}
