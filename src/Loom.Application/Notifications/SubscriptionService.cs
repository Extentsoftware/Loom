using Loom.Application.Abstractions;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

public sealed class SubscriptionService(
    ISubscriptionRepository subscriptions,
    IUnitOfWork uow,
    ISystemClock clock) : ISubscriptionService
{
    public async Task<Subscription> SubscribeNodeAsync(
        Guid userId,
        NodeId nodeId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role = null,
        CancellationToken ct = default)
    {
        var existing = (await subscriptions.ListByUserAsync(userId, ct))
            .FirstOrDefault(s =>
                s.NodeId == nodeId
                && s.ProjectId == null
                && s.EventType == eventType
                && s.Channel == channel
                && s.Role == role);
        if (existing is not null)
        {
            existing.ChangeMode(mode, clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return existing;
        }
        var sub = Subscription.CreateForNode(userId, nodeId, eventType, channel, mode, role, clock.UtcNow);
        await subscriptions.AddAsync(sub, ct);
        await uow.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<Subscription> SubscribeProjectAsync(
        Guid userId,
        Guid projectId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role = null,
        CancellationToken ct = default)
    {
        var existing = (await subscriptions.ListByUserAsync(userId, ct))
            .FirstOrDefault(s =>
                s.NodeId == null
                && s.ProjectId == projectId
                && s.EventType == eventType
                && s.Channel == channel
                && s.Role == role);
        if (existing is not null)
        {
            existing.ChangeMode(mode, clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return existing;
        }
        var sub = Subscription.CreateForProject(userId, projectId, eventType, channel, mode, role, clock.UtcNow);
        await subscriptions.AddAsync(sub, ct);
        await uow.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<Subscription> SubscribeGlobalAsync(
        Guid userId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role = null,
        CancellationToken ct = default)
    {
        var existing = (await subscriptions.ListByUserAsync(userId, ct))
            .FirstOrDefault(s =>
                s.NodeId == null
                && s.ProjectId == null
                && s.EventType == eventType
                && s.Channel == channel
                && s.Role == role);
        if (existing is not null)
        {
            existing.ChangeMode(mode, clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return existing;
        }
        var sub = Subscription.CreateGlobal(userId, eventType, channel, mode, role, clock.UtcNow);
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
