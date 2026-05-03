using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loom.Infrastructure.Tests.Persistence;

[Collection(nameof(MsSqlCollection))]
public sealed class WorkflowRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Workflow_WithStepsAndSelectors_RoundTrips()
    {
        var defaultBudgets = new Budgets(MaxInputTokens: 8000, MaxOutputTokens: 2000, MaxWallClock: TimeSpan.FromMinutes(2), MaxCostUsd: 0.50m);
        var key = Slug.From($"kickoff-{Guid.NewGuid():N}".Substring(0, 20));

        WorkflowId workflowId;
        await using (var ctx = fixture.CreateContext())
        {
            var w = Workflow.Create(
                key, version: 1, title: "Kickoff",
                stepDrafts:
                [
                    new WorkflowStepDraft(
                        Key: "normalize",
                        Kind: WorkflowStepKind.InProc,
                        Gating: WorkflowStepGating.Auto,
                        EnginePref: null,
                        OutputSchemaName: "TranscriptTurns",
                        Budgets: defaultBudgets,
                        Selectors: [new FragmentSelector(FragmentCategory.Skill, Slug.From("transcript-normalize"))]),
                    new WorkflowStepDraft(
                        Key: "discovery",
                        Kind: WorkflowStepKind.Agent,
                        Gating: WorkflowStepGating.HumanPo,
                        EnginePref: EngineName.Anthropic,
                        OutputSchemaName: "DiscoveryObject",
                        Budgets: defaultBudgets,
                        Selectors:
                        [
                            new FragmentSelector(FragmentCategory.Identity, Slug.From("po-discovery-assistant")),
                            new FragmentSelector(FragmentCategory.Methodology, Slug.From("problem-framing"))
                        ])
                ],
                now: Now);
            workflowId = w.Id;
            ctx.Workflows.Add(w);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var loaded = await ctx.Workflows
                .Include("_steps")
                .AsNoTracking()
                .FirstAsync(w => w.Id == workflowId);

            loaded.Key.Value.Should().Be(key.Value);
            loaded.Version.Should().Be(1);
            loaded.Steps.Should().HaveCount(2);
            loaded.Steps[0].Key.Should().Be("normalize");
            loaded.Steps[0].Kind.Should().Be(WorkflowStepKind.InProc);
            loaded.Steps[0].EnginePref.Should().BeNull();
            loaded.Steps[0].FragmentSelectors.Should().ContainSingle()
                .Which.Key.Value.Should().Be("transcript-normalize");

            loaded.Steps[1].Key.Should().Be("discovery");
            loaded.Steps[1].Kind.Should().Be(WorkflowStepKind.Agent);
            loaded.Steps[1].EnginePref.Should().Be(EngineName.Anthropic);
            loaded.Steps[1].Gating.Should().Be(WorkflowStepGating.HumanPo);
            loaded.Steps[1].FragmentSelectors.Should().HaveCount(2);
            loaded.Steps[1].FragmentSelectors[0].Category.Should().Be(FragmentCategory.Identity);
            loaded.Steps[1].FragmentSelectors[1].Key.Value.Should().Be("problem-framing");
        }
    }
}
