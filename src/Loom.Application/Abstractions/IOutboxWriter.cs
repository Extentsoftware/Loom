using Loom.Domain.Common;

namespace Loom.Application.Abstractions;

/// <summary>
/// Persists domain events into the outbox table within the same transaction
/// as the aggregate change that produced them. Implementation lives in
/// Loom.Infrastructure; the Application layer holds the seam so services can
/// flush their event collector without taking a hard dependency on EF.
/// </summary>
public interface IOutboxWriter
{
    Task WriteAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}
