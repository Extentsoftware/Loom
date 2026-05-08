using FluentAssertions;
using Loom.Application.Abstractions;
using Loom.Application.Notifications;
using Loom.Domain.Common.DomainEvents;
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
        var projectId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var sub = Subscription.CreateForNode(userId, nodeId, SubscriptionEventType.RunCompleted, SubscriptionChannel.InApp, SubscriptionMode.Realtime, role: null, DateTimeOffset.UtcNow);

        var subs = new FakeSubscriptionRepository();
        await subs.AddAsync(sub);

        var inApp = new RecordingChannel(SubscriptionChannel.InApp);
        var teams = new RecordingChannel(SubscriptionChannel.Teams);

        var svc = new NotificationService(subs, [inApp, teams], NullLogger<NotificationService>.Instance);

        var payload = new NotificationPayload("Run done", "all good", null, NotificationSeverity.Info);
        await svc.DispatchAsync(nodeId, projectId, SubscriptionEventType.RunCompleted, payload);

        inApp.Sends.Should().HaveCount(1);
        inApp.Sends[0].UserId.Should().Be(userId);
        teams.Sends.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_digest_subscription_skipped_in_realtime_path()
    {
        var nodeId = new NodeId(Guid.CreateVersion7());
        var projectId = Guid.CreateVersion7();
        var sub = Subscription.CreateForNode(Guid.CreateVersion7(), nodeId, SubscriptionEventType.NodeUpdated, SubscriptionChannel.Email, SubscriptionMode.Digest, role: null, DateTimeOffset.UtcNow);
        var subs = new FakeSubscriptionRepository();
        await subs.AddAsync(sub);
        var email = new RecordingChannel(SubscriptionChannel.Email);

        var svc = new NotificationService(subs, [email], NullLogger<NotificationService>.Instance);
        await svc.DispatchAsync(nodeId, projectId, SubscriptionEventType.NodeUpdated,
            new NotificationPayload("x", "y", null, NotificationSeverity.Info));

        email.Sends.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_project_scoped_subscription_fires_for_node_in_project()
    {
        var nodeId = new NodeId(Guid.CreateVersion7());
        var projectId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var sub = Subscription.CreateForProject(userId, projectId, SubscriptionEventType.RunPaused, SubscriptionChannel.InApp, SubscriptionMode.Realtime, role: null, DateTimeOffset.UtcNow);

        var subs = new FakeSubscriptionRepository();
        await subs.AddAsync(sub);

        var inApp = new RecordingChannel(SubscriptionChannel.InApp);
        var svc = new NotificationService(subs, [inApp], NullLogger<NotificationService>.Instance);

        await svc.DispatchAsync(nodeId, projectId, SubscriptionEventType.RunPaused,
            new NotificationPayload("Gate", "wait", null, NotificationSeverity.Warning),
            eventRole: WorkflowStepGatingRole.Po);

        inApp.Sends.Should().HaveCount(1);
    }

    [Fact]
    public async Task DispatchAsync_role_filtered_subscription_skips_non_matching_role()
    {
        var nodeId = new NodeId(Guid.CreateVersion7());
        var projectId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var sub = Subscription.CreateForProject(userId, projectId, SubscriptionEventType.RunPaused, SubscriptionChannel.InApp, SubscriptionMode.Realtime, role: WorkflowStepGatingRole.Ux, DateTimeOffset.UtcNow);

        var subs = new FakeSubscriptionRepository();
        await subs.AddAsync(sub);

        var inApp = new RecordingChannel(SubscriptionChannel.InApp);
        var svc = new NotificationService(subs, [inApp], NullLogger<NotificationService>.Instance);

        // Wrong role — should be filtered out.
        await svc.DispatchAsync(nodeId, projectId, SubscriptionEventType.RunPaused,
            new NotificationPayload("Gate", "wait", null, NotificationSeverity.Warning),
            eventRole: WorkflowStepGatingRole.Po);
        inApp.Sends.Should().BeEmpty();

        // Matching role — should fire.
        await svc.DispatchAsync(nodeId, projectId, SubscriptionEventType.RunPaused,
            new NotificationPayload("Gate", "wait", null, NotificationSeverity.Warning),
            eventRole: WorkflowStepGatingRole.Ux);
        inApp.Sends.Should().HaveCount(1);
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
        public Task<IReadOnlyList<Subscription>> FindForEventAsync(
            NodeId nodeId,
            Guid projectId,
            SubscriptionEventType eventType,
            WorkflowStepGatingRole? eventRole,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Subscription>>(_subs
                .Where(s => s.EventType == eventType
                    && (s.NodeId == nodeId || s.ProjectId == projectId)
                    && (s.Role == null || s.Role == eventRole))
                .ToList());
        public Task<IReadOnlyList<Subscription>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Subscription>>(_subs.Where(s => s.UserId == userId).ToList());
        public void Remove(Subscription subscription) => _subs.Remove(subscription);
    }
}
