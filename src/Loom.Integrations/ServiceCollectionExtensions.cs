using Loom.Integrations.AzureDevOps;
using Loom.Integrations.GitSync;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Integrations;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Phase-2 integration adapters: GitSync + ADO. Webhook
    /// intake controllers live in Loom.Web; only the validator + parsers
    /// live here, registered as singletons since they're stateless.
    /// </summary>
    public static IServiceCollection AddLoomIntegrations(this IServiceCollection services)
    {
        services.AddScoped<IGitSyncService, LibGit2GitSyncService>();
        services.AddHttpClient<IAdoAdapter, AdoAdapter>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
