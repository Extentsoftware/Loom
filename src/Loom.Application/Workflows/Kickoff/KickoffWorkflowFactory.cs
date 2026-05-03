using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Builds the v1 kickoff workflow. Three steps in order:
///   1. normalize    — InProc, Auto, takes "transcript" → "normalize.output"
///   2. discovery    — Agent, Anthropic, gate=HumanPo, produces DiscoveryObject
///   3. decompose    — Agent, Anthropic, gate=HumanPo, produces DecompositionProposal
/// Phase 7's Workflow Designer will replace this code-as-data factory with
/// a YAML-driven definition.
/// </summary>
public static class KickoffWorkflowFactory
{
    public const string WorkflowKey = "kickoff";
    public const int CurrentVersion = 1;

    public const string NormalizeStepKey = "normalize";
    public const string DiscoveryStepKey = "discovery";
    public const string DecomposeStepKey = "decompose";

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
                EnginePref: EngineName.Anthropic,
                OutputSchemaName: "DiscoveryObject",
                Budgets: defaultBudgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("po-discovery-assistant")),
                    new FragmentSelector(FragmentCategory.Methodology, Slug.From("problem-framing")),
                    new FragmentSelector(FragmentCategory.Methodology, Slug.From("definition-of-ready")),
                    new FragmentSelector(FragmentCategory.Skill, Slug.From("transcript-discovery-extraction"))
                ]),

            new WorkflowStepDraft(
                Key: DecomposeStepKey,
                Kind: WorkflowStepKind.Agent,
                Gating: WorkflowStepGating.HumanPo,
                EnginePref: EngineName.Anthropic,
                OutputSchemaName: "DecompositionProposal",
                Budgets: defaultBudgets,
                Selectors:
                [
                    new FragmentSelector(FragmentCategory.Identity, Slug.From("po-architect-pair")),
                    new FragmentSelector(FragmentCategory.Skill, Slug.From("feature-decomposition"))
                ])
        };

        return Workflow.Create(Slug.From(WorkflowKey), CurrentVersion, "Kickoff", drafts, now);
    }
}
