using Loom.Application.Abstractions;
using Loom.Domain.Annotations;
using Loom.Domain.Artifacts;
using Loom.Domain.BudgetControl;
using Loom.Domain.Conversations;
using Loom.Domain.Fragments;
using Loom.Domain.Integrations;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Loom.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Loom.Infrastructure.Persistence;

public sealed class LoomDbContext : DbContext, IUnitOfWork
{
    private readonly IDomainEventCollector? _events;

    public LoomDbContext(DbContextOptions<LoomDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Used by DI when the collector is registered. The DbContext implements
    /// the outbox-flush logic itself rather than calling out to an
    /// IOutboxWriter — that indirection would create a DI cycle (the writer
    /// depends on the DbContext, the DbContext depends on the writer). The
    /// one-arg constructor stays here so design-time tooling and tests that
    /// bypass DI can still construct a context.
    /// </summary>
    public LoomDbContext(
        DbContextOptions<LoomDbContext> options,
        IDomainEventCollector events) : base(options)
    {
        _events = events;
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<FeatureNode> Nodes => Set<FeatureNode>();
    public DbSet<Fragment> Fragments => Set<Fragment>();
    public DbSet<FragmentVersion> FragmentVersions => Set<FragmentVersion>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();
    public DbSet<AssembledPrompt> AssembledPrompts => Set<AssembledPrompt>();
    public DbSet<Transcript> Transcripts => Set<Transcript>();
    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Annotation> Annotations => Set<Annotation>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ProjectBudget> ProjectBudgets => Set<ProjectBudget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        // SQLite has no notion of schemas; EnsureCreated trips on it.
        if (!isSqlite)
        {
            modelBuilder.HasDefaultSchema("loom");
        }
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoomDbContext).Assembly);

        if (isSqlite)
        {
            // SQLite has no rowversion. FeatureNode.Version was configured
            // as IsRowVersion which makes EF skip writing the column on
            // INSERT and read it back via RETURNING. SQLite never supplies
            // a value, so we drop the value-generation behaviour and add a
            // DB-level default of 0. Concurrency-checking is best-effort
            // on SQLite (dev provider only).
            var versionProp = modelBuilder.Entity<Loom.Domain.Nodes.FeatureNode>()
                .Property(n => n.Version);
            versionProp.Metadata.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            versionProp.Metadata.IsConcurrencyToken = false;
            versionProp.HasDefaultValue(0u);
        }

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Drains the per-request domain-event collector and writes the events
    /// into the outbox table inside the same transaction as the aggregate
    /// changes. Domain handlers run later, asynchronously, via the dispatcher.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_events is not null)
        {
            var pending = _events.Drain();
            foreach (var evt in pending)
            {
                var eventType = evt.GetType().FullName
                    ?? throw new InvalidOperationException("Domain event has no full name.");
                var payloadJson = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType(), (System.Text.Json.JsonSerializerOptions?)null);
                Outbox.Add(OutboxEntry.Create(evt.OccurredAt, eventType, payloadJson));
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
