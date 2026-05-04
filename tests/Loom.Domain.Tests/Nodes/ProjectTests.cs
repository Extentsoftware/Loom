using FluentAssertions;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Domain.Tests.Nodes;

public sealed class ProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsAndAssigns()
    {
        var p = Project.Create(Slug.From("retail-web"), "  Retail Web  ", Now);
        p.Name.Should().Be("Retail Web");
        p.Slug.Value.Should().Be("retail-web");
        p.CreatedAt.Should().Be(Now);
        p.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void UpdateDescription_NormalizesEmpty()
    {
        var p = Project.Create(Slug.From("a"), "A", Now);
        p.UpdateDescription("   ", Now.AddMinutes(1));
        p.Description.Should().BeNull();

        p.UpdateDescription("Some context", Now.AddMinutes(2));
        p.Description.Should().Be("Some context");
    }

    [Fact]
    public void Rename_TouchesUpdatedAt()
    {
        var p = Project.Create(Slug.From("a"), "A", Now);
        p.Rename("Better name", Now.AddHours(1));
        p.Name.Should().Be("Better name");
        p.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Archive_FlipsFlagAndTouchesUpdatedAt()
    {
        var p = Project.Create(Slug.From("a"), "A", Now);
        p.IsArchived.Should().BeFalse();

        p.Archive(Now.AddHours(1));
        p.IsArchived.Should().BeTrue();
        p.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Archive_OnAlreadyArchived_Throws()
    {
        var p = Project.Create(Slug.From("a"), "A", Now);
        p.Archive(Now.AddHours(1));

        var act = () => p.Archive(Now.AddHours(2));
        act.Should().Throw<DomainException>().WithMessage("*already archived*");
    }

    [Fact]
    public void Unarchive_RestoresAndTouchesUpdatedAt()
    {
        var p = Project.Create(Slug.From("a"), "A", Now);
        p.Archive(Now.AddHours(1));

        p.Unarchive(Now.AddHours(2));
        p.IsArchived.Should().BeFalse();
        p.UpdatedAt.Should().Be(Now.AddHours(2));
    }

    [Fact]
    public void Unarchive_OnNonArchived_Throws()
    {
        var p = Project.Create(Slug.From("a"), "A", Now);

        var act = () => p.Unarchive(Now.AddHours(1));
        act.Should().Throw<DomainException>().WithMessage("*not archived*");
    }
}
