using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class TranscriptConfiguration : IEntityTypeConfiguration<Transcript>
{
    public void Configure(EntityTypeBuilder<Transcript> b)
    {
        b.ToTable("transcripts");

        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasConversion(ValueConverters.TranscriptId);
        b.Property(t => t.RunId).HasConversion(ValueConverters.RunId);
        b.Property(t => t.RawText).IsRequired();
        b.Property(t => t.NormalizedJson);
        b.Property(t => t.CreatedAt).IsRequired();

        b.HasIndex(t => t.RunId).IsUnique();
    }
}
