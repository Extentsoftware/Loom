using System.Collections.Concurrent;
using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// In-memory engine health monitor. Tracks the last <see
/// cref="WindowSize"/> outcomes per engine and reports unhealthy when the
/// failure rate within the window exceeds <see cref="FailureThreshold"/>.
/// Phase-5 keeps this single-instance; the router degrades gracefully to
/// "all engines healthy" if no monitor is registered (the default
/// snapshot returns IsHealthy=true).
/// </summary>
public sealed class InMemoryEngineHealthMonitor : IEngineHealthMonitor
{
    private const int WindowSize = 20;
    private const double FailureThreshold = 0.5;

    private readonly ConcurrentDictionary<EngineName, EngineWindow> _windows = new();

    public EngineHealth Snapshot(EngineName engine)
    {
        if (!_windows.TryGetValue(engine, out var w))
        {
            return new EngineHealth(engine, 0, 0, IsHealthy: true);
        }
        var (success, failure) = w.Counts();
        var total = success + failure;
        var unhealthy = total >= 5 && (double)failure / total > FailureThreshold;
        return new EngineHealth(engine, success, failure, !unhealthy);
    }

    public void RecordSuccess(EngineName engine) =>
        _windows.GetOrAdd(engine, _ => new EngineWindow()).Record(true);

    public void RecordFailure(EngineName engine) =>
        _windows.GetOrAdd(engine, _ => new EngineWindow()).Record(false);

    public IReadOnlyDictionary<EngineName, EngineHealth> All() =>
        _windows.Keys.ToDictionary(k => k, Snapshot);

    private sealed class EngineWindow
    {
        private readonly bool[] _slots = new bool[WindowSize];
        private readonly object _lock = new();
        private int _next;
        private int _filled;

        public void Record(bool success)
        {
            lock (_lock)
            {
                _slots[_next] = success;
                _next = (_next + 1) % WindowSize;
                if (_filled < WindowSize) _filled++;
            }
        }

        public (int Success, int Failure) Counts()
        {
            lock (_lock)
            {
                int s = 0, f = 0;
                for (var i = 0; i < _filled; i++)
                {
                    if (_slots[i]) s++; else f++;
                }
                return (s, f);
            }
        }
    }
}
