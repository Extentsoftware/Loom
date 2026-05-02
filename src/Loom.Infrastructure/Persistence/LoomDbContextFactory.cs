using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loom.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> tooling instantiate the context without the full
/// host pipeline. Used only at design time (migrations, scaffolding).
///
/// The connection string here is a placeholder — migrations are produced
/// against a relational provider, not actually executed. Override via the
/// <c>LOOM_DESIGN_TIME_CONNECTION</c> environment variable when needed.
/// </summary>
public sealed class LoomDbContextFactory : IDesignTimeDbContextFactory<LoomDbContext>
{
    private const string DefaultConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=loom_design_time;Trusted_Connection=True;TrustServerCertificate=True";

    public LoomDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("LOOM_DESIGN_TIME_CONNECTION")
            ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<LoomDbContext>()
            .UseSqlServer(connection, sql =>
            {
                sql.MigrationsHistoryTable("__ef_migrations_history", "loom");
            })
            .Options;

        return new LoomDbContext(options);
    }
}
