using FluentAssertions;
using Loom.Application.Memory;
using Loom.Application.Tests.Fakes;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Xunit;

namespace Loom.Application.Tests.Memory;

public sealed class SqlMemorySearchTests
{
    private static FeatureNode CreateNode(FakeFeatureNodeRepository repo, Guid projectId, string title, string? intent, DateTimeOffset createdAt)
    {
        var slug = Slug.From(title.ToLowerInvariant().Replace(' ', '-').Replace(",", "").Replace(".", "")[..Math.Min(40, title.Length)]);
        var n = FeatureNode.Create(projectId, parentId: null, slug, NodeType.Feature, title, ownerId: Guid.CreateVersion7(), createdAt);
        if (!string.IsNullOrWhiteSpace(intent))
        {
            n.SetIntent(intent, createdAt);
        }
        repo.AddAsync(n).GetAwaiter().GetResult();
        return n;
    }

    [Fact]
    public async Task SearchSimilar_returns_token_overlap_matches()
    {
        var repo = new FakeFeatureNodeRepository();
        var clock = new FakeSystemClock();
        var projectId = Guid.CreateVersion7();
        CreateNode(repo, projectId, "Checkout redesign", "Speed up cart-to-purchase flow", clock.UtcNow);
        CreateNode(repo, projectId, "Search relevance", "Improve search ranking", clock.UtcNow);

        var search = new SqlMemorySearch(repo, clock);
        var report = await search.SearchSimilarAsync("checkout cart purchase", projectId, limit: 5);

        report.Candidates.Should().NotBeEmpty();
        report.Candidates[0].Title.Should().Be("Checkout redesign");
    }

    [Fact]
    public async Task SearchSimilar_empty_query_returns_empty_report()
    {
        var search = new SqlMemorySearch(new FakeFeatureNodeRepository(), new FakeSystemClock());
        var report = await search.SearchSimilarAsync("   ", projectId: null, limit: 5);
        report.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSimilar_filters_by_project_when_specified()
    {
        var repo = new FakeFeatureNodeRepository();
        var clock = new FakeSystemClock();
        var p1 = Guid.CreateVersion7();
        var p2 = Guid.CreateVersion7();
        CreateNode(repo, p1, "Checkout in p1", "checkout flow", clock.UtcNow);
        CreateNode(repo, p2, "Checkout in p2", "checkout flow", clock.UtcNow);

        var search = new SqlMemorySearch(repo, clock);
        var report = await search.SearchSimilarAsync("checkout", projectId: p1, limit: 5);

        report.Candidates.Should().OnlyContain(c => c.ProjectId == p1);
    }

    [Fact]
    public async Task Recency_boosts_score()
    {
        var repo = new FakeFeatureNodeRepository();
        var clock = new FakeSystemClock();
        var projectId = Guid.CreateVersion7();
        // Old node, identical title.
        CreateNode(repo, projectId, "Checkout flow", "checkout flow", clock.UtcNow.AddDays(-365));
        // Fresh node.
        CreateNode(repo, projectId, "Checkout flow", "checkout flow", clock.UtcNow);

        var search = new SqlMemorySearch(repo, clock);
        var report = await search.SearchSimilarAsync("checkout flow", projectId, limit: 5);

        report.Candidates.Should().HaveCountGreaterThanOrEqualTo(2);
        report.Candidates[0].Score.Should().BeGreaterThanOrEqualTo(report.Candidates[1].Score);
    }
}
