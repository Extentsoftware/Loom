using Loom.Domain.Nodes;
using Loom.Domain.Notifications;

namespace Loom.Application.Abstractions;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetAsync(SubscriptionId id, CancellationToken ct = default);

    /// <summary>
    /// Find subscriptions matching a node + event type that should fire
    /// for any user. Used by the notification dispatcher when an event
    /// arrives.
    /// </summary>
    Task<IReadOnlyList<Subscription>> FindForEventAsync(NodeId nodeId, SubscriptionEventType eventType, CancellationToken ct = default);

    Task<IReadOnlyList<Subscription>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Subscription subscription, CancellationToken ct = default);
    void Remove(Subscription subscription);
}
