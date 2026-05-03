using Loom.Domain.Nodes;

namespace Loom.Application.Workflows;

/// <summary>
/// Snapshot of the live node fields and ancestor chain that's fed into prompt
/// assembly as the auto-injected "context" fragment. Pure data — the composer
/// renders it into the system prompt; nothing here mutates.
/// </summary>
public sealed record NodeContext(
    NodeId NodeId,
    string Title,
    string? Intent,
    NodePhase Phase,
    NodeType Type,
    IReadOnlyList<string> AncestorTitles,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<Outcome> Outcomes,
    IReadOnlyList<Hypothesis> Hypotheses);
