namespace Loom.Application.Artifacts;

/// <summary>
/// In-memory shape of an uploaded blob. Each <see cref="ArtifactFile"/> is
/// one logical file; an upload may carry multiple files (e.g. a Figma
/// frame export bundled with its preview PNG). The bundle is the unit
/// MessagePack-serialized into the blob store row.
/// </summary>
public sealed record ArtifactBundle(IReadOnlyList<ArtifactFile> Files);

public sealed record ArtifactFile(
    string Filename,
    string ContentType,
    byte[] Bytes);
