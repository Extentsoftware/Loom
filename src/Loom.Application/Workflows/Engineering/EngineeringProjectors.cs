using Loom.Application.Abstractions;
using Loom.Application.Artifacts;
using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Engineering;

/// <summary>
/// Shared base for the engineering projectors — each one writes (or
/// updates) one Artifact of a specific kind, carrying the agent's JSON
/// envelope on the canonical pointer's ExternalId. The Artifact
/// Inspector's per-kind renderer picks it up from there.
/// </summary>
internal static class EngineeringProjectorHelpers
{
    public static async Task UpsertArtifactAsync(
        IArtifactRepository artifacts,
        IArtifactService artifactService,
        NodeId nodeId,
        ArtifactKind kind,
        string title,
        string body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }
        var existing = await artifacts.GetByNodeAsync(nodeId, ct);
        // SliceDesign and ImplementationPlan both project as Kind=Doc;
        // dedupe by (kind, title) so the two don't fight over one row.
        var match = existing.FirstOrDefault(a => a.Kind == kind && a.Title == title);
        var pointer = new CanonicalPointer(
            Store: CanonicalStore.HubNative,
            ExternalId: body.Trim(),
            Url: null);
        if (match is null)
        {
            await artifactService.CreateAsync(nodeId, kind, title, pointer, ct);
        }
        else
        {
            await artifactService.UpdateCanonicalAsync(match.Id, pointer, ct);
        }
    }
}

public sealed class SliceDesignProjector(
    IArtifactRepository artifacts,
    IArtifactService artifactService) : IStepOutputProjector
{
    public string SchemaName => "SliceDesign";

    public Task ProjectAsync(NodeId nodeId, WorkflowStep workflowStep, RunId runId, string output, CancellationToken ct = default) =>
        EngineeringProjectorHelpers.UpsertArtifactAsync(
            artifacts, artifactService, nodeId,
            ArtifactKind.Doc, "Slice design", output, ct);
}

public sealed class ImplementationPlanProjector(
    IArtifactRepository artifacts,
    IArtifactService artifactService) : IStepOutputProjector
{
    public string SchemaName => "ImplementationPlan";

    public Task ProjectAsync(NodeId nodeId, WorkflowStep workflowStep, RunId runId, string output, CancellationToken ct = default) =>
        EngineeringProjectorHelpers.UpsertArtifactAsync(
            artifacts, artifactService, nodeId,
            ArtifactKind.Doc, "Implementation plan", output, ct);
}

public sealed class TestPlanProjector(
    IArtifactRepository artifacts,
    IArtifactService artifactService) : IStepOutputProjector
{
    public string SchemaName => "TestPlan";

    public Task ProjectAsync(NodeId nodeId, WorkflowStep workflowStep, RunId runId, string output, CancellationToken ct = default) =>
        EngineeringProjectorHelpers.UpsertArtifactAsync(
            artifacts, artifactService, nodeId,
            ArtifactKind.TestPlan, "Test plan", output, ct);
}
