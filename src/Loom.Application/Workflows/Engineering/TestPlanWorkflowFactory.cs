using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Engineering;

/// <summary>
/// Phase-4 engineering workflow: derives a structured test plan from the
/// node's acceptance criteria. Single auto-gated agent step; output
/// projects as a <see cref="Loom.Domain.Artifacts.ArtifactKind.TestPlan"/>
/// artifact. No human gate — the test plan is meant as scaffolding the
/// QA engineer extends, not a sign-off step.
/// </summary>
public static class TestPlanWorkflowFactory
{
    public const string WorkflowKey = "test-plan";
    public const int CurrentVersion = 1;

    public const string ProposeStepKey = "propose-test-plan";

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
                OutputSchemaName: "TestPlan",
                Budgets: budgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("qa-engineer")),
                    new FragmentSelector(FragmentCategory.Methodology, Slug.From("definition-of-done"))
                ])
        };

        return Workflow.Create(Slug.From(WorkflowKey), CurrentVersion, "Test plan", drafts, now);
    }
}
