using Loom.Domain.Common;

namespace Loom.Application.Abstractions;

/// <summary>
/// Per-request bag of domain events accumulated by application services
/// during a unit of work. The unit-of-work commit hook calls Flush, which
/// hands the buffered events to IOutboxWriter inside the same transaction.
/// Scoped lifetime; reset implicitly at scope end.
/// </summary>
public interface IDomainEventCollector
{
    void Add(IDomainEvent evt);
    IReadOnlyList<IDomainEvent> Drain();
}
