using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Xunit;

namespace Loom.Domain.Tests.Fragments;

public sealed class FragmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AnAuthor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AProject = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_GlobalFragment_OK()
    {
        var f = Fragment.Create(
            Slug.From("problem-framing"),
            FragmentCategory.Methodology,
            FragmentScope.Global,
            scopeId: null,
            title: "Problem framing",
            ownerId: AnAuthor,
            now: Now);

        f.Scope.Should().Be(FragmentScope.Global);
        f.ScopeId.Should().BeNull();
        f.CurrentVersionId.Should().BeNull();
    }

    [Fact]
    public void Create_GlobalWithScopeId_Throws()
    {
        var act = () => Fragment.Create(
            Slug.From("a"), FragmentCategory.Methodology, FragmentScope.Global,
            scopeId: AProject, title: "x", ownerId: AnAuthor, now: Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ProjectWithoutScopeId_Throws()
    {
        var act = () => Fragment.Create(
            Slug.From("a"), FragmentCategory.Methodology, FragmentScope.Project,
            scopeId: null, title: "x", ownerId: AnAuthor, now: Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PublishVersion_BecomesCurrent_AndIncrementsNumber()
    {
        var f = NewMethodology();

        var v1 = f.PublishVersion("v1 content", new EngineHints(), "initial", AnAuthor, Now);
        var v2 = f.PublishVersion("v2 content", new EngineHints(), "update", AnAuthor, Now.AddMinutes(1));

        v1.Version.Should().Be(1);
        v2.Version.Should().Be(2);
        f.CurrentVersionId.Should().Be(v2.Id);
        f.Versions.Should().HaveCount(2);
    }

    [Fact]
    public void PublishVersion_RequiresContent()
    {
        var f = NewMethodology();
        var act = () => f.PublishVersion("   ", new EngineHints(), null, AnAuthor, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deprecate_DoesNotRemove()
    {
        var f = NewMethodology();
        var v1 = f.PublishVersion("c", new EngineHints(), null, AnAuthor, Now);

        v1.Deprecate();

        v1.IsDeprecated.Should().BeTrue();
        f.Versions.Should().Contain(v1);
    }

    [Fact]
    public void Tags_NormalizeToLowerAndDedupe()
    {
        var f = NewMethodology();
        f.AddTag("Discovery");
        f.AddTag("DISCOVERY");
        f.AddTag("kickoff");

        f.Tags.Should().Equal("discovery", "kickoff");
    }

    private static Fragment NewMethodology() =>
        Fragment.Create(
            Slug.From("definition-of-ready"),
            FragmentCategory.Methodology,
            FragmentScope.Global,
            scopeId: null,
            title: "Definition of ready",
            ownerId: AnAuthor,
            now: Now);
}
