using Loom.Application.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Agents.Foundry;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Foundry runtime: options bound to the "Foundry" section,
    /// an HttpClient configured with sensible timeouts, the chat client seam,
    /// and the IAgentRuntime implementation. The runtime is added as
    /// IAgentRuntime (multi-registration) so the DefaultAgentRouter in
    /// Loom.Application sees it alongside any other registered runtimes.
    /// </summary>
    public static IServiceCollection AddFoundryAgentRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<FoundryOptions>()
            .Bind(configuration.GetSection(FoundryOptions.SectionName))
            .ValidateOnStart();

        // Azure OpenAI streaming can run for tens of seconds. Per-call wall-
        // clock is enforced by the runtime's linked CTS.
        services.AddHttpClient<IFoundryChatClient, FoundryChatClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddSingleton<IAgentRuntime, FoundryAgentRuntime>();
        return services;
    }
}
