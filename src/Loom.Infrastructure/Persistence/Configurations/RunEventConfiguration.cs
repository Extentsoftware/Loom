using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEvent>
{
    public void Configure(EntityTypeBuilder<RunEvent> b)
    {
        b.ToTable("run_events");

        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasConversion(ValueConverters.RunEventId);
        b.Property(e => e.RunId).HasConversion(ValueConverters.RunId);
        b.Property(e => e.Sequence).IsRequired();
        b.Property(e => e.Kind).HasConversion<int>().IsRequired();
        b.Property(e => e.PayloadJson);
        b.Property(e => e.At).IsRequired();

        b.HasIndex(e => new { e.RunId, e.Sequence }).IsUnique();
    }
}
