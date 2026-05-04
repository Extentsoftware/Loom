namespace Loom.Agents.Foundry;

/// <summary>
/// Configuration for the Foundry runtime. Bound from the configuration
/// section "Foundry". The runtime targets Azure OpenAI Chat Completions
/// (Surface A — see ADR-0017): the deployment determines the model and
/// is also reported on Cost.Deployment for per-deployment billing.
///
/// Endpoint + Deployment + ApiVersion + ApiKey are required; missing any
/// of them fails fast at first call. Configure via user-secrets or
/// LOOM_FOUNDRY_* env vars in dev; Key Vault references in non-dev.
/// </summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>Resource base URL, e.g. https://artio-dev-fr-foundry.openai.azure.com.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Azure OpenAI deployment name. Determines the model; also
    /// reported on Cost.Deployment.</summary>
    public string Deployment { get; set; } = string.Empty;

    /// <summary>Azure OpenAI REST API version, e.g. 2024-02-01.</summary>
    public string ApiVersion { get; set; } = "2024-02-01";

    /// <summary>API key. No default — must be configured.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Friendly model identity behind the deployment, used for
    /// Cost.Model when the streaming response doesn't surface one.
    /// Streaming usually reports a normalised model id which overrides
    /// this value.</summary>
    public string DefaultModel { get; set; } = string.Empty;
}
