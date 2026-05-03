using Loom.Application.Abstractions;
using Loom.Domain.Common;

namespace Loom.Application.Common;

/// <summary>
/// Plain in-memory implementation of IDomainEventCollector. Scoped lifetime
/// — one instance per HTTP request / per workflow execution / per test.
/// Drain() returns the buffered events and clears the buffer atomically.
/// </summary>
public sealed class DomainEventCollector : IDomainEventCollector
{
    private readonly List<IDomainEvent> _events = [];

    public void Add(IDomainEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        _events.Add(evt);
    }

    public IReadOnlyList<IDomainEvent> Drain()
    {
        var snapshot = _events.ToArray();
        _events.Clear();
        return snapshot;
    }
}
