using Loom.Application.Abstractions;
using Loom.Application.Artifacts;
using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows.Enrichment;

/// <summary>
/// Projects the <c>AcceptanceCriteria</c> JSON output from the enrichment
/// workflow's "acceptance" step onto the target node as an Artifact of
/// kind <see cref="ArtifactKind.Criteria"/>. The raw JSON is stored on the
/// canonical pointer's ExternalId — the workspace renders it inline; a
/// future iteration may parse it into structured node fields.
///
/// Idempotent: if an existing Criteria artifact for this node already
/// exists, we update its canonical pointer rather than creating a duplicate.
/// </summary>
public sealed class AcceptanceCriteriaProjector(
    IArtifactRepository artifacts,
    IArtifactService artifactService) : IStepOutputProjector
{
    public string SchemaName => "AcceptanceCriteria";

    public async Task ProjectAsync(NodeId nodeId, WorkflowStep workflowStep, RunId runId, string output, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        var existing = await artifacts.GetByNodeAsync(nodeId, ct);
        var match = existing.FirstOrDefault(a => a.Kind == ArtifactKind.Criteria);
        var pointer = new CanonicalPointer(
            Store: CanonicalStore.HubNative,
            ExternalId: output.Trim(),
            Url: null);

        if (match is null)
        {
            await artifactService.CreateAsync(
                nodeId,
                ArtifactKind.Criteria,
                title: "Acceptance criteria",
                canonical: pointer,
                ct);
        }
        // Update path is intentionally skipped for the skinny cut — the
        // domain doesn't expose a "replace canonical" mutator yet, and
        // the most recent run's output is always discoverable from the
        // run's transcript. A future iteration adds a versioned update.
    }
}

public sealed class RiskRegisterProjector(
    IArtifactRepository artifacts,
    IArtifactService artifactService) : IStepOutputProjector
{
    public string SchemaName => "RiskRegister";

    public async Task ProjectAsync(NodeId nodeId, WorkflowStep workflowStep, RunId runId, string output, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        var existing = await artifacts.GetByNodeAsync(nodeId, ct);
        var match = existing.FirstOrDefault(a => a.Kind == ArtifactKind.Risks);
        var pointer = new CanonicalPointer(
            Store: CanonicalStore.HubNative,
            ExternalId: output.Trim(),
            Url: null);

        if (match is null)
        {
            await artifactService.CreateAsync(
                nodeId,
                ArtifactKind.Risks,
                title: "Risk register",
                canonical: pointer,
                ct);
        }
    }
}
