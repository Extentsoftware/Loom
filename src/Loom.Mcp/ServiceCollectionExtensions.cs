using Microsoft.Extensions.DependencyInjection;

namespace Loom.Mcp;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Loom's MCP server (in-process inside the Loom.Web host)
    /// with the Phase-2 read tools: get_node_context, search_nodes,
    /// list_rules. Tools are discovered by attribute scan over this
    /// assembly. The host wires the HTTP/SSE transport via MapMcp.
    /// </summary>
    public static IServiceCollection AddLoomMcpServer(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        return services;
    }
}
