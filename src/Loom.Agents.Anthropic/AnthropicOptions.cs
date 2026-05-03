namespace Loom.Agents.Anthropic;

/// <summary>
/// Configuration for the Anthropic runtime. Bound from the configuration
/// section "Anthropic". The API key is REQUIRED — Loom.Web validates it
/// at startup and refuses to boot without one (set LOOM_ANTHROPIC_API_KEY
/// or Anthropic:ApiKey in user-secrets / Key Vault).
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>API key. No default — must be configured.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Default model used when a step doesn't override.</summary>
    public string DefaultModel { get; set; } = "claude-opus-4-7";

    /// <summary>Optional override for the API base URL.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Whether to enable extended thinking by default. Per-step
    /// EngineHints can still override this either way.</summary>
    public bool ExtendedThinking { get; set; }
}
