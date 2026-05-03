using FluentAssertions;
using Loom.Application.Features;
using Loom.Application.Tests.Fakes;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Application.Tests.Features;

public sealed class FeatureServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    private static FeatureService MakeService(
        out FakeProjectRepository projects,
        out FakeFeatureNodeRepository nodes,
        out FakeRunRepository runs,
        out FakeDomainEventCollector events,
        out FakeUnitOfWork uow)
    {
        projects = new FakeProjectRepository();
        nodes = new FakeFeatureNodeRepository();
        runs = new FakeRunRepository();
        events = new FakeDomainEventCollector();
        uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);
        return new FeatureService(projects, nodes, runs, events, uow, clock);
    }

    [Fact]
    public async Task CreateRootNode_PersistsAndEmitsNodeCreated()
    {
        var svc = MakeService(out var projects, out _, out _, out var events, out _);
        var p = await svc.CreateProjectAsync(Slug.From("acme"), "Acme", "...");

        var node = await svc.CreateRootNodeAsync(
            p.Id,
            Slug.From("checkout"),
            NodeType.Feature,
            "Express checkout",
            ownerId: Guid.NewGuid());

        node.ProjectId.Should().Be(p.Id);
        node.Type.Should().Be(NodeType.Feature);
        node.ParentId.Should().BeNull();
        events.Recorded.OfType<NodeCreated>().Should().ContainSingle().Which.NodeId.Should().Be(node.Id);
    }

    [Fact]
    public async Task ApplyDiscovery_BulkSetsAndEmitsDiscoveryAccepted()
    {
        var svc = MakeService(out _, out _, out _, out var events, out _);
        var p = await svc.CreateProjectAsync(Slug.From("acme"), "Acme", null);
        var node = await svc.CreateRootNodeAsync(p.Id, Slug.From("c"), NodeType.Feature, "Untitled", Guid.NewGuid());

        var acceptance = new DiscoveryAcceptance(
            Title: "Express checkout",
            Intent: "Reduce friction.",
            Outcomes: [Outcome.Of("Conversion +5%")],
            Hypotheses: [],
            OpenQuestions: ["EU coverage?"],
            Stakeholders: []);

        var by = Guid.NewGuid();
        await svc.ApplyDiscoveryAsync(node.Id, acceptance, by);

        var ws = await svc.GetWorkspaceAsync(node.Id);
        ws.Should().NotBeNull();
        ws!.Title.Should().Be("Express checkout");
        ws.Intent.Should().StartWith("Reduce");
        ws.Outcomes.Should().ContainSingle();

        events.Recorded.OfType<DiscoveryAccepted>().Should().ContainSingle()
            .Which.AcceptedBy.Should().Be(by);
    }

    [Fact]
    public async Task GetTree_BuildsRecursiveStructure()
    {
        var svc = MakeService(out _, out _, out _, out _, out _);
        var p = await svc.CreateProjectAsync(Slug.From("acme"), "Acme", null);
        var initiative = await svc.CreateRootNodeAsync(p.Id, Slug.From("payments"), NodeType.Initiative, "Payments", Guid.NewGuid());
        var feature = await svc.CreateChildNodeAsync(initiative.Id, Slug.From("checkout"), NodeType.Feature, "Checkout", Guid.NewGuid());
        await svc.CreateChildNodeAsync(feature.Id, Slug.From("guest"), NodeType.Capability, "Guest path", Guid.NewGuid());

        var tree = await svc.GetTreeAsync(p.Id);

        tree.Roots.Should().ContainSingle();
        tree.Roots[0].Title.Should().Be("Payments");
        tree.Roots[0].Children.Should().ContainSingle();
        tree.Roots[0].Children[0].Title.Should().Be("Checkout");
        tree.Roots[0].Children[0].Children.Should().ContainSingle();
        tree.Roots[0].Children[0].Children[0].Title.Should().Be("Guest path");
    }
}

public sealed class FeatureServiceSearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_FindsByTitleAndIntent_CaseInsensitive()
    {
        var projects = new FakeProjectRepository();
        var nodes = new FakeFeatureNodeRepository();
        var runs = new FakeRunRepository();
        var events = new FakeDomainEventCollector();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);
        var svc = new FeatureService(projects, nodes, runs, events, uow, clock);

        var p = await svc.CreateProjectAsync(Slug.From("acme"), "Acme", null);
        await svc.CreateRootNodeAsync(p.Id, Slug.From("checkout"), NodeType.Feature, "Express checkout", Guid.NewGuid());
        await svc.CreateRootNodeAsync(p.Id, Slug.From("search"), NodeType.Feature, "Catalog search", Guid.NewGuid());
        var third = await svc.CreateRootNodeAsync(p.Id, Slug.From("orders"), NodeType.Feature, "Order history", Guid.NewGuid());
        await svc.SetIntentAsync(third.Id, "Surface CHECKOUT history for returning users.");

        var hits = await svc.SearchAsync("checkout", projectId: p.Id);

        hits.Should().HaveCount(2);
        hits.Select(h => h.Slug).Should().BeEquivalentTo(["checkout", "orders"]);
    }

    [Fact]
    public async Task SearchAsync_RespectsProjectScope()
    {
        var projects = new FakeProjectRepository();
        var nodes = new FakeFeatureNodeRepository();
        var runs = new FakeRunRepository();
        var events = new FakeDomainEventCollector();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);
        var svc = new FeatureService(projects, nodes, runs, events, uow, clock);

        var pa = await svc.CreateProjectAsync(Slug.From("acme"), "Acme", null);
        var pb = await svc.CreateProjectAsync(Slug.From("beta"), "Beta", null);
        await svc.CreateRootNodeAsync(pa.Id, Slug.From("ax"), NodeType.Feature, "Express checkout", Guid.NewGuid());
        await svc.CreateRootNodeAsync(pb.Id, Slug.From("bx"), NodeType.Feature, "Express checkout", Guid.NewGuid());

        (await svc.SearchAsync("checkout", projectId: pa.Id)).Should().ContainSingle();
        (await svc.SearchAsync("checkout", projectId: pb.Id)).Should().ContainSingle();
        (await svc.SearchAsync("checkout", projectId: null)).Should().HaveCount(2);
    }
}
