using Loom.Application.Abstractions;
using Loom.Application.Workflows.Kickoff;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loom.Infrastructure.Bootstrap;

public sealed class BootstrapOptions
{
    public const string SectionName = "Loom:Bootstrap";

    /// <summary>Master switch. Default: true in Development, false otherwise.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Apply pending EF migrations on startup.</summary>
    public bool ApplyMigrations { get; set; } = true;

    /// <summary>Seed the kickoff workflow (kickoff/v1) if not already present.</summary>
    public bool SeedKickoffWorkflow { get; set; } = true;

    /// <summary>Seed the global bootstrap fragments if not already present.</summary>
    public bool SeedFragments { get; set; } = true;
}

/// <summary>
/// Idempotent first-run setup. On startup:
///   1. Optionally apply pending EF migrations.
///   2. Seed the kickoff/v1 workflow if missing.
///   3. Seed the global bootstrap fragments if missing.
/// Each step is a no-op if the data already exists, so this can run on
/// every boot without producing duplicates. Phase 5+ will gate it behind
/// configuration in production deployments.
/// </summary>
public sealed class LoomBootstrapper(
    IServiceScopeFactory scopeFactory,
    IOptions<BootstrapOptions> options,
    ISystemClock clock,
    ILogger<LoomBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            BootstrapperLog.Disabled(logger);
            return;
        }

        BootstrapperLog.Starting(logger);
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<LoomDbContext>();

        if (opts.ApplyMigrations)
        {
            try
            {
                // SQLite is the no-install dev provider; its schema is built
                // from the model directly because the SQL-Server-shaped
                // migrations don't run on it.
                var provider = sp.GetService<LoomDatabaseProvider>();
                if (provider?.Kind == DatabaseProviderKind.Sqlite)
                {
                    await db.Database.EnsureCreatedAsync(cancellationToken);
                }
                else
                {
                    await db.Database.MigrateAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                BootstrapperLog.MigrateFailed(logger, ex);
                throw;
            }
        }

        if (opts.SeedFragments)
        {
            await SeedBootstrapFragmentsAsync(sp, cancellationToken);
        }

        if (opts.SeedKickoffWorkflow)
        {
            await SeedKickoffWorkflowAsync(sp, cancellationToken);
        }

        BootstrapperLog.Done(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedBootstrapFragmentsAsync(IServiceProvider sp, CancellationToken ct)
    {
        var fragments = sp.GetRequiredService<IFragmentRepository>();
        var uow = sp.GetRequiredService<IUnitOfWork>();
        var now = clock.UtcNow;

        var added = 0;
        foreach (var seed in Application.Workflows.Kickoff.SeedFragments.All)
        {
            var existing = await fragments.GetByKeyAsync(seed.Key.Value, FragmentScope.Global, scopeId: null, ct);
            if (existing is not null)
            {
                continue;
            }

            var fragment = Fragment.Create(
                seed.Key,
                seed.Category,
                FragmentScope.Global,
                scopeId: null,
                title: seed.Title,
                ownerId: Application.Workflows.Kickoff.SeedFragments.SystemOwnerId,
                now: now);
            fragment.PublishVersion(
                content: seed.Content,
                hints: Application.Workflows.Kickoff.SeedFragments.DefaultHints,
                changeNote: "Bootstrap seed",
                authorId: Application.Workflows.Kickoff.SeedFragments.SystemOwnerId,
                now: now);
            await fragments.AddAsync(fragment, ct);
            added++;
        }

        if (added > 0)
        {
            await uow.SaveChangesAsync(ct);
            BootstrapperLog.SeededFragments(logger, added);
        }
    }

    private async Task SeedKickoffWorkflowAsync(IServiceProvider sp, CancellationToken ct)
    {
        var workflows = sp.GetRequiredService<IWorkflowRepository>();
        var uow = sp.GetRequiredService<IUnitOfWork>();

        // Each seed is independent — earlier versions short-circuited as soon
        // as the kickoff row existed, which left enrichment / wireframing
        // unseeded on any subsequent restart.
        await SeedIfMissingAsync(workflows, uow,
            KickoffWorkflowFactory.WorkflowKey,
            KickoffWorkflowFactory.CurrentVersion,
            () => KickoffWorkflowFactory.Build(clock.UtcNow),
            ct);

        await SeedIfMissingAsync(workflows, uow,
            Application.Workflows.Kickoff.KickoffMultiWorkflowFactory.WorkflowKey,
            Application.Workflows.Kickoff.KickoffMultiWorkflowFactory.CurrentVersion,
            () => Application.Workflows.Kickoff.KickoffMultiWorkflowFactory.Build(clock.UtcNow),
            ct);

        await SeedIfMissingAsync(workflows, uow,
            Application.Workflows.Enrichment.EnrichmentWorkflowFactory.WorkflowKey,
            Application.Workflows.Enrichment.EnrichmentWorkflowFactory.CurrentVersion,
            () => Application.Workflows.Enrichment.EnrichmentWorkflowFactory.Build(clock.UtcNow),
            ct);

        await SeedIfMissingAsync(workflows, uow,
            Application.Workflows.Wireframing.WireframingWorkflowFactory.WorkflowKey,
            Application.Workflows.Wireframing.WireframingWorkflowFactory.CurrentVersion,
            () => Application.Workflows.Wireframing.WireframingWorkflowFactory.Build(clock.UtcNow),
            ct);

        await SeedIfMissingAsync(workflows, uow,
            Application.Workflows.Engineering.SliceDesignWorkflowFactory.WorkflowKey,
            Application.Workflows.Engineering.SliceDesignWorkflowFactory.CurrentVersion,
            () => Application.Workflows.Engineering.SliceDesignWorkflowFactory.Build(clock.UtcNow),
            ct);

        await SeedIfMissingAsync(workflows, uow,
            Application.Workflows.Engineering.BuildWorkflowFactory.WorkflowKey,
            Application.Workflows.Engineering.BuildWorkflowFactory.CurrentVersion,
            () => Application.Workflows.Engineering.BuildWorkflowFactory.Build(clock.UtcNow),
            ct);

        await SeedIfMissingAsync(workflows, uow,
            Application.Workflows.Engineering.TestPlanWorkflowFactory.WorkflowKey,
            Application.Workflows.Engineering.TestPlanWorkflowFactory.CurrentVersion,
            () => Application.Workflows.Engineering.TestPlanWorkflowFactory.Build(clock.UtcNow),
            ct);
    }

    private async Task SeedIfMissingAsync(
        IWorkflowRepository workflows,
        IUnitOfWork uow,
        string key,
        int version,
        Func<Loom.Domain.Workflows.Workflow> factory,
        CancellationToken ct)
    {
        var existing = await workflows.GetByKeyAsync(Slug.From(key), version, ct);
        if (existing is not null)
        {
            return;
        }
        var workflow = factory();
        await workflows.AddAsync(workflow, ct);
        await uow.SaveChangesAsync(ct);
        BootstrapperLog.SeededWorkflow(logger, key, version);
    }
}

internal static partial class BootstrapperLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Loom bootstrap starting")]
    public static partial void Starting(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Loom bootstrap done")]
    public static partial void Done(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Loom bootstrap disabled by configuration")]
    public static partial void Disabled(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Seeded {Count} bootstrap fragment(s)")]
    public static partial void SeededFragments(ILogger logger, int count);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Seeded workflow '{Key}' v{Version}")]
    public static partial void SeededWorkflow(ILogger logger, string key, int version);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "EF migration during bootstrap failed")]
    public static partial void MigrateFailed(ILogger logger, Exception ex);
}
