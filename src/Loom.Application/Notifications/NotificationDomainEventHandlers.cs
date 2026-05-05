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
            SubscriptionEventType.NodeUpdated,
            new NotificationPayload(
                Title: "Node updated",
                Body: $"Node {evt.NodeId.Value:N} was updated.",
                DeepLink: new Uri($"/n/{evt.NodeId.Value:D}", UriKind.Relative),
                Severity: NotificationSeverity.Info),
            ct);
}

public sealed class RunPausedNotificationHandler(IRunRepository runs, INotificationService notifications)
    : IDomainEventHandler<RunPausedForHuman>
{
    public async Task HandleAsync(RunPausedForHuman evt, CancellationToken ct = default)
    {
        // The event carries RunId but not NodeId; resolve via the run repo
        // before fanning out so subscription matching has a node key.
        // Phase-3.5 may push NodeId onto the event payload directly.
        var run = await runs.GetAsync(evt.RunId, ct);
        if (run is null)
        {
            return;
        }
        await notifications.DispatchAsync(
            run.NodeId,
            SubscriptionEventType.RunPaused,
            new NotificationPayload(
                Title: $"Gate awaiting {evt.GatingRole}",
                Body: $"Run paused at step '{evt.StepKey}'.",
                DeepLink: new Uri($"/runs/{evt.RunId.Value:D}/gate", UriKind.Relative),
                Severity: NotificationSeverity.Warning,
                RunId: evt.RunId),
            ct);
    }
}

public sealed class RunCompletedNotificationHandler(
    INotificationService notifications,
    IRunRepository runs) : IDomainEventHandler<RunCompleted>
{
    public async Task HandleAsync(RunCompleted evt, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(evt.RunId, ct);
        if (run is null)
        {
            return;
        }
        await notifications.DispatchAsync(
            run.NodeId,
            SubscriptionEventType.RunCompleted,
            new NotificationPayload(
                Title: "Run completed",
                Body: $"Run {evt.RunId.Value:N} completed (cost ${evt.CostUsd:F4}).",
                DeepLink: new Uri($"/runs/{evt.RunId.Value:D}", UriKind.Relative),
                Severity: NotificationSeverity.Info,
                RunId: evt.RunId),
            ct);
    }
}

public sealed class RunFailedNotificationHandler(
    INotificationService notifications,
    IRunRepository runs) : IDomainEventHandler<RunFailed>
{
    public async Task HandleAsync(RunFailed evt, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(evt.RunId, ct);
        if (run is null)
        {
            return;
        }
        await notifications.DispatchAsync(
            run.NodeId,
            SubscriptionEventType.RunFailed,
            new NotificationPayload(
                Title: "Run failed",
                Body: $"Run {evt.RunId.Value:N} failed: {evt.Reason}",
                DeepLink: new Uri($"/runs/{evt.RunId.Value:D}", UriKind.Relative),
                Severity: NotificationSeverity.Error,
                RunId: evt.RunId),
            ct);
    }
}
