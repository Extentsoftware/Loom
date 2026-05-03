using Loom.Application.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Agents.Anthropic;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Anthropic runtime: options bound to the "Anthropic"
    /// section, an HttpClient configured with sensible timeouts, the chat
    /// client seam, and the IAgentRuntime implementation. The runtime is
    /// added as IAgentRuntime (multi-registration) so the DefaultAgentRouter
    /// in Loom.Application sees it.
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

        services.AddSingleton<IAgentRuntime, AnthropicAgentRuntime>();
        return services;
    }
}
