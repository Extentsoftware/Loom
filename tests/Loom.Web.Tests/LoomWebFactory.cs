using Loom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Web.Tests;

/// <summary>
/// Boots the Loom.Web host with the DbContext swapped for an in-memory
/// provider and Entra disabled, so the host can stand up without external
/// dependencies. Use for routing, layout, and basic page-render tests.
///
/// Note: in-memory provider does not enforce relational constraints
/// (unique indexes, cascade behaviour). Tests that need real schema
/// behaviour belong in Loom.Infrastructure.Tests with Testcontainers.
/// </summary>
public sealed class LoomWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Enabled"] = "false",
                ["ConnectionStrings:Loom"] = "Server=ignored;Database=ignored;",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the SQL-backed DbContext with in-memory.
            var dbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<LoomDbContext>) ||
                            d.ServiceType == typeof(LoomDbContext))
                .ToList();
            foreach (var d in dbContextDescriptors)
            {
                services.Remove(d);
            }

            services.AddDbContext<LoomDbContext>(options =>
                options.UseInMemoryDatabase($"loom-test-{Guid.NewGuid():N}"));
        });
    }
}
