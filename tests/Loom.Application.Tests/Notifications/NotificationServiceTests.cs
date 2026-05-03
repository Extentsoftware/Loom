using FluentAssertions;
using Loom.Application.Abstractions;
using Loom.Application.Notifications;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Loom.Application.Tests.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task DispatchAsync_realtime_subscription_calls_matching_channel()
    {
        var nodeId = new NodeId(Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();
        var sub = Subscription.Create(userId, nodeId, SubscriptionEventType.RunCompleted, SubscriptionChannel.InApp, SubscriptionMode.Realtime, DateTimeOffset.UtcNow);

        var subs = new FakeSubscriptionRepository();
        await subs.AddAsync(sub);

        var inApp = new RecordingChannel(SubscriptionChannel.InApp);
        var teams = new RecordingChannel(SubscriptionChannel.Teams);

        var svc = new NotificationService(subs, [inApp, teams], NullLogger<NotificationService>.Instance);

        var payload = new NotificationPayload("Run done", "all good", null, NotificationSeverity.Info);
        await svc.DispatchAsync(nodeId, SubscriptionEventType.RunCompleted, payload);

        inApp.Sends.Should().HaveCount(1);
        inApp.Sends[0].UserId.Should().Be(userId);
        teams.Sends.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_digest_subscription_skipped_in_realtime_path()
    {
        var nodeId = new NodeId(Guid.CreateVersion7());
        var sub = Subscription.Create(Guid.CreateVersion7(), nodeId, SubscriptionEventType.NodeUpdated, SubscriptionChannel.Email, SubscriptionMode.Digest, DateTimeOffset.UtcNow);
        var subs = new FakeSubscriptionRepository();
        await subs.AddAsync(sub);
        var email = new RecordingChannel(SubscriptionChannel.Email);

        var svc = new NotificationService(subs, [email], NullLogger<NotificationService>.Instance);
        await svc.DispatchAsync(nodeId, SubscriptionEventType.NodeUpdated,
            new NotificationPayload("x", "y", null, NotificationSeverity.Info));

        email.Sends.Should().BeEmpty();
    }

    private sealed class RecordingChannel(SubscriptionChannel channel) : INotificationChannel
    {
        public SubscriptionChannel Channel { get; } = channel;
        public List<(Guid UserId, NodeId Node, NotificationPayload Payload)> Sends { get; } = [];
        public Task SendAsync(Guid userId, NodeId nodeId, NotificationPayload payload, CancellationToken ct = default)
        {
            Sends.Add((userId, nodeId, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSubscriptionRepository : ISubscriptionRepository
    {
        private readonly List<Subscription> _subs = [];
        public Task AddAsync(Subscription subscription, CancellationToken ct = default)
        {
            _subs.Add(subscription);
            return Task.CompletedTask;
        }
        public Task<Subscription?> GetAsync(SubscriptionId id, CancellationToken ct = default) =>
            Task.FromResult(_subs.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<Subscription>> FindForEventAsync(NodeId nodeId, SubscriptionEventType eventType, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Subscription>>(_subs.Where(s => s.NodeId == nodeId && s.EventType == eventType).ToList());
        public Task<IReadOnlyList<Subscription>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Subscription>>(_subs.Where(s => s.UserId == userId).ToList());
        public void Remove(Subscription subscription) => _subs.Remove(subscription);
    }
}
