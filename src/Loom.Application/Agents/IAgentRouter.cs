using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// Selects an IAgentRuntime for a workflow step. Phase 1: trivial — returns
/// the only registered runtime if its EngineName matches the step's
/// EnginePref. Phase 5 swaps in a real router with health, capacity, and
/// budget gating. The seam stays the same so callers don't churn.
/// </summary>
public interface IAgentRouter
{
    IAgentRuntime Resolve(EngineName preferred);
}

/// <summary>
/// Phase-1 router: registered runtimes by EngineName, picks the matching one,
/// throws if no runtime claims the requested engine. Designed so registering
/// only AnthropicAgentRuntime in DI is sufficient for Phase 1's kickoff.
/// </summary>
public sealed class DefaultAgentRouter(IEnumerable<IAgentRuntime> runtimes) : IAgentRouter
{
    private readonly Dictionary<EngineName, IAgentRuntime> _byEngine =
        runtimes.ToDictionary(r => r.Engine);

    public IAgentRuntime Resolve(EngineName preferred)
    {
        if (_byEngine.TryGetValue(preferred, out var runtime))
        {
            return runtime;
        }
        throw new InvalidOperationException(
            $"No IAgentRuntime is registered for engine '{preferred}'. Registered: {string.Join(", ", _byEngine.Keys)}.");
    }
}
