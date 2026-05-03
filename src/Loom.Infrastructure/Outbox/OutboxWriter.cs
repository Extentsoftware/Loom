using System.Text.Json;
using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Infrastructure.Persistence;

namespace Loom.Infrastructure.Outbox;

/// <summary>
/// Persists IDomainEvent instances into the outbox table. Called by the unit-
/// of-work commit hook with the events accumulated by IDomainEventCollector,
/// inside the same transaction as the aggregate writes — so a successful
/// SaveChangesAsync atomically writes the row + the event.
/// </summary>
public sealed class OutboxWriter(LoomDbContext db) : IOutboxWriter
{
    public Task WriteAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var evt in events)
        {
            // EventType is the runtime type's full name; the dispatcher
            // resolves handlers by this string so it must remain stable
            // across deploys (anchor it in tests).
            var eventType = evt.GetType().FullName
                ?? throw new InvalidOperationException("Domain event has no full name.");
            var payloadJson = JsonSerializer.Serialize(evt, evt.GetType(), (JsonSerializerOptions?)null);
            db.Outbox.Add(OutboxEntry.Create(evt.OccurredAt, eventType, payloadJson));
        }
        return Task.CompletedTask;
    }
}
