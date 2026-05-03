using FluentAssertions;
using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loom.Infrastructure.Tests.Persistence;

[Collection(nameof(MsSqlCollection))]
public sealed class RunEventRoundTripTests(MsSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunEvents_PersistAndOrderBySequence()
    {
        var runId = RunId.New();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.RunEvents.AddRange(
                RunEvent.StepStarted(runId, 0, "discovery", Now),
                RunEvent.TokenUsage(runId, 1, 1500, 800, Now.AddSeconds(2)),
                RunEvent.GatePaused(runId, 2, "discovery", Now.AddSeconds(3)),
                RunEvent.GateResolved(runId, 3, "discovery", Guid.NewGuid(), Now.AddSeconds(60)),
                RunEvent.StepCompleted(runId, 4, "discovery", Now.AddSeconds(61)));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var events = await ctx.RunEvents
                .AsNoTracking()
                .Where(e => e.RunId == runId)
                .OrderBy(e => e.Sequence)
                .ToListAsync();

            events.Should().HaveCount(5);
            events[0].Kind.Should().Be(RunEventKind.StepStarted);
            events[3].Kind.Should().Be(RunEventKind.GateResolved);
            events[4].Kind.Should().Be(RunEventKind.StepCompleted);
            events.Select(e => e.Sequence).Should().Equal(0, 1, 2, 3, 4);
        }
    }
}
