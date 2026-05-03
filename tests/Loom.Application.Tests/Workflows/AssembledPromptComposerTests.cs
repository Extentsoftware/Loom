using FluentAssertions;
using Loom.Application.Fragments;
using Loom.Application.Workflows;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Xunit;

namespace Loom.Application.Tests.Workflows;

public sealed class AssembledPromptComposerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    private static EffectiveFragment MakeEffective(FragmentCategory category, string key, string content, EffectiveFragmentSource source)
    {
        var f = Fragment.Create(Slug.From(key), category, FragmentScope.Global, scopeId: null, title: key.Replace('-', ' '), ownerId: Guid.NewGuid(), now: Now);
        var v = f.PublishVersion(content, new EngineHints(), changeNote: null, authorId: Guid.NewGuid(), now: Now);
        return new EffectiveFragment(f, v, source);
    }

    private static WorkflowStep MakeStep() => Workflow.Create(
        Slug.From("kickoff"), 1, "Kickoff",
        [new WorkflowStepDraft(
            Key: "discovery",
            Kind: WorkflowStepKind.Agent,
            Gating: WorkflowStepGating.HumanPo,
            EnginePref: EngineName.Anthropic,
            OutputSchemaName: "DiscoveryObject",
            Budgets: new Budgets(MaxInputTokens: null, MaxOutputTokens: null, MaxWallClock: null, MaxCostUsd: null),
            Selectors: [])],
        Now).Steps[0];

    private static NodeContext SampleContext() => new(
        NodeId.New(),
        Title: "Express checkout",
        Intent: "Reduce friction.",
        Phase: NodePhase.Discovery,
        Type: NodeType.Feature,
        AncestorTitles: ["Payments"],
        OpenQuestions: ["EU coverage?"],
        Outcomes: [Outcome.Of("Conversion +5%")],
        Hypotheses: []);

    [Fact]
    public void Compose_ProducesSystemPrompt_OrderedByCategory()
    {
        var composer = new AssembledPromptComposer();
        var effectives = new List<EffectiveFragment>
        {
            MakeEffective(FragmentCategory.Skill, "extract-discovery", "skill body", EffectiveFragmentSource.Global),
            MakeEffective(FragmentCategory.Identity, "po-discovery-assistant", "identity body", EffectiveFragmentSource.Global),
            MakeEffective(FragmentCategory.Methodology, "problem-framing", "methodology body", EffectiveFragmentSource.Project)
        };

        var prompt = composer.Compose(
            workflowStep: MakeStep(),
            fragments: effectives,
            nodeContext: SampleContext(),
            inputs: new Dictionary<string, string> { ["transcript"] = "Anna: We need express checkout." },
            runId: RunId.New(),
            now: Now);

        var sys = prompt.SystemPrompt;
        sys.IndexOf("identity body", StringComparison.Ordinal).Should().BeLessThan(sys.IndexOf("methodology body", StringComparison.Ordinal));
        sys.IndexOf("methodology body", StringComparison.Ordinal).Should().BeLessThan(sys.IndexOf("skill body", StringComparison.Ordinal));
        sys.Should().Contain("Express checkout");
        sys.Should().Contain("Reduce friction.");
        sys.Should().Contain("EU coverage?");
    }

    [Fact]
    public void Compose_RecordsFragmentRefs_PinningExactVersions()
    {
        var composer = new AssembledPromptComposer();
        var ef = MakeEffective(FragmentCategory.Identity, "po-discovery-assistant", "ident body", EffectiveFragmentSource.Global);

        var prompt = composer.Compose(
            workflowStep: MakeStep(),
            fragments: [ef],
            nodeContext: SampleContext(),
            inputs: new Dictionary<string, string> { ["x"] = "y" },
            runId: RunId.New(),
            now: Now);

        prompt.Fragments.Should().ContainSingle();
        var fr = prompt.Fragments[0];
        fr.FragmentId.Should().Be(ef.Fragment.Id);
        fr.VersionId.Should().Be(ef.Version.Id);
        fr.Version.Should().Be(ef.Version.Version);
    }

    [Fact]
    public void Compose_WrapsInputsAsTaggedUserMessages()
    {
        var composer = new AssembledPromptComposer();
        var prompt = composer.Compose(
            workflowStep: MakeStep(),
            fragments: [],
            nodeContext: SampleContext(),
            inputs: new Dictionary<string, string>
            {
                ["transcript"] = "Hello.",
                ["prior_output"] = "{\"x\":1}"
            },
            runId: RunId.New(),
            now: Now);

        prompt.Messages.Should().HaveCount(2);
        prompt.Messages.Should().Contain(m => m.Role == "user" && m.Content.Contains("<transcript>"));
        prompt.Messages.Should().Contain(m => m.Role == "user" && m.Content.Contains("<prior_output>"));
    }
}
