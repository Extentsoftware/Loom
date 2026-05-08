using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Engineering;

/// <summary>
/// Phase-4 engineering workflow: agent proposes a stepwise
/// implementation plan from the node's slice design + acceptance
/// criteria; the assigned developer reviews and accepts. The plan is
/// what the dev pulls into their IDE / Claude Code session.
/// </summary>
public static class BuildWorkflowFactory
{
    public const string WorkflowKey = "build";
    public const int CurrentVersion = 1;

    public const string ProposeStepKey = "propose-implementation-plan";
    public const string ReviewStepKey = "dev-review";

    public static Workflow Build(DateTimeOffset now)
    {
        var budgets = new Budgets(
            MaxInputTokens: 60_000,
            MaxOutputTokens: 4_000,
            MaxWallClock: TimeSpan.FromMinutes(3),
            MaxCostUsd: 1.00m);

        var drafts = new[]
        {
            new WorkflowStepDraft(
                Key: ProposeStepKey,
                Kind: WorkflowStepKind.Agent,
                Gating: WorkflowStepGating.Auto,
                EnginePref: EngineName.Foundry,
                OutputSchemaName: "ImplementationPlan",
                Budgets: budgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("dotnet-backend-engineer")),
                    new FragmentSelector(FragmentCategory.Methodology, Slug.From("definition-of-done"))
                ]),

            new WorkflowStepDraft(
                Key: ReviewStepKey,
                Kind: WorkflowStepKind.HumanGate,
                Gating: WorkflowStepGating.HumanDev,
                EnginePref: null,
                OutputSchemaName: null,
                Budgets: budgets,
                Selectors: [])
        };

        return Workflow.Create(Slug.From(WorkflowKey), CurrentVersion, "Build", drafts, now);
    }
}
