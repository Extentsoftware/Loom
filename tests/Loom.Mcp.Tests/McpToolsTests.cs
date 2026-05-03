using FluentAssertions;
using Loom.Application.Abstractions;
using Loom.Application.Features;
using Loom.Application.Fragments;
using Loom.Application.Tests.Fakes;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Mcp.Tools;
using Xunit;

namespace Loom.Mcp.Tests;

/// <summary>
/// Phase-2 MCP tools wired against the real application services with fake
/// repositories. Verifies the wire shapes of the JSON results — IDE clients
/// hardcode against these.
/// </summary>
public sealed class McpToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetNodeContext_ResolvesByGuid_AndReturnsAssembledFragments()
    {
        var (nodes, projects, fragmentService, fragments) = MakeServices();
        var owner = Guid.NewGuid();
        var p = await ProvisionProjectAndNodeAsync(projects, nodes, "checkout", "Express checkout", owner);

        SeedFragment(fragments, FragmentCategory.Identity, "po-discovery-assistant", "you are a PO");

        var result = await NodeContextTool.GetNodeContextAsync(
            nodes, projects, fragmentService, p.NodeId.Value.ToString());

        result.Ok.Should().BeTrue();
        result.Node.Should().NotBeNull();
        result.Node!.Slug.Should().Be("checkout");
        result.Node.EffectiveFragments.Should().ContainSingle(f => f.Key == "po-discovery-assistant");
    }

    [Fact]
    public async Task GetNodeContext_ResolvesBySlug()
    {
        var (nodes, projects, fragmentService, _) = MakeServices();
        var owner = Guid.NewGuid();
        var p = await ProvisionProjectAndNodeAsync(projects, nodes, "checkout", "Express checkout", owner);

        var result = await NodeContextTool.GetNodeContextAsync(
            nodes, projects, fragmentService, "checkout");

        result.Ok.Should().BeTrue();
        result.Node!.NodeId.Should().Be(p.NodeId.Value);
    }

    [Fact]
    public async Task GetNodeContext_UnknownRef_ReturnsNotFound()
    {
        var (nodes, projects, fragmentService, _) = MakeServices();

        var result = await NodeContextTool.GetNodeContextAsync(
            nodes, projects, fragmentService, "no-such-thing");

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("no-such-thing");
    }

    [Fact]
    public async Task SearchNodes_ReturnsMatchingHits()
    {
        var (nodes, projects, _, _) = MakeServices();
        var owner = Guid.NewGuid();
        await ProvisionProjectAndNodeAsync(projects, nodes, "checkout", "Express checkout", owner);
        await ProvisionProjectAndNodeAsync(projects, nodes, "search-bar", "Catalog search", owner);

        var featureService = new FeatureService(
            (FakeProjectRepository)projects,
            (FakeFeatureNodeRepository)nodes,
            new FakeRunRepository(),
            new FakeDomainEventCollector(),
            new FakeUnitOfWork(),
            new FakeSystemClock(Now));

        var result = await SearchNodesTool.SearchAsync(featureService, "checkout");

        result.Count.Should().Be(1);
        result.Hits[0].Slug.Should().Be("checkout");
    }

    [Fact]
    public async Task ListRules_RendersGlobalAndProjectFragments_OrderedByCategory()
    {
        var (_, projects, _, fragments) = MakeServices();
        SeedFragment(fragments, FragmentCategory.Skill, "extract", "skill body");
        SeedFragment(fragments, FragmentCategory.Identity, "po-discovery-assistant", "identity body");
        var pid = Guid.NewGuid();
        SeedFragment(fragments, FragmentCategory.Project, "stack-conventions", "project body",
            scope: FragmentScope.Project, scopeId: pid);

        var result = await ListRulesTool.ListAsync(fragments, pid);

        result.Count.Should().Be(3);
        var idIdentity = result.Markdown.IndexOf("identity body", StringComparison.Ordinal);
        var idProject = result.Markdown.IndexOf("project body", StringComparison.Ordinal);
        var idSkill = result.Markdown.IndexOf("skill body", StringComparison.Ordinal);
        idIdentity.Should().BeLessThan(idProject);
        idProject.Should().BeLessThan(idSkill);
    }

    private static (
        IFeatureNodeRepository nodes,
        IProjectRepository projects,
        IFragmentService fragmentService,
        IFragmentRepository fragments) MakeServices()
    {
        var projects = new FakeProjectRepository();
        var nodes = new FakeFeatureNodeRepository();
        var fragments = new FakeFragmentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);
        var fragmentService = new FragmentService(fragments, nodes, uow, clock);
        return (nodes, projects, fragmentService, fragments);
    }

    private static async Task<(Guid ProjectId, NodeId NodeId)> ProvisionProjectAndNodeAsync(
        IProjectRepository projects, IFeatureNodeRepository nodes, string slug, string title, Guid owner)
    {
        var project = Project.Create(Slug.From($"p-{slug}"), $"Project {slug}", Now);
        await projects.AddAsync(project);
        var node = FeatureNode.Create(project.Id, parentId: null, Slug.From(slug), NodeType.Feature, title, owner, Now);
        await nodes.AddAsync(node);
        return (project.Id, node.Id);
    }

    private static void SeedFragment(
        IFragmentRepository fragments, FragmentCategory category, string key, string content,
        FragmentScope scope = FragmentScope.Global, Guid? scopeId = null)
    {
        var owner = Guid.NewGuid();
        var f = Fragment.Create(Slug.From(key), category, scope, scopeId, $"{key} title", owner, Now);
        f.PublishVersion(content, new EngineHints(), changeNote: null, authorId: owner, now: Now);
        ((FakeFragmentRepository)fragments).ById[f.Id] = f;
    }
}
