using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Runs;

/// <summary>
/// Pull-style task assignment for human-gated runs. Backs the MCP tools
/// that let a developer's Claude Code instance ask "what's queued for me?",
/// claim a task, and report the gate output back to Loom.
///
/// Identity is currently a parameter — the MCP server has no per-call
/// auth yet, so the calling tool passes the user's Loom id explicitly.
/// Real per-user MCP tokens are a follow-up.
/// </summary>
public interface IRunAssignmentService
{
    /// <summary>
    /// Tasks the user can act on right now: paused at a human gate, and
    /// either unassigned or already assigned to <paramref name="userId"/>.
    /// </summary>
    Task<IReadOnlyList<TaskEnvelope>> ListClaimableAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Tasks already assigned to the user (paused or otherwise non-terminal).
    /// </summary>
    Task<IReadOnlyList<TaskEnvelope>> ListAssignedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Take ownership of an unclaimed task. Idempotent if the same user is
    /// already the assignee; throws on a different assignee.
    /// </summary>
    Task<TaskEnvelope> ClaimAsync(RunId runId, Guid userId, CancellationToken ct = default);

    Task ReleaseAsync(RunId runId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Submit the gate output and resolve the human gate. The run continues
    /// (or terminates) per the workflow definition. Validates the user is
    /// the current assignee.
    /// </summary>
    Task CompleteAsync(RunId runId, Guid userId, IReadOnlyDictionary<string, string>? edits, CancellationToken ct = default);
}

/// <summary>
/// What a Claude Code instance receives for a task: the run + the
/// assembled prompt that was sent to the agent so it has context to act.
/// Schema name + step key let the caller know what shape its response
/// must follow.
/// </summary>
public sealed record TaskEnvelope(
    RunId RunId,
    NodeId NodeId,
    string NodeTitle,
    string? StepKey,
    string? OutputSchemaName,
    string? PromptText,
    Guid? AssigneeUserId,
    DateTimeOffset CreatedAt,
    string DeepLink);
