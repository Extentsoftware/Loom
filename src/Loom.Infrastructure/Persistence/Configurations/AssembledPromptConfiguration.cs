using System.Text.Json;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class AssembledPromptConfiguration : IEntityTypeConfiguration<AssembledPrompt>
{
    public void Configure(EntityTypeBuilder<AssembledPrompt> b)
    {
        b.ToTable("assembled_prompts");

        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasConversion(ValueConverters.AssembledPromptId);
        b.Property(p => p.RunId).HasConversion(ValueConverters.RunId);
        b.Property(p => p.SystemPrompt).IsRequired();
        b.Property(p => p.OutputSchemaJson);
        b.Property(p => p.AssembledAt).IsRequired();

        b.HasIndex(p => p.RunId).IsUnique();

        // Messages and Fragments persisted as JSON. Both are frozen at run
        // queue time and only ever read whole; JSON beats a child table.
        var messages = b.Property<List<AssembledPromptMessage>>("_messages")
            .HasColumnName("messages")
            .HasConversion(MessageJsonConverter, MessageListComparer);
        messages.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        var fragments = b.Property<List<FragmentRef>>("_fragments")
            .HasColumnName("fragments")
            .HasConversion(FragmentRefJsonConverter, FragmentRefListComparer);
        fragments.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        b.Ignore(p => p.Messages);
        b.Ignore(p => p.Fragments);
    }

    private sealed record MessageRow(string Role, string Content);

    private static readonly ValueConverter<List<AssembledPromptMessage>, string> MessageJsonConverter =
        new(
            v => JsonSerializer.Serialize(
                (v ?? new List<AssembledPromptMessage>())
                    .Select(m => new MessageRow(m.Role, m.Content)).ToList(),
                (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<AssembledPromptMessage>()
                : (JsonSerializer.Deserialize<List<MessageRow>>(v, (JsonSerializerOptions?)null) ?? new List<MessageRow>())
                    .Select(r => new AssembledPromptMessage(r.Role, r.Content))
                    .ToList());

    private static readonly ValueComparer<List<AssembledPromptMessage>> MessageListComparer =
        new(
            (a, b) => (a ?? new List<AssembledPromptMessage>()).SequenceEqual(b ?? new List<AssembledPromptMessage>()),
            v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
            v => v.ToList());

    private sealed record FragmentRefRow(Guid FragmentId, Guid VersionId, int Version);

    private static readonly ValueConverter<List<FragmentRef>, string> FragmentRefJsonConverter =
        new(
            v => JsonSerializer.Serialize(
                (v ?? new List<FragmentRef>())
                    .Select(r => new FragmentRefRow(r.FragmentId.Value, r.VersionId.Value, r.Version)).ToList(),
                (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<FragmentRef>()
                : (JsonSerializer.Deserialize<List<FragmentRefRow>>(v, (JsonSerializerOptions?)null) ?? new List<FragmentRefRow>())
                    .Select(r => new FragmentRef(new FragmentId(r.FragmentId), new FragmentVersionId(r.VersionId), r.Version))
                    .ToList());

    private static readonly ValueComparer<List<FragmentRef>> FragmentRefListComparer =
        new(
            (a, b) => (a ?? new List<FragmentRef>()).SequenceEqual(b ?? new List<FragmentRef>()),
            v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
            v => v.ToList());
}
