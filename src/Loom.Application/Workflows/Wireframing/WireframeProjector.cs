using Loom.Application.Abstractions;
using Loom.Application.Artifacts;
using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Wireframing;

/// <summary>
/// Projects the <c>Wireframe</c> JSON output from a wireframing agent step
/// onto the target node as an Artifact of kind <see cref="ArtifactKind.Wireframe"/>.
/// The agent is expected to emit JSON of the shape
/// <c>{"description":"…","html":"…"}</c>; the projector stores the entire
/// JSON envelope on the canonical pointer's ExternalId for the inspector
/// to render. Phase 4b switches the canonical store to Figma once the
/// adapter is wired.
/// </summary>
public sealed class WireframeProjector(
    IArtifactRepository artifacts,
    IArtifactService artifactService) : IStepOutputProjector
{
    public string SchemaName => "Wireframe";

    public async Task ProjectAsync(NodeId nodeId, WorkflowStep workflowStep, RunId runId, string output, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        var existing = await artifacts.GetByNodeAsync(nodeId, ct);
        var match = existing.FirstOrDefault(a => a.Kind == ArtifactKind.Wireframe);
        var pointer = new CanonicalPointer(
            Store: CanonicalStore.HubNative,
            ExternalId: output.Trim(),
            Url: null);

        if (match is null)
        {
            await artifactService.CreateAsync(
                nodeId,
                ArtifactKind.Wireframe,
                title: "Proposed wireframe",
                canonical: pointer,
                ct);
        }
    }
}
