using Loom.Domain.Common;

namespace Loom.Domain.Runs;

public readonly record struct AssembledPromptId(Guid Value) : IEntityId
{
    public static AssembledPromptId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// One message in an assembled prompt. Roles follow Anthropic conventions
/// (system/user/assistant); tool-result messages will arrive as a separate
/// kind in a later phase if we need them outside the SDK abstraction.
/// </summary>
public sealed record AssembledPromptMessage(string Role, string Content);

/// <summary>
/// The frozen prompt that was sent (or will be sent) to an engine for a Run.
/// AssembledPrompt is immutable once created — provenance for the resulting
/// artifact depends on it. The Fragments list captures the exact (FragmentId,
/// FragmentVersionId, Version) trio used to produce SystemPrompt, so that a
/// later replay can re-compose with the same versions.
/// </summary>
public sealed class AssembledPrompt
{
    private readonly List<AssembledPromptMessage> _messages = [];
    private readonly List<FragmentRef> _fragments = [];

    private AssembledPrompt() { } // EF Core

    private AssembledPrompt(
        AssembledPromptId id,
        RunId runId,
        string systemPrompt,
        string? outputSchemaJson,
        DateTimeOffset assembledAt)
    {
        Id = id;
        RunId = runId;
        SystemPrompt = systemPrompt;
        OutputSchemaJson = outputSchemaJson;
        AssembledAt = assembledAt;
    }

    public AssembledPromptId Id { get; private set; }
    public RunId RunId { get; private set; }
    public string SystemPrompt { get; private set; } = null!;
    public string? OutputSchemaJson { get; private set; }
    public DateTimeOffset AssembledAt { get; private set; }

    public IReadOnlyList<AssembledPromptMessage> Messages => _messages.AsReadOnly();
    public IReadOnlyList<FragmentRef> Fragments => _fragments.AsReadOnly();

    public static AssembledPrompt Create(
        RunId runId,
        string systemPrompt,
        IEnumerable<AssembledPromptMessage> messages,
        IEnumerable<FragmentRef> fragments,
        string? outputSchemaJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(fragments);

        var p = new AssembledPrompt(
            AssembledPromptId.New(),
            runId,
            systemPrompt,
            string.IsNullOrWhiteSpace(outputSchemaJson) ? null : outputSchemaJson,
            now);
        p._messages.AddRange(messages);
        p._fragments.AddRange(fragments);

        if (p._messages.Count == 0)
        {
            throw new DomainException("An assembled prompt must contain at least one message.");
        }
        return p;
    }
}
