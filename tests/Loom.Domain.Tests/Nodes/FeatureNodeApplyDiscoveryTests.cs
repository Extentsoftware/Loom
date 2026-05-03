using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Domain.Tests.Nodes;

public sealed class FeatureNodeApplyDiscoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AProject = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static FeatureNode NewFeature() => FeatureNode.Create(
        AProject,
        parentId: null,
        slug: Slug.From("checkout"),
        type: NodeType.Feature,
        title: "Untitled",
        ownerId: AnOwner,
        now: Now);

    private static DiscoveryAcceptance SampleAcceptance() => new(
        Title: "Express checkout flow",
        Intent: "Reduce checkout abandonment by removing form friction.",
        Outcomes: [Outcome.Of("Cart-to-purchase conversion +5%", "%", measurable: true)],
        Hypotheses: [new Hypothesis("If we remove the address step", "then conversion lifts", "because most users repeat-buy")],
        OpenQuestions: ["Do we have address-on-file coverage in EU?"],
        Stakeholders: [new Stakeholder(UserId: null, Name: "Jane Doe", Role: "PM", Interest: "Champion")]);

    [Fact]
    public void ApplyDiscovery_BulkSetsFields()
    {
        var node = NewFeature();
        var t = Now.AddMinutes(5);

        node.ApplyDiscovery(SampleAcceptance(), t);

        node.Title.Should().Be("Express checkout flow");
        node.Intent.Should().StartWith("Reduce checkout");
        node.Outcomes.Should().ContainSingle();
        node.Hypotheses.Should().ContainSingle();
        node.OpenQuestions.Should().ContainSingle();
        node.Stakeholders.Should().ContainSingle();
        node.UpdatedAt.Should().Be(t);
    }

    [Fact]
    public void ApplyDiscovery_BumpsVersionOnce()
    {
        var node = NewFeature();
        var beforeVersion = node.Version;

        node.ApplyDiscovery(SampleAcceptance(), Now.AddMinutes(1));

        node.Version.Should().Be(beforeVersion + 1);
    }

    [Fact]
    public void ApplyDiscovery_ReplacesCollectionsWholesale()
    {
        var node = NewFeature();
        node.AddOutcome(Outcome.Of("Old outcome"), Now);
        node.AddOpenQuestion("Old question", Now);

        node.ApplyDiscovery(SampleAcceptance(), Now.AddMinutes(1));

        node.Outcomes.Should().ContainSingle().Which.Statement.Should().Contain("conversion");
        node.OpenQuestions.Should().ContainSingle().Which.Should().Contain("address-on-file");
    }

    [Fact]
    public void ApplyDiscovery_RejectsDoneNode()
    {
        var node = NewFeature();
        node.AdvancePhase(NodePhase.Enrich, Now);
        node.AdvancePhase(NodePhase.Build, Now);
        node.AdvancePhase(NodePhase.Test, Now);
        node.AdvancePhase(NodePhase.Done, Now);

        var act = () => node.ApplyDiscovery(SampleAcceptance(), Now.AddMinutes(1));

        act.Should().Throw<DomainException>().WithMessage("*Done*");
    }

    [Fact]
    public void ApplyDiscovery_RejectsNullAcceptance()
    {
        var node = NewFeature();
        var act = () => node.ApplyDiscovery(null!, Now);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyDiscovery_LeavesTitleWhenAcceptanceTitleEmpty()
    {
        var node = NewFeature();
        var ac = SampleAcceptance() with { Title = "  " };

        node.ApplyDiscovery(ac, Now.AddMinutes(1));

        node.Title.Should().Be("Untitled");
    }
}
