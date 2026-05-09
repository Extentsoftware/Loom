namespace Loom.Application.Artifacts;

/// <summary>
/// Limits on file uploads attached as project seed artifacts (ADR-0018).
/// Defaults aim at "screenshots + a Figma frame" rather than full design
/// archives; deployments can raise them in configuration.
/// </summary>
public sealed class ProjectArtifactOptions
{
    public const string SectionName = "Loom:Artifacts";

    /// <summary>Maximum bytes per single file inside a bundle.</summary>
    public long MaxFileBytes { get; set; } = 25L * 1024 * 1024; // 25 MiB

    /// <summary>Maximum bytes for the whole bundle (sum of files).</summary>
    public long MaxBundleBytes { get; set; } = 100L * 1024 * 1024; // 100 MiB
}
