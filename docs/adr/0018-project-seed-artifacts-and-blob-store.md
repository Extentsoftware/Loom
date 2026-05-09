# ADR-0018 — Project seed artifacts and DB-backed blob store

- **Status**: Proposed
- **Date**: 2026-05-09
- **Decider(s)**: Loom team

## Context

The kickoff and project-creation flows currently start from a blank slate.
In practice, most real work begins with material the team has already
produced — repository URLs, Figma boards, screenshots of competing
products, sketches, screenshots of the existing UI we're replacing,
exported design specs. Today there is no place to attach any of that, so
the kickoff agent operates without context that the human took for granted
when they wrote the prompt, and the design round-trip starts from worse
ground than it has to.

Two related questions follow:

1. **Where do these "seed" artifacts live in the model?** The existing
   [`Artifact` aggregate](../../src/Loom.Domain/Artifacts/Artifact.cs) is
   node-bound (`NodeId`) and carries `ArtifactVersion` chains plus locks
   for the design round-trip (ADR-0013). A repository URL pasted at
   project creation isn't a versioned design output — it's reference
   material. Conflating the two muddles the version-chain semantics that
   ADR-0013 was careful about.
2. **Where do the bytes live?** `BlobRef.Uri` is already an abstraction
   over the storage backend, but Loom has no concrete backend. The team
   has a working precedent in
   `C:\Data\Fortius\Marketplace\marketplace-mcp-api`: `ChatFileBundle` is
   serialized via MessagePack into a `byte[]` column and addressed with
   a `ChatFileBundle://{guid}/{long}` URI that
   `DocumentDownloadService` resolves. Single moving part, no SAS
   tokens, no separate auth boundary.

## Decision

### Domain: `ProjectArtifact` as a new aggregate

- New aggregate `ProjectArtifact` lives in `Loom.Domain.Artifacts` next to
  `Artifact` but is *not* a version-chained design output. It is a flat,
  immutable-by-default reference attached to a `ProjectId` with an
  optional `NodeId` for feature-scoped seed material.
- Inheritance is a query concern, not an entity concern: feature lookups
  union `ProjectId = @p AND (NodeId IS NULL OR NodeId = @n)`. No
  cascade copying; the project-scope row stays the single source.
- Two flavours, distinguished by a `ProjectArtifactKind` enum:
  - **Link** (Repo, Figma, ExternalUrl, Confluence, Miro, …) — carries
    `Url` and `Label`; zero bytes stored.
  - **File** (Image, Document, ArchiveBundle) — carries an
    `ArtifactBundle` payload (filename, content-type, bytes; possibly
    multiple files per row for grouped uploads like a Figma frame
    export). The bundle is MessagePack-serialized into a
    `varbinary(max)` column.
- No version chain. Re-uploading replaces by deletion + new row; this is
  reference material, not a design deliverable. If a project artifact
  *becomes* a design output, the workflow promotes it into a node-bound
  `Artifact` with the seed as v1.
- No soft-lock. Multiple humans can touch the seed list concurrently;
  conflicts at this layer are not interesting.

### Infrastructure: DB-backed blob store mirroring the marketplace pattern

- `IArtifactBlobStore` (new, in `Loom.Application.Artifacts`) exposes
  `GetAsync(Uri)` and `PutAsync(ArtifactBundle)` returning `BlobRef`.
- The default implementation is `SqlArtifactBlobStore` in
  `Loom.Infrastructure`: writes to a new `ArtifactBlob` table
  (`Id Guid`, `Bytes varbinary(max)`, `ContentType`, `SizeBytes`,
  `CreatedUtc`). MessagePack handles the bundle serialization on the
  way in/out; the resolver in `BlobRef.Uri` form is
  `loom-artifact://{ArtifactBlobId}`.
- Retention: blobs are reference-counted by `ProjectArtifact` (and, when
  Phase-4 `ArtifactVersion.Content` migrates to this scheme, by version
  rows too). Orphan sweep runs in the existing outbox dispatcher cadence.
- Size cap: 25 MB per file, 100 MB per bundle, configurable via
  `Loom:Artifacts:MaxFileBytes`. Above the cap, the upload is rejected
  with a hint pointing at the (future) blob-store ADR.

### Surface: Kickoff and Create-Project

- `Pages/Kickoff.razor` and the project-create flow gain an artifact
  panel: drag-drop file area + "Add link" sub-form. Saves go through a
  new `IProjectArtifactService` on `Loom.Application`.
- `MainLayout` gains a small "Seeds" affordance on project-scope pages
  so users can manage attached material later, not only at kickoff.

### MCP: `get_artifact` and `list_project_artifacts`

