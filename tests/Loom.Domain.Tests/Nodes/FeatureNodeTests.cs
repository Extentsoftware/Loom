using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Domain.Tests.Nodes;

public sealed class FeatureNodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AProject = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static FeatureNode NewFeature(string title = "Express checkout flow") =>
        FeatureNode.Create(
            AProject,
            parentId: null,
            slug: Slug.From("express-checkout"),
            type: NodeType.Feature,
            title: title,
            ownerId: AnOwner,
            now: Now);

    [Fact]
    public void Create_StartsInDiscovery()
    {
        var node = NewFeature();
        node.Phase.Should().Be(NodePhase.Discovery);
        node.Title.Should().Be("Express checkout flow");
        node.Type.Should().Be(NodeType.Feature);
        node.Version.Should().Be(0u);
    }

    [Fact]
    public void Create_RejectsEmptyOwner()
    {
        var act = () => FeatureNode.Create(
            AProject, null, Slug.From("a"), NodeType.Feature, "x", Guid.Empty, Now);
        act.Should().Throw<DomainException>().WithMessage("*Owner*");
    }

    [Fact]
    public void Create_RejectsEmptyTitle()
    {
        var act = () => FeatureNode.Create(
            AProject, null, Slug.From("a"), NodeType.Feature, "  ", AnOwner, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetIntent_TouchesAndTrims()
    {
        var node = NewFeature();
        var later = Now.AddMinutes(5);

        node.SetIntent("  Reduce friction  ", later);

        node.Intent.Should().Be("Reduce friction");
        node.UpdatedAt.Should().Be(later);
        node.Version.Should().Be(1u);
    }

    [Fact]
    public void SetIntent_OnDoneNode_Throws()
    {
        var node = NewFeature();
        AdvanceTo(node, NodePhase.Done);

        var act = () => node.SetIntent("new intent", Now.AddDays(1));
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(NodePhase.Discovery, NodePhase.Enrich, true)]
    [InlineData(NodePhase.Enrich, NodePhase.Build, true)]
    [InlineData(NodePhase.Build, NodePhase.Test, true)]
    [InlineData(NodePhase.Test, NodePhase.Done, true)]
    [InlineData(NodePhase.Discovery, NodePhase.Build, false)]   // skipping
    [InlineData(NodePhase.Build, NodePhase.Discovery, false)]    // backward
    [InlineData(NodePhase.Discovery, NodePhase.Discovery, false)] // self
    public void AdvancePhase_RespectsAllowedTransitions(NodePhase from, NodePhase to, bool allowed)
    {
        var node = NewFeature();
        AdvanceTo(node, from);

        var act = () => node.AdvancePhase(to, Now.AddHours(1));

        if (allowed)
        {
            act.Should().NotThrow();
            node.Phase.Should().Be(to);
        }
        else
        {
            act.Should().Throw<DomainException>();
            node.Phase.Should().Be(from);
        }
    }

    [Fact]
    public void AdvancePhase_AnyNonTerminalToArchived_Allowed()
    {
        foreach (var from in new[] { NodePhase.Discovery, NodePhase.Enrich, NodePhase.Build, NodePhase.Test, NodePhase.Done })
        {
            var node = NewFeature();
            AdvanceTo(node, from);
            node.AdvancePhase(NodePhase.Archived, Now.AddHours(1));
            node.Phase.Should().Be(NodePhase.Archived);
        }
    }

    [Fact]
    public void Reopen_OnlyFromTerminal()
    {
        var node = NewFeature();
        AdvanceTo(node, NodePhase.Done);
        node.Reopen(Now.AddDays(1));
        node.Phase.Should().Be(NodePhase.Discovery);

        var act = () => node.Reopen(Now.AddDays(2));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reparent_RejectsSelfParent()
    {
        var node = NewFeature();
        var act = () => node.Reparent(node.Id, Now.AddMinutes(1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddOutcome_AppendsAndTouches()
    {
        var node = NewFeature();
        node.AddOutcome(Outcome.Of("Confirmation in <= 3 clicks", "clicks", measurable: true), Now.AddMinutes(1));
        node.Outcomes.Should().HaveCount(1);
        node.Outcomes[0].Statement.Should().Be("Confirmation in <= 3 clicks");
        node.Outcomes[0].Measurable.Should().BeTrue();
    }

    [Fact]
    public void ReplaceOutcomes_ClearsThenAdds()
    {
        var node = NewFeature();
        node.AddOutcome(Outcome.Of("A"), Now);
        node.AddOutcome(Outcome.Of("B"), Now);

        node.ReplaceOutcomes([Outcome.Of("C"), Outcome.Of("D")], Now.AddMinutes(1));

        node.Outcomes.Select(o => o.Statement).Should().Equal("C", "D");
    }

    [Fact]
    public void Outcomes_AreReadOnly_FromOutside()
    {
        var node = NewFeature();
        node.Outcomes.Should().BeAssignableTo<IReadOnlyList<Outcome>>();
    }

    private static void AdvanceTo(FeatureNode node, NodePhase target)
    {
        var seq = new[] { NodePhase.Enrich, NodePhase.Build, NodePhase.Test, NodePhase.Done };
        foreach (var p in seq)
        {
            if (node.Phase == target)
            {
                return;
            }
            node.AdvancePhase(p, Now.AddMinutes((int)p));
        }
    }
}
