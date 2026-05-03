using Loom.Application.Abstractions;
using Loom.Domain.Artifacts;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;

namespace Loom.Infrastructure.Persistence;

public sealed class LoomDbContext : DbContext, IUnitOfWork
{
    public LoomDbContext(DbContextOptions<LoomDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<FeatureNode> Nodes => Set<FeatureNode>();
    public DbSet<Fragment> Fragments => Set<Fragment>();
    public DbSet<FragmentVersion> FragmentVersions => Set<FragmentVersion>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("loom");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoomDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
