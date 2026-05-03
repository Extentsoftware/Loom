using Loom.Domain.Common;

namespace Loom.Domain.Runs;

public readonly record struct RunEventId(Guid Value) : IEntityId
{
    public static RunEventId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Append-only audit record for a Run's lifecycle. The Sequence column is
/// monotonically increasing per (RunId) and is what the indexer pipeline
/// orders on. Payload is opaque JSON — kind defines its shape.
/// </summary>
public sealed class RunEvent
{
    private RunEvent() { } // EF Core

    private RunEvent(
        RunEventId id,
        RunId runId,
        int sequence,
        RunEventKind kind,
        string? payloadJson,
        DateTimeOffset at)
    {
        Id = id;
        RunId = runId;
        Sequence = sequence;
        Kind = kind;
        PayloadJson = payloadJson;
        At = at;
    }

    public RunEventId Id { get; private set; }
    public RunId RunId { get; private set; }
    public int Sequence { get; private set; }
    public RunEventKind Kind { get; private set; }
    public string? PayloadJson { get; private set; }
    public DateTimeOffset At { get; private set; }

    public static RunEvent StepStarted(RunId runId, int sequence, string stepKey, DateTimeOffset now) =>
        Create(runId, sequence, RunEventKind.StepStarted, $$"""{"step":"{{stepKey}}"}""", now);

    public static RunEvent StepOutput(RunId runId, int sequence, string stepKey, string outputJson, DateTimeOffset now) =>
        Create(runId, sequence, RunEventKind.StepOutput, $$"""{"step":"{{stepKey}}","output":{{outputJson}}}""", now);

    public static RunEvent StepFailed(RunId runId, int sequence, string stepKey, string reason, DateTimeOffset now) =>
        Create(runId, sequence, RunEventKind.StepFailed, $$"""{"step":"{{stepKey}}","reason":{{System.Text.Json.JsonSerializer.Serialize(reason)}}}""", now);

    public static RunEvent GatePaused(RunId runId, int sequence, string stepKey, DateTimeOffset now) =>
        Create(runId, sequence, RunEventKind.GatePaused, $$"""{"step":"{{stepKey}}"}""", now);

    public static RunEvent GateResolved(RunId runId, int sequence, string stepKey, Guid resolvedBy, DateTimeOffset now) =>
        Create(runId, sequence, RunEventKind.GateResolved, $$"""{"step":"{{stepKey}}","by":"{{resolvedBy:N}}"}""", now);

    public static RunEvent StepCompleted(RunId runId, int sequence, string stepKey, DateTimeOffset now) =>
        Create(runId, sequence, RunEventKind.StepCompleted, $$"""{"step":"{{stepKey}}"}""", now);

    public static RunEvent TokenUsage(RunId runId, int sequence, int input, int output, DateTimeOffset now) =>
        Create(runId, sequence, RunEventKind.TokenUsage, $$"""{"in":{{input}},"out":{{output}}}""", now);

    private static RunEvent Create(RunId runId, int sequence, RunEventKind kind, string? payloadJson, DateTimeOffset now)
    {
        if (sequence < 0)
        {
            throw new DomainException("RunEvent sequence must be non-negative.");
        }
        return new RunEvent(RunEventId.New(), runId, sequence, kind, payloadJson, now);
    }
}
