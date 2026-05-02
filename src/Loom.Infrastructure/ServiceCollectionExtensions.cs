using Loom.Application.Abstractions;
using Loom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Loom infrastructure services: DbContext, repositories, clock.
    /// </summary>
    public static IServiceCollection AddLoomInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<LoomDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__ef_migrations_history", "loom");
            });
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LoomDbContext>());
        services.AddScoped<IFeatureNodeRepository, FeatureNodeRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IFragmentRepository, FragmentRepository>();
        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddSingleton<ISystemClock, SystemClock>();

        return services;
    }
}

internal sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
