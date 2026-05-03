using Loom.Domain.Annotations;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Conversations;
using Loom.Domain.Fragments;
using Loom.Domain.Integrations;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
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

    public static readonly ValueConverter<WorkflowId, Guid> WorkflowId =
        new(id => id.Value, g => new WorkflowId(g));

    public static readonly ValueConverter<WorkflowStepId, Guid> WorkflowStepId =
        new(id => id.Value, g => new WorkflowStepId(g));

    public static readonly ValueConverter<AssembledPromptId, Guid> AssembledPromptId =
        new(id => id.Value, g => new AssembledPromptId(g));

    public static readonly ValueConverter<AssembledPromptId?, Guid?> NullableAssembledPromptId =
        new(id => id.HasValue ? id.Value.Value : null,
            g => g.HasValue ? new AssembledPromptId(g.Value) : null);

    public static readonly ValueConverter<RunEventId, Guid> RunEventId =
        new(id => id.Value, g => new RunEventId(g));

    public static readonly ValueConverter<TranscriptId, Guid> TranscriptId =
        new(id => id.Value, g => new TranscriptId(g));

    public static readonly ValueConverter<IntegrationConnectionId, Guid> IntegrationConnectionId =
        new(id => id.Value, g => new IntegrationConnectionId(g));

    public static readonly ValueConverter<IntegrationConnectionId?, Guid?> NullableIntegrationConnectionId =
        new(id => id.HasValue ? id.Value.Value : null,
            g => g.HasValue ? new IntegrationConnectionId(g.Value) : null);

    public static readonly ValueConverter<WebhookDeliveryId, Guid> WebhookDeliveryId =
        new(id => id.Value, g => new WebhookDeliveryId(g));

    public static readonly ValueConverter<ConversationId, Guid> ConversationId =
        new(id => id.Value, g => new ConversationId(g));

    public static readonly ValueConverter<MessageId, Guid> MessageId =
        new(id => id.Value, g => new MessageId(g));

    public static readonly ValueConverter<AnnotationId, Guid> AnnotationId =
        new(id => id.Value, g => new AnnotationId(g));

    public static readonly ValueConverter<SubscriptionId, Guid> SubscriptionId =
        new(id => id.Value, g => new SubscriptionId(g));

    public static readonly ValueConverter<RunId?, Guid?> NullableRunId =
        new(id => id.HasValue ? id.Value.Value : null,
            g => g.HasValue ? new RunId(g.Value) : null);
}
