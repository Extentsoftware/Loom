using Loom.Application.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Agents.Anthropic;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Anthropic runtime: options bound to the "Anthropic"
    /// section, an HttpClient configured with sensible timeouts, and the
    /// chat client seam. The IAgentRuntime registration is **conditional**
    /// on Anthropic:ApiKey being configured — without a key, the runtime
    /// is silently absent from the multi-engine router. A workflow that
    /// pins EngineName.Anthropic on a host with no key fails fast at the
    /// router with a clear "no IAgentRuntime is registered" message,
    /// rather than throwing later from inside the chat client. Foundry
    /// (ADR-0017) is the default; Anthropic is opt-in via configuration.
    /// </summary>
    public static IServiceCollection AddAnthropicAgentRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AnthropicOptions>()
            .Bind(configuration.GetSection(AnthropicOptions.SectionName))
            .ValidateOnStart();

        // The Anthropic streaming endpoint needs a long timeout — model
        // generation can run for tens of seconds. Per-call wall-clock is
        // enforced by the runtime's linked CTS.
        services.AddHttpClient<IAnthropicChatClient, AnthropicChatClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        var apiKey = configuration.GetSection(AnthropicOptions.SectionName)["ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<IAgentRuntime, AnthropicAgentRuntime>();
        }

        return services;
    }
}
