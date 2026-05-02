using Loom.Domain.Fragments;

namespace Loom.Domain.Runs;

/// <summary>
/// Concrete fragment version pinned into a run. Captures both the fragment id
/// and the specific version id so provenance survives later edits to the
/// fragment library.
/// </summary>
public sealed record FragmentRef(FragmentId FragmentId, FragmentVersionId VersionId, int Version);

/// <summary>
/// Caps imposed on a run by the workflow step. Engines must respect these;
/// the run watchdog will cancel a run that exceeds them.
/// </summary>
public sealed record Budgets(
    int? MaxInputTokens,
    int? MaxOutputTokens,
    TimeSpan? MaxWallClock,
    decimal? MaxCostUsd);

/// <summary>
/// Cost actually consumed by a run. Both the model name and the deployment
/// are captured because Foundry billing is per-deployment; we'll want this
/// when comparing model performance later.
/// </summary>
public sealed record Cost(
    string Model,
    string? Deployment,
    int InputTokens,
    int OutputTokens,
    decimal UsdAmount);

/// <summary>
/// Permission grant for a tool the run is allowed to call. Tools without a
/// matching grant are refused at the engine boundary.
/// </summary>
public sealed record ToolGrant(string ToolName, IReadOnlyList<string>? AllowedArguments = null);
