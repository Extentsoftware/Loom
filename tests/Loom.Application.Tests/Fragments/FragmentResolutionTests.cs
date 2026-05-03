using FluentAssertions;
using Loom.Application.Fragments;
using Loom.Application.Tests.Fakes;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Workflows;
using Xunit;

namespace Loom.Application.Tests.Fragments;

public sealed class FragmentResolutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Fragment SeedFragment(
        FakeFragmentRepository repo,
        FragmentCategory category,
        string key,
        FragmentScope scope,
        Guid? scopeId,
        string content)
    {
        var f = Fragment.Create(Slug.From(key), category, scope, scopeId, $"{key} title", Owner, Now);
        f.PublishVersion(content, new EngineHints(), changeNote: null, authorId: Owner, now: Now);
        repo.ById[f.Id] = f;
        return f;
    }

    [Fact]
    public async Task EffectiveSet_LocalNodeOverridesProjectAndGlobal()
    {
        var nodes = new FakeFeatureNodeRepository();
        var fragments = new FakeFragmentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);

        var projectId = Guid.NewGuid();
        var node = FeatureNode.Create(projectId, parentId: null, slug: Slug.From("a"), type: NodeType.Feature, title: "A", ownerId: Owner, now: Now);
        await nodes.AddAsync(node);

        SeedFragment(fragments, FragmentCategory.Identity, "po-discovery-assistant", FragmentScope.Global, null, "global identity");
        SeedFragment(fragments, FragmentCategory.Identity, "po-discovery-assistant", FragmentScope.Project, projectId, "project identity");
        SeedFragment(fragments, FragmentCategory.Identity, "po-discovery-assistant", FragmentScope.Node, node.Id.Value, "node identity");

        var svc = new FragmentService(fragments, nodes, uow, clock);
        var selectors = new[] { new FragmentSelector(FragmentCategory.Identity, Slug.From("po-discovery-assistant")) };

        var effective = await svc.GetEffectiveFragmentsAsync(node.Id, selectors);

        effective.Should().ContainSingle();
        effective[0].Source.Should().Be(EffectiveFragmentSource.Node);
        effective[0].Version.Content.Should().Be("node identity");
    }

    [Fact]
    public async Task EffectiveSet_FallsBackToProjectThenGlobal()
    {
        var nodes = new FakeFeatureNodeRepository();
        var fragments = new FakeFragmentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);

        var projectId = Guid.NewGuid();
        var node = FeatureNode.Create(projectId, parentId: null, slug: Slug.From("a"), type: NodeType.Feature, title: "A", ownerId: Owner, now: Now);
        await nodes.AddAsync(node);

        SeedFragment(fragments, FragmentCategory.Methodology, "problem-framing", FragmentScope.Global, null, "global methodology");
        SeedFragment(fragments, FragmentCategory.Methodology, "problem-framing", FragmentScope.Project, projectId, "project methodology");

        var svc = new FragmentService(fragments, nodes, uow, clock);
        var selectors = new[] { new FragmentSelector(FragmentCategory.Methodology, Slug.From("problem-framing")) };

        var effective = await svc.GetEffectiveFragmentsAsync(node.Id, selectors);

        effective.Should().ContainSingle();
        effective[0].Source.Should().Be(EffectiveFragmentSource.Project);
        effective[0].Version.Content.Should().Be("project methodology");
    }

    [Fact]
    public async Task EffectiveSet_AncestorScopedFragmentApplies()
    {
        var nodes = new FakeFeatureNodeRepository();
        var fragments = new FakeFragmentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);

        var projectId = Guid.NewGuid();
        var parent = FeatureNode.Create(projectId, parentId: null, slug: Slug.From("p"), type: NodeType.Initiative, title: "P", ownerId: Owner, now: Now);
        await nodes.AddAsync(parent);
        var child = FeatureNode.Create(projectId, parentId: parent.Id, slug: Slug.From("c"), type: NodeType.Feature, title: "C", ownerId: Owner, now: Now);
        await nodes.AddAsync(child);

        SeedFragment(fragments, FragmentCategory.Domain, "glossary", FragmentScope.Global, null, "global glossary");
        SeedFragment(fragments, FragmentCategory.Domain, "glossary", FragmentScope.Node, parent.Id.Value, "ancestor glossary");

        var svc = new FragmentService(fragments, nodes, uow, clock);
        var selectors = new[] { new FragmentSelector(FragmentCategory.Domain, Slug.From("glossary")) };

        var effective = await svc.GetEffectiveFragmentsAsync(child.Id, selectors);

        effective.Should().ContainSingle();
        effective[0].Source.Should().Be(EffectiveFragmentSource.Ancestor);
        effective[0].Version.Content.Should().Be("ancestor glossary");
    }

    [Fact]
    public async Task EffectiveSet_DeprecatedVersion_IsExcluded()
    {
        var nodes = new FakeFeatureNodeRepository();
        var fragments = new FakeFragmentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);

        var projectId = Guid.NewGuid();
        var node = FeatureNode.Create(projectId, parentId: null, slug: Slug.From("a"), type: NodeType.Feature, title: "A", ownerId: Owner, now: Now);
        await nodes.AddAsync(node);

        var f = SeedFragment(fragments, FragmentCategory.Skill, "extract", FragmentScope.Global, null, "v1");
        f.CurrentVersion!.Deprecate();

        var svc = new FragmentService(fragments, nodes, uow, clock);
        var selectors = new[] { new FragmentSelector(FragmentCategory.Skill, Slug.From("extract")) };

        var effective = await svc.GetEffectiveFragmentsAsync(node.Id, selectors);

        effective.Should().BeEmpty();
    }
}
