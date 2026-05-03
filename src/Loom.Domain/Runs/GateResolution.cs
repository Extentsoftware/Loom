namespace Loom.Domain.Runs;

public enum GateDecision
{
    Accepted = 1,
    Rejected = 2
}

/// <summary>
/// Captures how a human gate was resolved. Stored on the gate run for audit
/// and replay; the WorkflowEngine consumes <c>Edits</c> as inputs into the
/// next step (e.g. PO-edited DiscoveryObject feeds the decompose step).
/// </summary>
public sealed record GateResolution(
    GateDecision Decision,
    Guid ResolvedBy,
    DateTimeOffset ResolvedAt,
    string? Reason,
    IReadOnlyDictionary<string, string>? Edits);
