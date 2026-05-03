using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Enrichment;

/// <summary>
/// Phase-3 per-node enrichment workflow. Runs after a child node is
/// accepted from the kickoff decompose gate (or on demand from the
/// Feature Workspace's "Enrich" button). Two agent steps:
///
///   1. acceptance — produce acceptance criteria from the node's intent
///      and outcomes. Auto-gated; output writes onto the node.
///   2. risks      — surface risks + mitigations. Auto-gated; output
///      attaches as a Risks artifact (Phase-4 wires the artifact write).
///
/// No human gates here — enrichment is meant to populate scaffolding the
/// PO will review on the Feature Workspace, not to interrupt them.
/// </summary>
public static class EnrichmentWorkflowFactory
{
    public const string WorkflowKey = "enrichment";
    public const int CurrentVersion = 1;

    public const string AcceptanceStepKey = "acceptance";
    public const string RisksStepKey = "risks";

    public static Workflow Build(DateTimeOffset now)
    {
        var defaultBudgets = new Budgets(
            MaxInputTokens: 60_000,
            MaxOutputTokens: 2_500,
            MaxWallClock: TimeSpan.FromMinutes(2),
            MaxCostUsd: 0.50m);

        var drafts = new[]
        {
            new WorkflowStepDraft(
                Key: AcceptanceStepKey,
                Kind: WorkflowStepKind.Agent,
                Gating: WorkflowStepGating.Auto,
                EnginePref: EngineName.Anthropic,
                OutputSchemaName: "AcceptanceCriteria",
                Budgets: defaultBudgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("po-discovery-assistant")),
                    new FragmentSelector(FragmentCategory.Methodology, Slug.From("definition-of-ready"))
                ]),

            new WorkflowStepDraft(
                Key: RisksStepKey,
                Kind: WorkflowStepKind.Agent,
                Gating: WorkflowStepGating.Auto,
                EnginePref: EngineName.Anthropic,
                OutputSchemaName: "RiskRegister",
                Budgets: defaultBudgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("po-architect-pair"))
                ])
        };

        return Workflow.Create(Slug.From(WorkflowKey), CurrentVersion, "Enrichment", drafts, now);
    }
}
