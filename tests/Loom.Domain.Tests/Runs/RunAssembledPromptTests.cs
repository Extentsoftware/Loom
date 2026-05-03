using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Xunit;

namespace Loom.Domain.Tests.Runs;

public sealed class RunAssembledPromptTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    private static Run Queued() => Run.Queue(
        NodeId.New(),
        workflowId: Guid.NewGuid(),
        stepId: Guid.NewGuid(),
        engine: EngineName.Anthropic,
        budgets: new Budgets(MaxInputTokens: 8000, MaxOutputTokens: 2000, MaxWallClock: TimeSpan.FromMinutes(2), MaxCostUsd: 0.50m),
        fragments: [new FragmentRef(FragmentId.New(), FragmentVersionId.New(), 1)],
        now: Now);

    [Fact]
    public void AttachAssembledPrompt_RecordsId_WhenQueued()
    {
        var run = Queued();
        var promptId = AssembledPromptId.New();

        run.AttachAssembledPrompt(promptId);

        run.AssembledPromptId.Should().Be(promptId);
    }

    [Fact]
    public void AttachAssembledPrompt_RejectsRunningRun()
    {
        var run = Queued();
        run.MarkRunning("ext-1", Now);

        var act = () => run.AttachAssembledPrompt(AssembledPromptId.New());

        act.Should().Throw<DomainException>().WithMessage("*Running*");
    }

    [Fact]
    public void AttachAssembledPrompt_RejectsSecondAttach()
    {
        var run = Queued();
        run.AttachAssembledPrompt(AssembledPromptId.New());

        var act = () => run.AttachAssembledPrompt(AssembledPromptId.New());

        act.Should().Throw<DomainException>().WithMessage("*already*");
    }
}
