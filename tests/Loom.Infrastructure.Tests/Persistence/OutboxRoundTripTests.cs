using FluentAssertions;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loom.Infrastructure.Tests.Persistence;

[Collection(nameof(MsSqlCollection))]
public sealed class OutboxRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OutboxWriter_PersistsEvents_AndAssignsMonotonicSequence()
    {
        var nodeId = NodeId.New();
        var projectId = Guid.NewGuid();

        await using (var ctx = fixture.CreateContext())
        {
            var writer = new OutboxWriter(ctx);
            await writer.WriteAsync(
            [
                new NodeCreated(nodeId, projectId, ParentId: null, NodeType.Feature, Now),
                new NodeUpdated(nodeId, projectId, Now.AddSeconds(1))
            ]);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var entries = await ctx.Outbox
                .AsNoTracking()
                .OrderBy(e => e.Sequence)
                .ToListAsync();

            entries.Should().HaveCountGreaterThanOrEqualTo(2);
            entries.Select(e => e.Sequence).Should().BeInAscendingOrder();
            entries.Select(e => e.EventType).Should().Contain(typeof(NodeCreated).FullName!);
            entries.Select(e => e.EventType).Should().Contain(typeof(NodeUpdated).FullName!);
            entries.Should().AllSatisfy(e => e.ProcessedAt.Should().BeNull());
            entries.Should().AllSatisfy(e => e.Attempts.Should().Be(0));
        }
    }

    [Fact]
    public async Task OutboxEntry_MarkProcessed_SetsTimestamp()
    {
        Guid entryId;
        await using (var ctx = fixture.CreateContext())
        {
            var entry = OutboxEntry.Create(Now, "Test.Event", "{}");
            ctx.Outbox.Add(entry);
            await ctx.SaveChangesAsync();
            entryId = entry.Id;
        }

        await using (var ctx = fixture.CreateContext())
        {
            var entry = await ctx.Outbox.FirstAsync(e => e.Id == entryId);
            entry.MarkProcessed(Now.AddSeconds(5));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var entry = await ctx.Outbox.AsNoTracking().FirstAsync(e => e.Id == entryId);
            entry.ProcessedAt.Should().NotBeNull();
        }
    }
}
