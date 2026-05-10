using FluentAssertions;
using Loom.Application.Abstractions;
using Loom.Application.Features;
using Loom.Application.Tests.Fakes;
using Loom.Application.Workflows;
using Loom.Application.Workflows.Kickoff;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Xunit;

namespace Loom.Application.Tests.Workflows.Kickoff;

/// <summary>
/// Focused tests for <see cref="KickoffService.AcceptDecompositionAsync"/>'s
/// parent-slug topo-resolution. The end-to-end kickoff test bypasses
/// this method and creates child nodes directly via FeatureService, so
/// the multi-feature wiring would otherwise be unverified.
/// </summary>
public sealed class KickoffServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid PoUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task AcceptDecomposition_NestsCapabilitiesUnderTheirFeature()
    {
        var (kickoff, features, projects, nodes) = BuildHarness();

        // Initiative root, simulating a multi-feature kickoff.
        var project = await features.CreateProjectAsync(
            Slug.From("checkout-2026"), "Checkout 2026", description: null);
        var initiative = await features.CreateRootNodeAsync(
            project.Id, Slug.From("checkout-init"), NodeType.Initiative,
            "Checkout 2026", PoUserId);

        // Two features, three capabilities — capabilities point at
        // their feature via ParentSlug. The order is intentionally
        // bad: a capability appears before its feature in the input,
        // so we exercise the topological sort.
        var accepted = new List<ProposedChildAcceptance>
        {
            new(Slug.From("guest-checkout-saved-card"), NodeType.Capability,
                "Saved-card surfacing on guest path", Intent: null,
                ParentSlug: Slug.From("guest-checkout")),
            new(Slug.From("guest-checkout"), NodeType.Feature,
                "Guest checkout", Intent: "Single-form path for first-time buyers."),
            new(Slug.From("returning-checkout"), NodeType.Feature,
                "Returning-user checkout", Intent: "Three-click path for saved-card customers."),
            new(Slug.From("returning-saved-card"), NodeType.Capability,
                "Saved-card surfacing on cart", Intent: null,
                ParentSlug: Slug.From("returning-checkout")),
            new(Slug.From("returning-cvv"), NodeType.Capability,
                "Optional CVV revalidation", Intent: null,
                ParentSlug: Slug.From("returning-checkout")),
        };

        var fakeRunId = RunId.New();
        await kickoff.AcceptDecompositionAsync(
            fakeRunId, KickoffMultiWorkflowFactory.DecomposeStepKey,
            initiative.Id, accepted, PoUserId);

        var initiativeChildren = await nodes.GetChildrenAsync(initiative.Id);
        initiativeChildren.Select(n => n.Slug.Value).Should().BeEquivalentTo(
            ["guest-checkout", "returning-checkout"],
            "only top-level features should be direct children of the initiative");

        var guest = initiativeChildren.Single(n => n.Slug.Value == "guest-checkout");
        var guestKids = await nodes.GetChildrenAsync(guest.Id);
        guestKids.Select(n => n.Slug.Value).Should().BeEquivalentTo(
            ["guest-checkout-saved-card"],
            "the saved-card capability should nest under guest-checkout");

        var returning = initiativeChildren.Single(n => n.Slug.Value == "returning-checkout");
        var returningKids = await nodes.GetChildrenAsync(returning.Id);
        returningKids.Select(n => n.Slug.Value).Should().BeEquivalentTo(
            ["returning-saved-card", "returning-cvv"],
            "both returning-user capabilities should nest under returning-checkout");
    }

    [Fact]
    public async Task AcceptDecomposition_FallsBackToKickoffParent_WhenParentSlugUnresolvable()
    {
        var (kickoff, features, projects, nodes) = BuildHarness();

        var project = await features.CreateProjectAsync(
            Slug.From("p1"), "P1", description: null);
        var feature = await features.CreateRootNodeAsync(
            project.Id, Slug.From("feat1"), NodeType.Feature,
            "Feature", PoUserId);

        // ParentSlug points at a slug that doesn't exist in the batch
        // and isn't a child of the kickoff parent. The service should
        // create the child under the kickoff parent rather than fail.
        var accepted = new List<ProposedChildAcceptance>
        {
            new(Slug.From("orphan-cap"), NodeType.Capability,
                "Orphan capability", Intent: null,
                ParentSlug: Slug.From("never-proposed")),
        };

        await kickoff.AcceptDecompositionAsync(
            RunId.New(), KickoffWorkflowFactory.DecomposeStepKey,
            feature.Id, accepted, PoUserId);

        var kids = await nodes.GetChildrenAsync(feature.Id);
        kids.Should().ContainSingle()
            .Which.Slug.Value.Should().Be("orphan-cap");
    }

    private static (
        KickoffService kickoff,
        FeatureService features,
        FakeProjectRepository projects,
        FakeFeatureNodeRepository nodes) BuildHarness()
    {
        var projects = new FakeProjectRepository();
        var nodes = new FakeFeatureNodeRepository();
        var runs = new FakeRunRepository();
        var events = new FakeDomainEventCollector();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);
        var features = new FeatureService(projects, nodes, runs, events, uow, clock);
        var engine = new NoopWorkflowEngine();
        var kickoff = new KickoffService(features, engine, events, clock, uow);
        return (kickoff, features, projects, nodes);
    }

    private sealed class NoopWorkflowEngine : IWorkflowEngine
    {
        public Task<RunId?> StartAsync(NodeId nodeId, WorkflowId workflowId,
            IReadOnlyDictionary<string, string> initialInputs, CancellationToken ct = default)
            => Task.FromResult<RunId?>(null);

        public Task ResolveGateAsync(RunId runId, string stepKey, Guid resolvedBy,
            IReadOnlyDictionary<string, string>? edits = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
