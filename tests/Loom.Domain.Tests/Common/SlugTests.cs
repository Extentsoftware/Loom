using FluentAssertions;
using Loom.Domain.Common;
using Xunit;

namespace Loom.Domain.Tests.Common;

public sealed class SlugTests
{
    [Theory]
    [InlineData("checkout-2026")]
    [InlineData("a")]
    [InlineData("a1-b2-c3")]
    [InlineData("retail-web")]
    public void From_Valid_ReturnsSlug(string raw)
    {
        var s = Slug.From(raw);
        s.Value.Should().Be(raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Checkout-2026")] // uppercase
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("with space")]
    [InlineData("with_underscore")]
    [InlineData("with.dot")]
    public void From_Invalid_Throws(string raw)
    {
        var act = () => Slug.From(raw);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryFrom_Invalid_ReturnsFalse()
    {
        Slug.TryFrom("Bad Slug", out _).Should().BeFalse();
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Slug.From("foo").Should().Be(Slug.From("foo"));
        Slug.From("foo").Should().NotBe(Slug.From("bar"));
    }
}
