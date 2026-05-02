using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Xunit;

namespace Loom.Domain.Tests.Runs;

public sealed class RunTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly NodeId ANode = NodeId.New();

    private static Run Queued() => Run.Queue(
        ANode,
        workflowId: Guid.NewGuid(),
        stepId: Guid.NewGuid(),
        engine: EngineName.Foundry,
        budgets: new Budgets(MaxInputTokens: 8000, MaxOutputTokens: 2000, MaxWallClock: TimeSpan.FromMinutes(2), MaxCostUsd: 0.50m),
        fragments: [new FragmentRef(FragmentId.New(), FragmentVersionId.New(), 1)],
        now: Now);

    [Fact]
    public void Queue_StartsInQueuedState()
    {
        var r = Queued();
        r.State.Should().Be(RunState.Queued);
        r.StartedAt.Should().BeNull();
        r.Fragments.Should().HaveCount(1);
        r.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void MarkRunning_RecordsStartAndExternalId()
    {
        var r = Queued();
        var t = Now.AddSeconds(2);

        r.MarkRunning("foundry-thread-abc", t);

        r.State.Should().Be(RunState.Running);
        r.StartedAt.Should().Be(t);
        r.ExternalRunId.Should().Be("foundry-thread-abc");
    }

    [Fact]
    public void Complete_RequiresRunning()
    {
        var r = Queued();
        var act = () => r.Complete(NewCost(), Now.AddSeconds(5));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_FromRunning_Terminal()
    {
        var r = Queued();
        r.MarkRunning("x", Now.AddSeconds(1));
        var done = Now.AddSeconds(10);

        r.Complete(NewCost(), done);

        r.State.Should().Be(RunState.Completed);
        r.CompletedAt.Should().Be(done);
        r.IsTerminal.Should().BeTrue();
        r.Cost.Should().NotBeNull();
    }

    [Fact]
    public void PauseAndResume_RoundTrips()
    {
        var r = Queued();
        r.MarkRunning("x", Now);
        r.PauseForHuman(Now.AddMinutes(1));
        r.State.Should().Be(RunState.PausedForHuman);

        r.Resume(Now.AddMinutes(2));
        r.State.Should().Be(RunState.Running);
    }

    [Fact]
    public void Cancel_FromAnyNonTerminalState_OK()
    {
        foreach (var setup in new Action<Run>[]
        {
            _ => { },                                                       // Queued
            r => r.MarkRunning("x", Now),                                   // Running
            r => { r.MarkRunning("x", Now); r.PauseForHuman(Now.AddSeconds(1)); } // Paused
        })
        {
            var r = Queued();
            setup(r);
            r.Cancel("user requested", Now.AddMinutes(5));
            r.State.Should().Be(RunState.Cancelled);
            r.IsTerminal.Should().BeTrue();
        }
    }

    [Fact]
    public void TerminalState_RejectsFurtherTransitions()
    {
        var r = Queued();
        r.MarkRunning("x", Now);
        r.Complete(NewCost(), Now.AddSeconds(2));

        var act1 = () => r.Cancel("nope", Now.AddSeconds(3));
        var act2 = () => r.Fail("nope", null, Now.AddSeconds(3));

        act1.Should().Throw<DomainException>();
        act2.Should().Throw<DomainException>();
    }

    [Fact]
    public void Fail_FromRunning_RecordsReason()
    {
        var r = Queued();
        r.MarkRunning("x", Now);

        r.Fail("budget exceeded", NewCost(), Now.AddSeconds(5));

        r.State.Should().Be(RunState.Failed);
        r.FailureReason.Should().Be("budget exceeded");
    }

    private static Cost NewCost() => new(
        Model: "gpt-4o-mini",
        Deployment: "loom-eastus-prod",
        InputTokens: 1234,
        OutputTokens: 567,
        UsdAmount: 0.04m);
}
