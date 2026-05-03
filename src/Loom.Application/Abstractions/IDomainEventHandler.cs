using System.Diagnostics.CodeAnalysis;
using Loom.Domain.Common;

namespace Loom.Application.Abstractions;

/// <summary>
/// Outbox dispatcher target. Handlers are registered in DI; the dispatcher
/// resolves all handlers for an event's runtime type and calls them in
/// registration order. Handlers are expected to be idempotent — the outbox
/// guarantees at-least-once delivery, not exactly-once.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Standard DDD/CQRS name; the CA1711 rule's concern about delegate-suffix collision does not apply to a generic interface.")]
public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T evt, CancellationToken ct = default);
}
