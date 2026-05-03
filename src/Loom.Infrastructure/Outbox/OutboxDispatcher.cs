using System.Text.Json;
using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loom.Infrastructure.Outbox;

public sealed class OutboxDispatcherOptions
{
    public const string SectionName = "Loom:Outbox";

    /// <summary>How long to sleep between batches when there is no work.</summary>
    public TimeSpan IdlePollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum rows claimed per batch.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Attempts before a row is dead-lettered (Attempts = -1).</summary>
    public int MaxAttempts { get; set; } = 5;
}

/// <summary>
/// Background pump that drains the outbox table. Each tick:
///   1. Claim up to BatchSize unprocessed rows ordered by Sequence.
///   2. Deserialize the payload to the concrete IDomainEvent type.
///   3. Resolve every IDomainEventHandler&lt;T&gt; from the per-tick scope and
///      call them in registration order. Handlers are expected to be
///      idempotent — at-least-once delivery, not exactly-once.
///   4. Mark the row processed (or increment Attempts on failure).
///
/// SignalR broadcasts (NodeHubBroadcaster), Phase 6 indexer projections, and
/// Phase 3 notification fan-out all hang off this pump as handlers.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    DomainEventTypeRegistry typeRegistry,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        OutboxDispatcherLog.Started(logger, opts.BatchSize, opts.IdlePollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(opts, stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(opts.IdlePollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                OutboxDispatcherLog.LoopErrored(logger, ex);
                // Pause briefly to avoid hot-looping on a persistent failure.
                await Task.Delay(opts.IdlePollInterval, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(OutboxDispatcherOptions opts, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoomDbContext>();

        // Claim rows: dead-lettered (Attempts < 0) and already-processed
        // (ProcessedAt != null) rows are skipped. Phase 5+ swaps this for a
        // proper UPDLOCK/READPAST claim pattern when we actually run >1
        // dispatcher instance; Phase 1 has one, no contention.
        var batch = await db.Outbox
            .Where(o => o.ProcessedAt == null && o.Attempts >= 0 && o.Attempts < opts.MaxAttempts)
            .OrderBy(o => o.Sequence)
            .Take(opts.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            return 0;
        }

        foreach (var entry in batch)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!typeRegistry.TryResolve(entry.EventType, out var clrType))
                {
                    OutboxDispatcherLog.UnknownEventType(logger, entry.EventType);
                    entry.RecordFailure();
                    continue;
                }

                var deserialized = JsonSerializer.Deserialize(entry.PayloadJson, clrType, Json) as IDomainEvent;
                if (deserialized is null)
                {
                    OutboxDispatcherLog.DeserializeReturnedNull(logger, entry.EventType);
                    entry.RecordFailure();
                    continue;
                }

                await DispatchAsync(scope.ServiceProvider, clrType, deserialized, ct);
                entry.MarkProcessed(DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                OutboxDispatcherLog.HandlerFailed(logger, ex, entry.EventType, entry.Id);
                entry.RecordFailure();
            }
        }

        await db.SaveChangesAsync(ct);
        return batch.Count;
    }

    private static async Task DispatchAsync(IServiceProvider services, Type eventType, IDomainEvent evt, CancellationToken ct)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var handlers = services.GetServices(handlerType);

        // Cache reflection — there's only ever one HandleAsync per closed
        // generic so this is cheap, but worth pulling above the foreach.
        var handleAsync = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))
            ?? throw new InvalidOperationException($"No HandleAsync on {handlerType.Name}.");

        foreach (var handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }
            var task = (Task?)handleAsync.Invoke(handler, [evt, ct]);
            if (task is not null)
            {
                await task;
            }
        }
    }
}

internal static partial class OutboxDispatcherLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OutboxDispatcher started (batch size {BatchSize}, idle poll {IdlePollInterval})")]
    public static partial void Started(ILogger logger, int batchSize, TimeSpan idlePollInterval);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Unknown event type '{EventType}' in outbox; flagging row for retry / dead-letter")]
    public static partial void UnknownEventType(ILogger logger, string eventType);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Deserializing event type '{EventType}' returned null")]
    public static partial void DeserializeReturnedNull(ILogger logger, string eventType);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Domain event handler failed for {EventType} (outbox id {OutboxId})")]
    public static partial void HandlerFailed(ILogger logger, Exception ex, string eventType, Guid outboxId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Outbox dispatcher loop errored")]
    public static partial void LoopErrored(ILogger logger, Exception ex);
}