- Two new tools on the in-process MCP server (ADR-0008):
  - `list_project_artifacts(projectId, nodeId?)` returns the union view
    so agents see both project-scope and feature-scope seeds without
    knowing the inheritance rule.
  - `get_artifact(uri)` resolves a `loom-artifact://…` URI to bytes +
    content-type, mirroring `marketplace-mcp-api`'s
    `DocumentDownloadService` shape.
- The kickoff workflow's prompt assembly (ADR-0007 — workflows-as-data)
  reads project artifacts and includes link metadata + small images
  inline, large blobs by reference for the agent to fetch via MCP.

## Alternatives considered

- **Reuse the existing `Artifact` aggregate with a "seed" kind.**
  Rejected. The version chain and lock model are load-bearing for design
  round-trips (ADR-0013); broadening the aggregate to also model flat
  reference material erodes invariants ("monotonic versioning",
  "agent-bypasses-human-locks") that only make sense for outputs. Cleaner
  to keep `Artifact` and `ProjectArtifact` as siblings.
- **Azure Blob storage (or any external object store) from day one.**
  Considered. It scales further and keeps DB backups slim, but introduces
  a second auth boundary, SAS-token plumbing, lifecycle management, and
  a hard dependency that complicates the SQLite/dev-mode path. We
  already accept SQL-Server as the durable substrate; reusing it for
  modest binary payloads is consistent with the
  modular-monolith stance (ADR-0001).
- **Base64-in-JSON inside the existing `Artifact` content column.**
  Rejected. ~33% size inflation, no native binary semantics, awkward to
  stream. MessagePack is a strict win for the size-and-encoding question
  and the marketplace codebase has already de-risked it.
- **Filesystem-backed blob store (e.g. an `App_Data/blobs` directory).**
  Rejected. Requires shared storage in any multi-instance deployment,
  doesn't survive container restarts without a volume, and complicates
  backup ("the DB and the disk must be in sync"). SQL `varbinary(max)`
  gives us atomicity + transactionality + backup-as-one-thing for free.
- **Inheritance via copy at feature-creation time.** Rejected. Two
  copies of a 5 MB Figma export per feature in a 30-feature project
  bloats the DB pointlessly. A union query is cheap and the
  single-source-of-truth makes "edit the project seed and every
  feature sees it" the natural behaviour.

## Consequences

- **Easier:** kickoff and enrichment agents have access to real
  reference material from the start. The "I have a screenshot of the
  existing UI" use case becomes a 5-second drag-drop instead of a
  multi-step external upload + paste-link workflow.
- **Easier:** the abstraction created here (`IArtifactBlobStore` +
  MessagePack `ArtifactBundle`) is the same one the Phase-4 design-
  output `BlobRef` will use when it moves off whatever stub it has now,
  so this ADR also lays the rail for that migration.
- **Harder:** DB backup size now scales with seed-artifact volume.
  Empirically, a project with 10 screenshots + a Figma frame export sits
  comfortably under 50 MB. We should revisit if any project crosses
  ~1 GB total seed payload — at that point an Azure Blob backend
  behind the same `IArtifactBlobStore` interface is the obvious move.
- **Harder:** MessagePack adds a dependency to `Loom.Infrastructure`.
  Marketplace already runs it in production; the package is mature.
  Domain stays MessagePack-free; only the bundle DTOs in
  `Loom.Application.Artifacts` (or a small `Loom.Contracts` addition)
  carry `[MessagePackObject]` attributes.
- **New constraint:** the `loom-artifact://` URI scheme is now part of
  Loom's MCP contract. Changing it later means a migration of stored
  `BlobRef.Uri` values; treat it as load-bearing.
- **Migration shape:** a single additive migration creates
  `ProjectArtifact`, `ProjectArtifactLink`, and `ArtifactBlob`. Existing
  artifacts (Phase 4) are untouched in this slice; their move onto the
  shared blob store is a follow-up ADR / migration.

## Reversibility

Reversible, with two sharp edges.

- **The aggregate split** is cheap to reverse: drop `ProjectArtifact`
  rows or fold them into `Artifact` with a `Kind = Seed` flag. The
  schema diff is small.
- **The DB-backed blob store is reversible-with-effort.** Once seed
  blobs are in `ArtifactBlob`, moving them to Azure Blob means writing a
  `IArtifactBlobStore` implementation that re-uploads each row and
  rewrites `BlobRef.Uri` values. The interface boundary makes this a
  data migration rather than a code migration; the cost grows linearly
  with how much was uploaded before the switch.
- **The URI scheme** is the stickiest piece — once agents and MCP
  clients persist `loom-artifact://…` references in their own state,
  changing it requires a coordinated rename. Keep it stable.
