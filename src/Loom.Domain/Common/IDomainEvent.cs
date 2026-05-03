namespace Loom.Domain.Common;

/// <summary>
/// Marker interface for domain events. Concrete events are records under
/// Loom.Domain.Common.DomainEvents and carry only ids and primitives — never
/// entity references — so they can be serialized into the outbox and read
/// back at any point in the future without dragging in stale aggregate state.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
