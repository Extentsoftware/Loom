using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// Request shape sent to an IAgentRuntime. Carries the assembled prompt and
/// the budgets the runtime must enforce. ToolGrants are scoped per request;
/// the runtime declines tools not in the grant list at the engine boundary.
/// </summary>
public sealed record AgentRunRequest(
    RunId RunId,
    AssembledPrompt Prompt,
    Budgets Budgets,
    IReadOnlyList<ToolGrant> ToolGrants,
    string? PreferredModel);
