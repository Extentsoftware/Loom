using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Loom.Web.Tests;

/// <summary>
/// Boots the Loom.Web host with a per-factory SQLite database (temp file)
/// and Entra disabled, so the host can stand up without external
/// dependencies. Use for routing, layout, and basic page-render tests.
///
/// Note: SQLite enforces most relational constraints, but a few SQL-Server-
/// specific schema behaviours (filtered indexes, computed columns, certain
/// cascade shapes) are not exercised here. Tests that need SQL-Server-shape
/// behaviour belong in Loom.Infrastructure.Tests with Testcontainers.
/// </summary>
public sealed class LoomWebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"loom-test-{Guid.NewGuid():N}.db");

    public LoomWebFactory()
    {
        // Program.cs reads LOOM_CONNECTION eagerly before WAF gets a chance
        // to inject ConfigureAppConfiguration overrides, so any value from
        // appsettings.Development.json (SQL Server) wins by default. Setting
        // the env var here forces the host onto the SQLite test DB before
        // Program runs.
        Environment.SetEnvironmentVariable("LOOM_CONNECTION", $"Data Source={_dbPath}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Enabled"] = "false",
                ["ConnectionStrings:Loom"] = $"Data Source={_dbPath}",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
