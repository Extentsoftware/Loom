namespace Loom.Application.Agents;

/// <summary>
/// Capabilities an agent runtime advertises so the router can match a step's
/// requirements to a runtime that can actually run it. Phase 1 only consumes
/// this for sanity checks (one runtime registered); Phase 5's router uses it
/// for real engine selection.
/// </summary>
public sealed record EngineCapabilities(
    bool SupportsStreaming,
    bool SupportsToolUse,
    bool SupportsExtendedThinking,
    bool SupportsStructuredOutput,
    int MaxContextTokens);
