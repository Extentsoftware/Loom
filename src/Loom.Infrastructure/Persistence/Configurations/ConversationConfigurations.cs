using Loom.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> b)
    {
        b.ToTable("conversations");

        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasConversion(ValueConverters.ConversationId);
        b.Property(c => c.NodeId).HasConversion(ValueConverters.NodeId);
        b.Property(c => c.RunId).HasConversion(ValueConverters.NullableRunId);
        b.Property(c => c.Topic).HasMaxLength(300).IsRequired();
        b.Property(c => c.CreatedAt).IsRequired();
        b.Property(c => c.UpdatedAt).IsRequired();

        b.HasIndex(c => c.NodeId);
        b.HasIndex(c => c.RunId);

        b.Ignore(c => c.Messages);

        b.HasMany<Message>("_messages")
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation("_messages").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("messages");

        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasConversion(ValueConverters.MessageId);
        b.Property(m => m.ConversationId).HasConversion(ValueConverters.ConversationId);
        b.Property(m => m.Content).IsRequired();
        b.Property(m => m.CreatedAt).IsRequired();

        b.OwnsOne(m => m.Author, a =>
        {
            a.Property(p => p.Kind).HasColumnName("author_kind").HasConversion<int>().IsRequired();
            a.Property(p => p.UserId).HasColumnName("author_user_id");
            a.Property(p => p.RunId).HasColumnName("author_run_id");
        });

        b.HasIndex(m => new { m.ConversationId, m.CreatedAt });
    }
}
