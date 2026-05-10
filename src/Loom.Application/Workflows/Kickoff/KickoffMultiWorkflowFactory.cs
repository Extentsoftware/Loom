using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Builds the v1 multi-feature kickoff workflow. Used when the kickoff
/// transcript covers more than one feature; the root node is an
/// Initiative and decompose returns a tree of features (each with
/// capabilities nested under them via <c>parentSlug</c>).
///
/// Three steps in order:
///   1. normalize           — InProc, Auto, takes "transcript" → "normalize.output"
///   2. initiative-discovery — Agent, Foundry, gate=HumanPo, produces DiscoveryObject (initiative-shaped)
///   3. multi-decompose      — Agent, Foundry, gate=HumanPo, produces DecompositionProposal (tree)
///
/// The acceptance services are reused unchanged: AcceptDiscoveryAsync
/// applies to the Initiative root; KickoffService.AcceptDecompositionAsync
/// already topologically resolves <c>parentSlug</c> across the batch.
/// </summary>
public static class KickoffMultiWorkflowFactory
{
    public const string WorkflowKey = "kickoff-multi";
    public const int CurrentVersion = 1;

    public const string NormalizeStepKey = "normalize";
    public const string DiscoveryStepKey = "initiative-discovery";
    public const string DecomposeStepKey = "multi-decompose";

    public static Workflow Build(DateTimeOffset now)
    {
        var defaultBudgets = new Budgets(
            MaxInputTokens: 100_000,
            MaxOutputTokens: 4_000,
            MaxWallClock: TimeSpan.FromMinutes(2),
            MaxCostUsd: 1.00m);

        var drafts = new[]
        {
            new WorkflowStepDraft(
                Key: NormalizeStepKey,
                Kind: WorkflowStepKind.InProc,
                Gating: WorkflowStepGating.Auto,
                EnginePref: null,
                OutputSchemaName: "TranscriptTurns",
                Budgets: defaultBudgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Skill, Slug.From("transcript-normalize"))
                ]),

            new WorkflowStepDraft(
                Key: DiscoveryStepKey,
                Kind: WorkflowStepKind.Agent,
                Gating: WorkflowStepGating.HumanPo,
                EnginePref: EngineName.Foundry,
                OutputSchemaName: "DiscoveryObject",
                Budgets: defaultBudgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("po-discovery-assistant")),
                    new FragmentSelector(FragmentCategory.Skill, Slug.From("initiative-framing")),
                    new FragmentSelector(FragmentCategory.Skill, Slug.From("transcript-multi-discovery-extraction"))
                ]),

            new WorkflowStepDraft(
                Key: DecomposeStepKey,
                Kind: WorkflowStepKind.Agent,
                Gating: WorkflowStepGating.HumanPo,
                EnginePref: EngineName.Foundry,
                OutputSchemaName: "DecompositionProposal",
                Budgets: defaultBudgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("po-architect-pair")),
                    new FragmentSelector(FragmentCategory.Skill, Slug.From("multi-feature-decomposition"))
                ])
        };

        return Workflow.Create(Slug.From(WorkflowKey), CurrentVersion, "Multi-feature kickoff", drafts, now);
    }
}
