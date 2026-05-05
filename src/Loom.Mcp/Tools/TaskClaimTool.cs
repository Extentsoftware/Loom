using System.ComponentModel;
using System.Text.Json.Serialization;
using Loom.Application.Runs;
using Loom.Domain.Common;
using Loom.Domain.Runs;
using ModelContextProtocol.Server;

namespace Loom.Mcp.Tools;

/// <summary>
/// MCP tools for the developer-facing pull-claim flow. A developer's Claude
/// Code instance lists what's queued for them, claims one, acts on the
/// prompt, and reports the gate output back. Identity is currently a
/// userId parameter — per-call MCP auth is a follow-up.
/// </summary>
[McpServerToolType]
public static class TaskClaimTool
{
    [McpServerTool(Name = "loom_list_my_tasks")]
    [Description("List human-gated runs available to a Loom user — paused tasks they could claim or already own.")]
    public static async Task<TaskListResult> ListAsync(
        IRunAssignmentService assignment,
        [Description("Loom user id (GUID).")] string userId,
        [Description("If true, only tasks already assigned to this user. Default false (also returns unclaimed tasks).")] bool onlyMine = false,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return new TaskListResult(false, "userId is not a GUID.", []);
        }
        try
        {
            var tasks = onlyMine
                ? await assignment.ListAssignedAsync(userGuid, ct)
                : await assignment.ListClaimableAsync(userGuid, ct);
            return new TaskListResult(true, null, [.. tasks.Select(ToDto)]);
        }
        catch (DomainException ex)
        {
            return new TaskListResult(false, ex.Message, []);
        }
    }

    [McpServerTool(Name = "loom_claim_task")]
    [Description("Claim a paused human-gated Loom run for a user. Fails if the run is already claimed by someone else.")]
    public static async Task<TaskResult> ClaimAsync(
        IRunAssignmentService assignment,
        [Description("Run id (GUID) to claim.")] string runId,
        [Description("Loom user id (GUID) claiming the task.")] string userId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(runId, out var runGuid))
        {
            return new TaskResult(false, "runId is not a GUID.", null);
        }
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return new TaskResult(false, "userId is not a GUID.", null);
        }
        try
        {
            var env = await assignment.ClaimAsync(new RunId(runGuid), userGuid, ct);
            return new TaskResult(true, null, ToDto(env));
        }
        catch (DomainException ex)
        {
            return new TaskResult(false, ex.Message, null);
        }
    }

    [McpServerTool(Name = "loom_complete_task")]
    [Description("Submit the gate output (free-form key/value edits) and resolve a paused human gate. Caller must be the current assignee.")]
    public static async Task<SimpleResult> CompleteAsync(
        IRunAssignmentService assignment,
        [Description("Run id (GUID).")] string runId,
        [Description("Loom user id (GUID); must match the run's assignee.")] string userId,
        [Description("Optional dictionary of edits to apply. Pass null/empty to accept the agent's output unchanged.")] Dictionary<string, string>? edits = null,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(runId, out var runGuid))
        {
            return new SimpleResult(false, "runId is not a GUID.");
        }
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return new SimpleResult(false, "userId is not a GUID.");
        }
        try
        {
            await assignment.CompleteAsync(new RunId(runGuid), userGuid, edits, ct);
            return new SimpleResult(true, null);
        }
        catch (DomainException ex)
        {
            return new SimpleResult(false, ex.Message);
        }
    }

    [McpServerTool(Name = "loom_release_task")]
    [Description("Release a previously claimed task back to the unassigned pool.")]
    public static async Task<SimpleResult> ReleaseAsync(
        IRunAssignmentService assignment,
        [Description("Run id (GUID).")] string runId,
        [Description("Loom user id (GUID); must currently be the assignee.")] string userId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(runId, out var runGuid))
        {
            return new SimpleResult(false, "runId is not a GUID.");
        }
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return new SimpleResult(false, "userId is not a GUID.");
        }
        try
        {
            await assignment.ReleaseAsync(new RunId(runGuid), userGuid, ct);
            return new SimpleResult(true, null);
        }
        catch (DomainException ex)
        {
            return new SimpleResult(false, ex.Message);
        }
    }

    private static TaskDto ToDto(TaskEnvelope e) => new(
        RunId: e.RunId.Value,
        NodeId: e.NodeId.Value,
        NodeTitle: e.NodeTitle,
        StepKey: e.StepKey,
        OutputSchemaName: e.OutputSchemaName,
        PromptText: e.PromptText,
        AssigneeUserId: e.AssigneeUserId,
        CreatedAt: e.CreatedAt,
        DeepLink: e.DeepLink);

    public sealed record TaskListResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("tasks")] IReadOnlyList<TaskDto> Tasks);

    public sealed record TaskResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("task")] TaskDto? Task);

    public sealed record SimpleResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error);

    public sealed record TaskDto(
        [property: JsonPropertyName("run_id")] Guid RunId,
        [property: JsonPropertyName("node_id")] Guid NodeId,
        [property: JsonPropertyName("node_title")] string NodeTitle,
        [property: JsonPropertyName("step_key")] string? StepKey,
        [property: JsonPropertyName("output_schema_name")] string? OutputSchemaName,
        [property: JsonPropertyName("prompt_text")] string? PromptText,
        [property: JsonPropertyName("assignee_user_id")] Guid? AssigneeUserId,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("deep_link")] string DeepLink);
}
