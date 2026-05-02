using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loom.Infrastructure.Tests.Persistence;

[Collection(nameof(MsSqlCollection))]
public sealed class FeatureNodeRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FeatureNode_FullRoundTrip_PreservesEverything()
    {
        var projectId = await SeedProject(fixture);

        var nodeId = NodeId.New();
        await using (var ctx = fixture.CreateContext())
        {
            var node = FeatureNode.Create(
                projectId,
                parentId: null,
                slug: Slug.From($"saved-card-{Guid.NewGuid():N}".Substring(0, 20)),
                type: NodeType.Capability,
                title: "Saved card surfacing on cart",
                ownerId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
                now: Now);

            node.SetIntent("Default-highlight a tokenized card on cart for returning users.", Now);
            node.AddOutcome(Outcome.Of("Confirmation in <= 3 clicks", "clicks", measurable: true), Now);
            node.AddOutcome(Outcome.Of("No regression in guest flow", measurable: false), Now);
            node.AddConstraint(new Constraint(ConstraintKind.Regulatory, "PCI scope must not expand"), Now);
            node.AddHypothesis(new Hypothesis(
                If: "we surface saved tokens",
                Then: "returning conversion lifts",
                Because: "friction at payment is the dominant abandonment cause"), Now);
            node.AddOpenQuestion("Behaviour for expired cards?", Now);
            node.AddStakeholder(new Stakeholder(UserId: null, Name: "Anna", Role: "PO", Interest: "intent"), Now);

            // Use the entity's actual id for round-trip comparison.
            nodeId = node.Id;
            ctx.Nodes.Add(node);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var loaded = await ctx.Nodes.AsNoTracking().FirstAsync(n => n.Id == nodeId);

            loaded.Title.Should().Be("Saved card surfacing on cart");
            loaded.Type.Should().Be(NodeType.Capability);
            loaded.Phase.Should().Be(NodePhase.Discovery);
            loaded.Intent.Should().Be("Default-highlight a tokenized card on cart for returning users.");

            loaded.Outcomes.Should().HaveCount(2);
            loaded.Outcomes.Select(o => o.Statement)
                .Should().Equal("Confirmation in <= 3 clicks", "No regression in guest flow");
            loaded.Outcomes[0].Measurable.Should().BeTrue();

            loaded.Constraints.Should().ContainSingle().Which.Kind.Should().Be(ConstraintKind.Regulatory);
            loaded.Hypotheses.Should().ContainSingle().Which.Then.Should().Be("returning conversion lifts");
            loaded.OpenQuestions.Should().ContainSingle().Which.Should().Be("Behaviour for expired cards?");
            loaded.Stakeholders.Should().ContainSingle().Which.Name.Should().Be("Anna");
        }
    }

    [Fact]
    public async Task FeatureNode_ConcurrencyVersion_IncrementsOnUpdate()
    {
        var projectId = await SeedProject(fixture);

        var id = NodeId.New();
        await using (var ctx = fixture.CreateContext())
        {
            var node = FeatureNode.Create(
                projectId, null,
                Slug.From($"node-{Guid.NewGuid():N}".Substring(0, 20)),
                NodeType.Feature, "T", Guid.NewGuid(), Now);
            id = node.Id;
            ctx.Nodes.Add(node);
            await ctx.SaveChangesAsync();
        }

        byte[] firstVersion;
        await using (var ctx = fixture.CreateContext())
        {
            var n = await ctx.Nodes.FirstAsync(x => x.Id == id);
            firstVersion = BitConverter.GetBytes(n.Version);
            n.SetIntent("first edit", Now.AddSeconds(1));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var n = await ctx.Nodes.AsNoTracking().FirstAsync(x => x.Id == id);
            BitConverter.GetBytes(n.Version).Should().NotEqual(firstVersion);
        }
    }

    private static async Task<Guid> SeedProject(MsSqlFixture f)
    {
        await using var ctx = f.CreateContext();
        var p = Project.Create(Slug.From($"proj-{Guid.NewGuid():N}".Substring(0, 20)), "Test", Now);
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }
}

[Collection(nameof(MsSqlCollection))]
public sealed class FragmentRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Fragment_WithVersionsAndHints_RoundTrips()
    {
        var owner = Guid.NewGuid();

        FragmentId fragmentId;
        await using (var ctx = fixture.CreateContext())
        {
            var f = Fragment.Create(
                Slug.From($"dor-{Guid.NewGuid():N}".Substring(0, 20)),
                FragmentCategory.Methodology,
                FragmentScope.Global,
                scopeId: null,
                title: "Definition of ready",
                ownerId: owner,
                now: Now);

            f.AddTag("kickoff");
            f.AddTag("readiness");

            f.PublishVersion(
                content: "A feature is ready when ...",
                hints: new EngineHints(PrefersExtendedThinking: false, MaxContextTokens: 4000, RequiresJsonOutput: true),
                changeNote: "initial",
                authorId: owner,
                now: Now);

            f.PublishVersion(
                content: "Updated definition ...",
                hints: new EngineHints(RequiresJsonOutput: true, PreferredModelHint: "small"),
                changeNote: "tightened wording",
                authorId: owner,
                now: Now.AddDays(1));

            fragmentId = f.Id;
            ctx.Fragments.Add(f);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var loaded = await ctx.Fragments
                .Include("_versions")
                .AsNoTracking()
                .FirstAsync(x => x.Id == fragmentId);

            loaded.Versions.Should().HaveCount(2);
            loaded.CurrentVersion.Should().NotBeNull();
            loaded.CurrentVersion!.Version.Should().Be(2);
            loaded.CurrentVersion.Hints.RequiresJsonOutput.Should().BeTrue();
            loaded.CurrentVersion.Hints.PreferredModelHint.Should().Be("small");
            loaded.Tags.Should().BeEquivalentTo(["kickoff", "readiness"]);
        }
    }
}

[Collection(nameof(MsSqlCollection))]
public sealed class RunRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Run_StateMachine_PersistsAcrossContexts()
    {
        var nodeId = NodeId.New();
        var fragmentRef = new FragmentRef(FragmentId.New(), FragmentVersionId.New(), 1);
        RunId runId;

        await using (var ctx = fixture.CreateContext())
        {
            var run = Run.Queue(
                nodeId,
                workflowId: Guid.NewGuid(),
                stepId: Guid.NewGuid(),
                engine: EngineName.Foundry,
                budgets: new Budgets(8000, 2000, TimeSpan.FromMinutes(1), 1.00m),
                fragments: [fragmentRef],
                now: Now);
            runId = run.Id;
            ctx.Runs.Add(run);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var run = await ctx.Runs.FirstAsync(r => r.Id == runId);
            run.MarkRunning("foundry-thread-xyz", Now.AddSeconds(1));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var run = await ctx.Runs.FirstAsync(r => r.Id == runId);
            run.Complete(
                new Cost("gpt-4o-mini", "loom-eastus-prod", 1500, 800, 0.06m),
                Now.AddSeconds(30));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var run = await ctx.Runs.AsNoTracking().FirstAsync(r => r.Id == runId);
            run.State.Should().Be(RunState.Completed);
            run.ExternalRunId.Should().Be("foundry-thread-xyz");
            run.Cost.Should().NotBeNull();
            run.Cost!.UsdAmount.Should().Be(0.06m);
            run.Fragments.Should().ContainSingle()
                .Which.Should().Be(fragmentRef);
        }
    }
}
