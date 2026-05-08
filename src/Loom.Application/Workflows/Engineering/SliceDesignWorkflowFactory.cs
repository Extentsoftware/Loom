using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Engineering;

/// <summary>
/// Phase-4 engineering workflow: agent proposes a slice design (data
/// model + endpoints + handler list) from the node's intent and
/// acceptance criteria; a tech lead reviews. Output is projected as a
/// <see cref="Loom.Domain.Artifacts.ArtifactKind.Doc"/> artifact.
/// </summary>
public static class SliceDesignWorkflowFactory
{
    public const string WorkflowKey = "slice-design";
    public const int CurrentVersion = 1;

    public const string ProposeStepKey = "propose-slice";
    public const string ReviewStepKey = "lead-review";

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
                OutputSchemaName: "SliceDesign",
                Budgets: budgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("dotnet-backend-engineer")),
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("technical-architect")),
                    new FragmentSelector(FragmentCategory.Methodology, Slug.From("definition-of-ready"))
                ]),

            new WorkflowStepDraft(
                Key: ReviewStepKey,
                Kind: WorkflowStepKind.HumanGate,
                Gating: WorkflowStepGating.HumanLead,
                EnginePref: null,
                OutputSchemaName: null,
                Budgets: budgets,
                Selectors: [])
        };

        return Workflow.Create(Slug.From(WorkflowKey), CurrentVersion, "Slice design", drafts, now);
    }
}
