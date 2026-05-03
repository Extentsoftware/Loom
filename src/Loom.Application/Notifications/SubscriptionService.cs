using Loom.Application.Abstractions;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

public sealed class SubscriptionService(
    ISubscriptionRepository subscriptions,
    IUnitOfWork uow,
    ISystemClock clock) : ISubscriptionService
{
    public async Task<Subscription> SubscribeAsync(
        Guid userId,
        NodeId nodeId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        CancellationToken ct = default)
    {
        var existing = (await subscriptions.ListByUserAsync(userId, ct))
            .FirstOrDefault(s => s.NodeId == nodeId && s.EventType == eventType && s.Channel == channel);
        if (existing is not null)
        {
            existing.ChangeMode(mode, clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return existing;
        }
        var sub = Subscription.Create(userId, nodeId, eventType, channel, mode, clock.UtcNow);
        await subscriptions.AddAsync(sub, ct);
        await uow.SaveChangesAsync(ct);
        return sub;
    }

    public async Task UnsubscribeAsync(SubscriptionId id, CancellationToken ct = default)
    {
        var sub = await subscriptions.GetAsync(id, ct);
        if (sub is null)
        {
            return;
        }
        subscriptions.Remove(sub);
        await uow.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<Subscription>> ListMineAsync(Guid userId, CancellationToken ct = default) =>
        subscriptions.ListByUserAsync(userId, ct);
}
