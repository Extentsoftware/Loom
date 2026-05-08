using Loom.Application.Abstractions;
using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

public sealed class NotificationFeed(
    INotificationRepository repo,
    ISystemClock clock,
    IUnitOfWork uow) : INotificationFeed
{
    public Task<IReadOnlyList<Notification>> ListRecentAsync(Guid userId, int take, CancellationToken ct = default) =>
        repo.ListRecentAsync(userId, take, ct);

    public Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, int take, CancellationToken ct = default) =>
        repo.ListUnreadAsync(userId, take, ct);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        repo.UnreadCountAsync(userId, ct);

    public async Task MarkReadAsync(NotificationId id, Guid userId, CancellationToken ct = default)
    {
        var n = await repo.GetAsync(id, ct);
        if (n is null || n.UserId != userId)
        {
            return;
        }
        n.MarkRead(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await repo.ListUnreadAsync(userId, take: 1000, ct);
        if (unread.Count == 0)
        {
            return;
        }
        var now = clock.UtcNow;
        foreach (var n in unread)
        {
            n.MarkRead(now);
        }
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(NotificationId id, Guid userId, CancellationToken ct = default)
    {
        var n = await repo.GetAsync(id, ct);
        if (n is null || n.UserId != userId)
        {
            return;
        }
        repo.Remove(n);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken ct = default)
    {
        // ExecuteDeleteAsync issues a single DELETE, so no per-row tracking
        // needed. SaveChanges is implicit inside ExecuteDelete.
        _ = await repo.DeleteAllForUserAsync(userId, ct);
    }
}
