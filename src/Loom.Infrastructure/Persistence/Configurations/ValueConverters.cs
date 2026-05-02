using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loom.Infrastructure.Persistence.Configurations;

internal static class ValueConverters
{
    public static readonly ValueConverter<Slug, string> Slug =
        new(s => s.Value, s => Domain.Common.Slug.From(s));

    public static readonly ValueConverter<NodeId, Guid> NodeId =
        new(id => id.Value, g => new NodeId(g));

    public static readonly ValueConverter<NodeId?, Guid?> NullableNodeId =
        new(id => id.HasValue ? id.Value.Value : null,
            g => g.HasValue ? new NodeId(g.Value) : null);

    public static readonly ValueConverter<FragmentId, Guid> FragmentId =
        new(id => id.Value, g => new FragmentId(g));

    public static readonly ValueConverter<FragmentVersionId, Guid> FragmentVersionId =
        new(id => id.Value, g => new FragmentVersionId(g));

    public static readonly ValueConverter<FragmentVersionId?, Guid?> NullableFragmentVersionId =
        new(id => id.HasValue ? id.Value.Value : null,
            g => g.HasValue ? new FragmentVersionId(g.Value) : null);

    public static readonly ValueConverter<RunId, Guid> RunId =
        new(id => id.Value, g => new RunId(g));

    public static readonly ValueConverter<ArtifactId, Guid> ArtifactId =
        new(id => id.Value, g => new ArtifactId(g));
}
