using Loom.Domain.Common;

namespace Loom.Domain.Runs;

public readonly record struct TranscriptId(Guid Value) : IEntityId
{
    public static TranscriptId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// The raw text a PO pasted into the kickoff page, plus the normalized JSON
/// produced by the in-proc normalize step. Kept separate from the FeatureNode
/// because transcripts can be large and are mostly write-once. Linked to the
/// kickoff Run by RunId so provenance is intact.
/// </summary>
public sealed class Transcript
{
    private Transcript() { } // EF Core

    private Transcript(
        TranscriptId id,
        RunId runId,
        string rawText,
        DateTimeOffset createdAt)
    {
        Id = id;
        RunId = runId;
        RawText = rawText;
        CreatedAt = createdAt;
    }

    public TranscriptId Id { get; private set; }
    public RunId RunId { get; private set; }
    public string RawText { get; private set; } = null!;
    public string? NormalizedJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Transcript Create(RunId runId, string rawText, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawText);
        return new Transcript(TranscriptId.New(), runId, rawText, now);
    }

    /// <summary>
    /// Attach the JSON output of the normalize step. Set once; subsequent
    /// attempts overwrite (the normalize step is idempotent on a given
    /// transcript so re-runs land the same content).
    /// </summary>
    public void SetNormalized(string normalizedJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedJson);
        NormalizedJson = normalizedJson;
    }
}
