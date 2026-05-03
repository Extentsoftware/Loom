using System.Reflection;
using Loom.Application.Abstractions;
using Loom.Application.Agents;
using Loom.Application.Artifacts;
using Loom.Application.BudgetControl;
using Loom.Application.Common;
using Loom.Application.Conversations;
using Loom.Application.Features;
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
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IRunEventRepository, RunEventRepository>();
        services.AddScoped<IAssembledPromptRepository, AssembledPromptRepository>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<IIntegrationConnectionRepository, IntegrationConnectionRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IAnnotationRepository, AnnotationRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
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
        services.AddScoped<IAssembledPromptComposer, AssembledPromptComposer>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IAgentRouter, DefaultAgentRouter>();

        // Phase-3 services.
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IArtifactService, ArtifactService>();

        // Phase-5: budget control + engine health monitor.
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddSingleton<IEngineHealthMonitor, InMemoryEngineHealthMonitor>();

        // Notification channel stubs — InApp lives in Loom.Web (registered
        // there). Teams + Email are stubs in Phase 3; real impls land in
        // Phase 5+ once tenant config is in.
        services.AddScoped<INotificationChannel, TeamsChannelStub>();
        services.AddScoped<INotificationChannel, EmailChannelStub>();

        // Outbox handlers that bridge domain events into NotificationService.
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.NodeUpdated>, NodeUpdatedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.RunPausedForHuman>, RunPausedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.RunCompleted>, RunCompletedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.RunFailed>, RunFailedNotificationHandler>();

        // Auto-queue enrichment workflow for every newly-created child node.
        services.AddScoped<IDomainEventHandler<Loom.Domain.Common.DomainEvents.NodeCreated>, EnrichmentAutoQueueHandler>();

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
