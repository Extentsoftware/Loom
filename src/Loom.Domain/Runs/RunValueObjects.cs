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
///
/// Declared as a class (not a record) because this is mapped as an inline
/// owned type on Run; EF Core 10's change tracker re-evaluates record
/// owned references by value-equality on each SaveChanges and trips the
/// "Budgets#RunId is part of a key" error on subsequent updates within
/// one tracked scope. Reference-identity tracking on a class avoids that.
/// </summary>
public sealed class Budgets(
    int? MaxInputTokens,
    int? MaxOutputTokens,
    TimeSpan? MaxWallClock,
    decimal? MaxCostUsd)
{
    public int? MaxInputTokens { get; } = MaxInputTokens;
    public int? MaxOutputTokens { get; } = MaxOutputTokens;
    public TimeSpan? MaxWallClock { get; } = MaxWallClock;
    public decimal? MaxCostUsd { get; } = MaxCostUsd;
}

/// <summary>
/// Cost actually consumed by a run. Both the model name and the deployment
/// are captured because Foundry billing is per-deployment; we'll want this
/// when comparing model performance later.
///
/// Same record-vs-class rationale as <see cref="Budgets"/>.
/// </summary>
public sealed class Cost(
    string Model,
    string? Deployment,
    int InputTokens,
    int OutputTokens,
    decimal UsdAmount)
{
    public string Model { get; } = Model;
    public string? Deployment { get; } = Deployment;
    public int InputTokens { get; } = InputTokens;
    public int OutputTokens { get; } = OutputTokens;
    public decimal UsdAmount { get; } = UsdAmount;
}

/// <summary>
/// Permission grant for a tool the run is allowed to call. Tools without a
/// matching grant are refused at the engine boundary.
/// </summary>
public sealed record ToolGrant(string ToolName, IReadOnlyList<string>? AllowedArguments = null);
