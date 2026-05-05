using System.Reflection;
using Loom.Application.Abstractions;
using Loom.Application.Agents;
using Loom.Application.Artifacts;
using Loom.Application.BudgetControl;
using Loom.Application.Common;
using Loom.Application.Conversations;
using Loom.Application.Features;
using Loom.Application.Memory;
using Loom.Application.Fragments;
using Loom.Application.Notifications;
using Loom.Application.Runs;
using Loom.Application.Workflows;
using Loom.Application.Workflows.Enrichment;
using Loom.Domain.Common;
using Loom.Infrastructure.Bootstrap;
using Loom.Infrastructure.Outbox;
using Loom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        // Provider auto-detect: SQLite connection strings start with
        // "Data Source=" and don't contain "Server=". Anything else is
        // SQL Server. SQLite is the no-install dev path; production stays
        // SQL Server.
        var isSqlite = connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<LoomDbContext>(options =>
        {
            if (isSqlite)
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsHistoryTable("__ef_migrations_history", "loom");
                });
            }
        });
        services.AddSingleton(new LoomDatabaseProvider(isSqlite ? DatabaseProviderKind.Sqlite : DatabaseProviderKind.SqlServer));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LoomDbContext>());
        services.AddScoped<IFeatureNodeRepository, FeatureNodeRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IFragmentRepository, FragmentRepository>();
        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IRunEventRepository, RunEventRepository>();
        services.AddScoped<IAssembledPromptRepository, AssembledPromptRepository>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<IIntegrationConnectionRepository, IntegrationConnectionRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IAnnotationRepository, AnnotationRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IProjectBudgetRepository, ProjectBudgetRepository>();
        // Note: IOutboxWriter has no DI registration — the LoomDbContext
        // performs the outbox flush inline inside its overridden
        // SaveChangesAsync. Registering the writer here would create a
        // DbContext ↔ Writer construction cycle. OutboxWriter still exists
        // for round-trip tests that drive the persistence shape directly.
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddSingleton<ISystemClock, SystemClock>();

        // Application services. These have no infrastructure dependencies
        // beyond the repositories declared above; they live in Loom.Application
        // but the composition root for them is here, alongside the repos that
        // back them.
        services.AddScoped<IFeatureService, FeatureService>();
        services.AddScoped<IFragmentService, FragmentService>();
        services.AddScoped<IRunService, RunService>();
        services.AddScoped<Loom.Application.Runs.IRunAssignmentService, Loom.Application.Runs.RunAssignmentService>();
        services.AddScoped<IAssembledPromptComposer, AssembledPromptComposer>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IAgentRouter, DefaultAgentRouter>();

        // Phase-3 services.
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationFeed, NotificationFeed>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IArtifactService, ArtifactService>();

        // Phase-5: budget control + engine health monitor.
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddSingleton<IEngineHealthMonitor, InMemoryEngineHealthMonitor>();

        // Phase-6a: memory search seam (SQL-backed). Phase-6b will swap in
        // an Elasticsearch implementation behind the same interface. The
        // memory-lookup in-proc step is registered alongside the other
        // IInProcStep implementations the WorkflowEngine resolves by key.
        services.AddScoped<IMemorySearch, SqlMemorySearch>();
        services.AddScoped<IInProcStep, MemoryLookupStep>();

        // Notification channels — InApp lives in Loom.Web (registered
        // there). Teams uses an Incoming Webhook (real HTTP); Email is
        // still a stub pending SMTP/Graph wiring.
        services.AddOptions<TeamsWebhookOptions>()
            .BindConfiguration(TeamsWebhookOptions.SectionName);
        services.AddHttpClient<TeamsWebhookChannel>();
        // The typed-client registration above adds TeamsWebhookChannel as
        // transient; resolve through it so each notification gets a fresh
        // HttpClient backed by the IHttpClientFactory pool.
        services.AddScoped<INotificationChannel>(sp => sp.GetRequiredService<TeamsWebhookChannel>());
        services.AddScoped<INotificationChannel, EmailChannelStub>();

        // Outbox handlers that bridge domain events into NotificationService.
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.NodeUpdated>, NodeUpdatedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.RunPausedForHuman>, RunPausedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.RunCompleted>, RunCompletedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.RunFailed>, RunFailedNotificationHandler>();

        // Auto-queue enrichment workflow once the PO has committed the
        // decompose acceptance (NOT per-NodeCreated — that shape raced
        // with the user's accept loop and caused row-lock contention).
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.KickoffDecomposeAccepted>, EnrichmentAutoQueueHandler>();

        // Queue an enrichment run when a node is advanced into the Enrich
        // phase from the workspace's "Advance to enrich" button. Idempotent —
        // skips if an active enrichment run already exists on the node.
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.NodePhaseAdvanced>, EnrichmentPhaseAdvanceHandler>();

        // Step-output projectors. Each is registered as IStepOutputProjector;
        // the WorkflowEngine resolves them by OutputSchemaName.
        services.AddScoped<IStepOutputProjector, AcceptanceCriteriaProjector>();
        services.AddScoped<IStepOutputProjector, RiskRegisterProjector>();
        services.AddScoped<IStepOutputProjector, Loom.Application.Workflows.Wireframing.WireframeProjector>();

        // Kickoff-acceptance orchestration (transactional accept-decompose).
        services.AddScoped<Loom.Application.Workflows.Kickoff.IKickoffService, Loom.Application.Workflows.Kickoff.KickoffService>();

        return services;
    }

    /// <summary>
    /// Registers the outbox dispatcher hosted service plus the type registry
    /// it needs to deserialize event payloads. Call AFTER any handler
    /// registrations so DI sees them. Default-bound options are fine for
    /// Phase 1; Phase 5+ will read from configuration.
    /// </summary>
    public static IServiceCollection AddLoomOutboxDispatcher(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        params Assembly[] eventAssemblies)
    {
        if (configuration is not null)
        {
            services.AddOptions<OutboxDispatcherOptions>()
                .Bind(configuration.GetSection(OutboxDispatcherOptions.SectionName));
        }
        else
        {
            services.AddOptions<OutboxDispatcherOptions>();
        }

        // Always include the domain assembly so built-in events are
        // discoverable; callers can add more assemblies (e.g. their own
        // event types) by passing them in.
        var assemblies = eventAssemblies.Length == 0
            ? new[] { typeof(IDomainEvent).Assembly }
            : [typeof(IDomainEvent).Assembly, .. eventAssemblies];

        services.AddSingleton(_ => new DomainEventTypeRegistry(assemblies));
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }

    /// <summary>
    /// Registers the Loom bootstrapper hosted service. Idempotent — applies
    /// migrations and seeds workflow + fragments if missing. Call AFTER
    /// AddLoomInfrastructure so the repositories it depends on are wired.
    /// </summary>
    public static IServiceCollection AddLoomBootstrapper(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.AddOptions<BootstrapOptions>()
                .Bind(configuration.GetSection(BootstrapOptions.SectionName));
        }
        else
        {
            services.AddOptions<BootstrapOptions>();
        }
        services.AddHostedService<LoomBootstrapper>();
        return services;
    }
}

internal sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public enum DatabaseProviderKind { SqlServer, Sqlite }
public sealed record LoomDatabaseProvider(DatabaseProviderKind Kind);
