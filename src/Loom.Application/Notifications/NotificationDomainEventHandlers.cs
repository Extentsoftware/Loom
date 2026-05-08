using Loom.Application.Abstractions;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

/// <summary>
/// Bridge from outbox-published domain events to <see cref="INotificationService"/>.
/// Each handler renders the event into a <see cref="NotificationPayload"/>
/// and calls dispatch; the service does subscription-matching + channel
/// fan-out from there.
///
/// Registered as IDomainEventHandler&lt;T&gt; for each event type the host
/// cares about; the OutboxDispatcher resolves them per batch.
/// </summary>
public sealed class NodeUpdatedNotificationHandler(INotificationService notifications)
    : IDomainEventHandler<NodeUpdated>
{
    public Task HandleAsync(NodeUpdated evt, CancellationToken ct = default) =>
        notifications.DispatchAsync(
            evt.NodeId,
            evt.ProjectId,
            SubscriptionEventType.NodeUpdated,
            new NotificationPayload(
                Title: "Node updated",
                Body: $"Node {evt.NodeId.Value:N} was updated.",
                DeepLink: new Uri($"/n/{evt.NodeId.Value:D}", UriKind.Relative),
                Severity: NotificationSeverity.Info),
            eventRole: null,
            ct: ct);
}

public sealed class RunPausedNotificationHandler(
    IRunRepository runs,
    IFeatureNodeRepository nodes,
    INotificationService notifications)
    : IDomainEventHandler<RunPausedForHuman>
{
    public async Task HandleAsync(RunPausedForHuman evt, CancellationToken ct = default)
    {
        // The event carries RunId but not NodeId / ProjectId; resolve via
        // the run + node repos so subscription matching has both keys.
        var run = await runs.GetAsync(evt.RunId, ct);
        if (run is null)
        {
            return;
        }
        var node = await nodes.GetAsync(run.NodeId, ct);
        if (node is null)
        {
            return;
        }
        await notifications.DispatchAsync(
            run.NodeId,
            node.ProjectId,
            SubscriptionEventType.RunPaused,
            new NotificationPayload(
                Title: $"Gate awaiting {evt.GatingRole}",
                Body: $"Run paused at step '{evt.StepKey}'.",
                DeepLink: new Uri($"/runs/{evt.RunId.Value:D}/gate", UriKind.Relative),
                Severity: NotificationSeverity.Warning,
                RunId: evt.RunId),
            eventRole: evt.GatingRole,
            ct: ct);
    }
}

public sealed class RunCompletedNotificationHandler(
    INotificationService notifications,
    IRunRepository runs,
    IFeatureNodeRepository nodes) : IDomainEventHandler<RunCompleted>
{
    public async Task HandleAsync(RunCompleted evt, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(evt.RunId, ct);
        if (run is null)
        {
            return;
        }
        var node = await nodes.GetAsync(run.NodeId, ct);
        if (node is null)
        {
            return;
        }
        await notifications.DispatchAsync(
            run.NodeId,
            node.ProjectId,
            SubscriptionEventType.RunCompleted,
            new NotificationPayload(
                Title: "Run completed",
                Body: $"Run {evt.RunId.Value:N} completed (cost ${evt.CostUsd:F4}).",
                DeepLink: new Uri($"/runs/{evt.RunId.Value:D}", UriKind.Relative),
                Severity: NotificationSeverity.Info,
                RunId: evt.RunId),
            eventRole: null,
            ct: ct);
    }
}

public sealed class RunFailedNotificationHandler(
    INotificationService notifications,
    IRunRepository runs,
    IFeatureNodeRepository nodes) : IDomainEventHandler<RunFailed>
{
    public async Task HandleAsync(RunFailed evt, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(evt.RunId, ct);
        if (run is null)
        {
            return;
        }
        var node = await nodes.GetAsync(run.NodeId, ct);
        if (node is null)
        {
            return;
        }
        await notifications.DispatchAsync(
            run.NodeId,
            node.ProjectId,
            SubscriptionEventType.RunFailed,
            new NotificationPayload(
                Title: "Run failed",
                Body: $"Run {evt.RunId.Value:N} failed: {evt.Reason}",
                DeepLink: new Uri($"/runs/{evt.RunId.Value:D}", UriKind.Relative),
                Severity: NotificationSeverity.Error,
                RunId: evt.RunId),
            eventRole: null,
            ct: ct);
    }
}
