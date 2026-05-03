namespace Loom.Infrastructure.Outbox;

/// <summary>
/// Persistence row for a buffered domain event. Lives in Loom.Infrastructure
/// because the outbox is a persistence concern; the application layer never
/// constructs these directly. Sequence is monotonically assigned by the DB
/// (IDENTITY column) and is the order Phase 6's indexer ships records in.
/// </summary>
public sealed class OutboxEntry
{
    private OutboxEntry() { } // EF Core

    private OutboxEntry(Guid id, DateTimeOffset occurredAt, string eventType, string payloadJson)
    {
        Id = id;
        OccurredAt = occurredAt;
        EventType = eventType;
        PayloadJson = payloadJson;
        Attempts = 0;
    }

    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string EventType { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }

    public static OutboxEntry Create(DateTimeOffset occurredAt, string eventType, string payloadJson) =>
        new(Guid.CreateVersion7(), occurredAt, eventType, payloadJson);

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
    }

    public void RecordFailure()
    {
        Attempts++;
    }
}
