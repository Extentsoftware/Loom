using Loom.Domain.Nodes;
using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

public interface ISubscriptionService
{
    Task<Subscription> SubscribeAsync(
        Guid userId,
        NodeId nodeId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        CancellationToken ct = default);

    Task UnsubscribeAsync(SubscriptionId id, CancellationToken ct = default);
    Task<IReadOnlyList<Subscription>> ListMineAsync(Guid userId, CancellationToken ct = default);
}
