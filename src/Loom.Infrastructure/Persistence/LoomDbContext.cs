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
using Microsoft.EntityFrameworkCore.Storage;

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
    public DbSet<ProjectArtifact> ProjectArtifacts => Set<ProjectArtifact>();
    internal DbSet<ArtifactBlob> ArtifactBlobs => Set<ArtifactBlob>();
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
    public DbSet<Notification> Notifications => Set<Notification>();
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
            // FeatureNode.Version is now configured globally as a
            // non-server-generated concurrency token (see
            // FeatureNodeConfiguration), so the SQLite-specific tweak
            // that previously disabled rowversion semantics is no
            // longer needed.

            // SQLite has no IDENTITY equivalent; tell EF to not treat the
            // Sequence column as server-generated so it's included in the
            // INSERT. The DbContext SaveChangesAsync override assigns
            // values client-side as MAX+1.
            var seqProp = modelBuilder.Entity<Loom.Infrastructure.Outbox.OutboxEntry>()
                .Property(e => e.Sequence);
            seqProp.Metadata.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;

            // SQLite stores DateTimeOffset as TEXT and refuses to translate
            // ORDER BY against it (string compare doesn't sort offset-aware
            // values correctly). Convert every DateTimeOffset[?] property
            // in the model to long (UTC ticks) so SQLite stores them as
            // INTEGER. Read-back returns UTC — Loom uses these timestamps
            // for ordering, audit, and elapsed-time math, never for
            // offset-sensitive display, so this is acceptable on the dev
            // provider. MSSQL keeps native datetimeoffset semantics.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(Loom.Infrastructure.Persistence.Configurations.ValueConverters.DateTimeOffsetToTicks);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(Loom.Infrastructure.Persistence.Configurations.ValueConverters.NullableDateTimeOffsetToTicks);
                    }
                }
            }
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
        var newOutbox = new List<OutboxEntry>();
        if (_events is not null)
        {
            var pending = _events.Drain();
            foreach (var evt in pending)
            {
                var eventType = evt.GetType().FullName
                    ?? throw new InvalidOperationException("Domain event has no full name.");
                var payloadJson = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType(), (System.Text.Json.JsonSerializerOptions?)null);
                var entry = OutboxEntry.Create(evt.OccurredAt, eventType, payloadJson);
                newOutbox.Add(entry);
                Outbox.Add(entry);
            }
        }

        // SQLite has no IDENTITY column equivalent. Assign Sequence client-
        // side as MAX+1 over existing rows. Race-prone in multi-writer
        // scenarios but SQLite is dev-only and runs in-process. SQL Server
        // keeps using the IDENTITY column from the configuration.
        if (newOutbox.Count > 0 &&
            Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var maxSeq = await Outbox.AsNoTracking()
                .Select(o => (long?)o.Sequence)
                .MaxAsync(cancellationToken) ?? 0L;
            for (var i = 0; i < newOutbox.Count; i++)
            {
                newOutbox[i].Sequence = maxSeq + i + 1;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var inner = await Database.BeginTransactionAsync(ct);
        return new EfTransaction(inner);
    }

    /// <summary>
    /// Thin adapter from EF Core's <c>IDbContextTransaction</c> to the
    /// application-layer <see cref="IUnitOfWorkTransaction"/> abstraction.
    /// </summary>
    private sealed class EfTransaction(IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
