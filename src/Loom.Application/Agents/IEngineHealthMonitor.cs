using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// Tracks recent successes and failures per engine to inform routing
/// decisions. Phase-5 ships an in-memory rolling window; a Phase-6+
/// implementation can persist to MSSQL or Redis to share state across
/// web instances. The seam is the same.
/// </summary>
public interface IEngineHealthMonitor
{
    /// <summary>
    /// Returns the engine's current health snapshot. <c>IsHealthy</c> is
    /// true unless the recent failure rate exceeds the configured threshold.
    /// </summary>
    EngineHealth Snapshot(EngineName engine);

    void RecordSuccess(EngineName engine);
    void RecordFailure(EngineName engine);

    /// <summary>
    /// Track an in-flight call; dispose the returned token when the call
    /// settles. The router uses live in-flight counts as a tiebreaker
    /// between equally-healthy engines.
    /// </summary>
    IDisposable BeginInFlight(EngineName engine);

    IReadOnlyDictionary<EngineName, EngineHealth> All();
}

public sealed record EngineHealth(
    EngineName Engine,
    int RecentSuccess,
    int RecentFailure,
    int InFlight,
    bool IsHealthy);
