using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// Selects an IAgentRuntime for a workflow step. Phase 1 was trivial — a
/// single registered runtime resolved by name. Phase 5 layers on:
///   • engine preference order: caller passes a list, router walks it.
///   • health gate: skip engines with elevated recent failure rate.
///
/// The Phase-1 single-engine call still works (Resolve(EngineName)) for
/// callers that don't care about fallback.
/// </summary>
public interface IAgentRouter
{
    IAgentRuntime Resolve(EngineName preferred);

    /// <summary>
    /// Walk the preference list, returning the first registered + healthy
    /// runtime. Throws if none of the preferred engines have a registered
    /// runtime; if all registered runtimes are unhealthy, falls back to
    /// the first registered preferred engine (better to try than to fail
    /// outright; the runtime may still succeed).
    /// </summary>
    IAgentRuntime ResolveWithFallback(IReadOnlyList<EngineName> preferences);
}

public sealed class DefaultAgentRouter(
    IEnumerable<IAgentRuntime> runtimes,
    IEngineHealthMonitor? health = null) : IAgentRouter
{
    private readonly Dictionary<EngineName, IAgentRuntime> _byEngine =
        runtimes.ToDictionary(r => r.Engine);
    private readonly IEngineHealthMonitor? _health = health;

    public IAgentRuntime Resolve(EngineName preferred)
    {
        if (_byEngine.TryGetValue(preferred, out var runtime))
        {
            return runtime;
        }
        throw new InvalidOperationException(
            $"No IAgentRuntime is registered for engine '{preferred}'. Registered: {string.Join(", ", _byEngine.Keys)}.");
    }

    public IAgentRuntime ResolveWithFallback(IReadOnlyList<EngineName> preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.Count == 0)
        {
            throw new ArgumentException("At least one engine preference is required.", nameof(preferences));
        }

        IAgentRuntime? lastRegistered = null;
        foreach (var pref in preferences)
        {
            if (!_byEngine.TryGetValue(pref, out var runtime))
            {
                continue;
            }
            lastRegistered = runtime;
            if (_health is null || _health.Snapshot(pref).IsHealthy)
            {
                return runtime;
            }
        }

        if (lastRegistered is not null)
        {
            // Every preferred engine is registered but unhealthy. Try the
            // first registered one anyway — recent history is only a hint,
            // and reporting "no engine available" would be worse for the
            // user than letting the engine make its own decision.
            return lastRegistered;
        }

        throw new InvalidOperationException(
            $"None of the requested engines are registered. Wanted: {string.Join(", ", preferences)}; have: {string.Join(", ", _byEngine.Keys)}.");
    }
}
