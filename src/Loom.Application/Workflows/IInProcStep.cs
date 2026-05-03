namespace Loom.Application.Workflows;

/// <summary>
/// A workflow step that executes in-process without an LLM. Phase 1 ships
/// only TranscriptNormalizeStep (in Loom.Agents.InProc). Resolved by step Key.
/// </summary>
public interface IInProcStep
{
    string StepKey { get; }
    Task<string> ExecuteAsync(IReadOnlyDictionary<string, string> inputs, CancellationToken ct = default);
}
