using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Xunit;

namespace Loom.Domain.Tests.Workflows;

public sealed class WorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    private static readonly Budgets DefaultBudgets =
        new(MaxInputTokens: 8000, MaxOutputTokens: 2000, MaxWallClock: TimeSpan.FromMinutes(2), MaxCostUsd: 0.50m);

    private static WorkflowStepDraft Normalize() => new(
        Key: "normalize",
        Kind: WorkflowStepKind.InProc,
        Gating: WorkflowStepGating.Auto,
        EnginePref: null,
        OutputSchemaName: "TranscriptTurns",
        Budgets: DefaultBudgets,
        Selectors: [new FragmentSelector(FragmentCategory.Skill, Slug.From("transcript-normalize"))]);

    private static WorkflowStepDraft Discovery() => new(
        Key: "discovery",
        Kind: WorkflowStepKind.Agent,
        Gating: WorkflowStepGating.HumanPo,
        EnginePref: EngineName.Anthropic,
        OutputSchemaName: "DiscoveryObject",
        Budgets: DefaultBudgets,
        Selectors: [new FragmentSelector(FragmentCategory.Identity, Slug.From("po-discovery-assistant"))]);

    [Fact]
    public void Create_AssignsSequentialOrders()
    {
        var w = Workflow.Create(Slug.From("kickoff"), 1, "Kickoff", [Normalize(), Discovery()], Now);

        w.Steps.Should().HaveCount(2);
        w.Steps[0].Order.Should().Be(0);
        w.Steps[0].Key.Should().Be("normalize");
        w.Steps[1].Order.Should().Be(1);
        w.Steps[1].Key.Should().Be("discovery");
    }

    [Fact]
    public void Create_RejectsEmptySteps()
    {
        var act = () => Workflow.Create(Slug.From("empty"), 1, "Empty", [], Now);
        act.Should().Throw<DomainException>().WithMessage("*at least one step*");
    }

    [Fact]
    public void Create_RejectsVersionLessThanOne()
    {
        var act = () => Workflow.Create(Slug.From("kickoff"), 0, "Kickoff", [Normalize()], Now);
        act.Should().Throw<DomainException>().WithMessage("*version*");
    }

    [Fact]
    public void FindStep_ReturnsByKey()
    {
        var w = Workflow.Create(Slug.From("kickoff"), 1, "Kickoff", [Normalize(), Discovery()], Now);

        w.FindStep("discovery")!.Kind.Should().Be(WorkflowStepKind.Agent);
        w.FindStep("missing").Should().BeNull();
    }

    [Fact]
    public void AgentStep_RequiresEnginePref()
    {
        var bad = Discovery() with { EnginePref = null };
        var act = () => Workflow.Create(Slug.From("kickoff"), 1, "Kickoff", [Normalize(), bad], Now);
        act.Should().Throw<DomainException>().WithMessage("*EnginePref*");
    }

    [Fact]
    public void InProcStep_RejectsEnginePref()
    {
        var bad = Normalize() with { EnginePref = EngineName.Anthropic };
        var act = () => Workflow.Create(Slug.From("kickoff"), 1, "Kickoff", [bad], Now);
        act.Should().Throw<DomainException>().WithMessage("*InProc*");
    }

    [Fact]
    public void HumanGateStep_RejectsAutoGating()
    {
        var bad = new WorkflowStepDraft(
            Key: "po-review",
            Kind: WorkflowStepKind.HumanGate,
            Gating: WorkflowStepGating.Auto,
            EnginePref: null,
            OutputSchemaName: null,
            Budgets: DefaultBudgets,
            Selectors: []);
        var act = () => Workflow.Create(Slug.From("k"), 1, "K", [bad], Now);
        act.Should().Throw<DomainException>().WithMessage("*Auto*");
    }
}
