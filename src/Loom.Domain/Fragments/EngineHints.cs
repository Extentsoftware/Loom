namespace Loom.Domain.Fragments;

/// <summary>
/// Metadata that helps the agent router pick a suitable engine for a step
/// that includes this fragment. None of these are mandatory — engines treat
/// hints as preferences, not requirements. Hints are model-aware metadata,
/// kept *outside* the fragment text so the content stays portable.
/// </summary>
public sealed record EngineHints(
    bool PrefersExtendedThinking = false,
    int? MaxContextTokens = null,
    bool RequiresJsonOutput = false,
    bool RequiresFilesystem = false,
    string? PreferredModelHint = null);
