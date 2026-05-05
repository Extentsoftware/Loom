using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// Request shape sent to an IAgentRuntime. Carries the assembled prompt and
/// the budgets the runtime must enforce. ToolGrants are scoped per request;
/// the runtime declines tools not in the grant list at the engine boundary.
/// <para>
/// <see cref="RequiresJsonOutput"/> signals the engine should constrain
/// output to a JSON object (Azure OpenAI / Foundry maps this to
/// response_format: { type: "json_object" }). It's set true when the
/// owning WorkflowStep declares an OutputSchemaName.
/// </para>
/// </summary>
public sealed record AgentRunRequest(
    RunId RunId,
    AssembledPrompt Prompt,
    Budgets Budgets,
    IReadOnlyList<ToolGrant> ToolGrants,
    string? PreferredModel,
    bool RequiresJsonOutput = false);
