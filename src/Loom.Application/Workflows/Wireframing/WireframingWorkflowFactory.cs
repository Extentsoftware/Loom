using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Wireframing;

/// <summary>
/// Phase-4 (skinny) wireframing workflow: produces a draft wireframe as a
/// raw HTML/SVG payload, persists it as a Wireframe artifact, and pauses
/// at a UX-gated review step. The Figma adapter / plugin lives in the
/// fuller Phase 4 cut — this is the minimum viable round-trip the team
/// can use today: agent-generated → projected as artifact → reviewable.
/// </summary>
public static class WireframingWorkflowFactory
{
    public const string WorkflowKey = "wireframing";
    public const int CurrentVersion = 1;

    public const string ProposeStepKey = "propose-wireframe";
    public const string ReviewStepKey = "ux-review";

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
                OutputSchemaName: "Wireframe",
                Budgets: budgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("blazor-frontend-engineer")),
                    new FragmentSelector(FragmentCategory.Methodology, Slug.From("definition-of-ready"))
                ]),

            new WorkflowStepDraft(
                Key: ReviewStepKey,
                Kind: WorkflowStepKind.HumanGate,
                Gating: WorkflowStepGating.HumanUx,
                EnginePref: null,
                OutputSchemaName: null,
                Budgets: budgets,
                Selectors: [])
        };

        return Workflow.Create(Slug.From(WorkflowKey), CurrentVersion, "Wireframing", drafts, now);
    }
}
