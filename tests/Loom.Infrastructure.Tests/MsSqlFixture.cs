using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Loom.Infrastructure.Tests;

/// <summary>
/// Shared MSSQL container fixture for all integration tests in this assembly.
/// One container per test run — schema is migrated once, individual tests use
/// transactions and per-test isolation via separate scopes / cleanups.
/// </summary>
public sealed class MsSqlFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Loom_Test_Pa55word!")
        .Build();

    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Apply the schema once for the whole test run.
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public Loom.Infrastructure.Persistence.LoomDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Loom.Infrastructure.Persistence.LoomDbContext>()
            .UseSqlServer(ConnectionString, sql =>
            {
                sql.MigrationsHistoryTable("__ef_migrations_history", "loom");
            })
            .Options;

        return new Loom.Infrastructure.Persistence.LoomDbContext(options);
    }
}

[CollectionDefinition(nameof(MsSqlCollection))]
public sealed class MsSqlCollection : ICollectionFixture<MsSqlFixture>
{
}
