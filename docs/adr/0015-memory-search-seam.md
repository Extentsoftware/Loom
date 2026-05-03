# ADR-0015 — Memory search seam (Phase 6a) and the deferred Elasticsearch swap

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom team

## Context

Phase 6 of the master plan calls for similarity / memory search backed by
Elasticsearch (hybrid BM25 + vector), embedding generation per document,
and a separate `Loom.Indexer` worker process that projects from the
outbox into ES. The plan flags a clear pressure cut: keyword search
(Phase 2) is good enough for v1, and Phase 6 can collapse into Phase 7.

The product *needs* the seam — the kickoff workflow's "we've seen
something like this before" panel is the most-cited Loom moment in the
design doc. But the product does not need the ES dependency, the
embedding pipeline, the indexer worker, or the global search box right
now. Building all of that in one phase is what the plan warns against.

## Decision

Ship a thin **Phase 6a slice** that establishes the seam:

- **`IMemorySearch`** in `Loom.Application.Memory` — a single
  `SearchSimilarAsync(query, projectId, limit, ct)` method returning a
  `SimilarityReport` (query echo + candidate list with score and
  derived summary).
- **`SqlMemorySearch`** — a SQL-backed implementation that delegates to
  the existing `IFeatureNodeRepository.SearchAsync` for candidate
  retrieval and scores by token-overlap (title × 2 + intent) with a
  recency boost (linear decay over 180 days, floor at 0.5). The
  repository's substring match is widened by submitting the first
  significant query token rather than the full multi-word query.
- **`MemoryLookupStep : IInProcStep`** — a workflow step keyed
  `"memory-lookup"` that reads `memory-lookup.query` /
  `memory-lookup.project_id` / `memory-lookup.limit` inputs and emits
  the serialized `SimilarityReport` for downstream agent steps to fold
  into context.
- **DI**: `IMemorySearch` and the new `IInProcStep` registered alongside
  the existing scoped services. No new packages, no new project, no
  Elasticsearch.

**Defer to Phase 6b**: Elasticsearch client, BM25 + vector hybrid query,
`Loom.Indexer` worker process, embedding generation, re-index admin
command, global search box UI, and the Notification Centre full
version. All swappable behind the existing `IMemorySearch` interface.

## Alternatives considered

- **Ship the full Phase 6 (ES + indexer + embeddings + UI)**: rejected
  per the master plan's pressure cut and the user's stated frustration
  with sprawl. The risk of ES infra issues (managed cluster setup,
  cluster sizing, schema migrations, index aliasing) eating the budget
  is real.
- **Skip Phase 6 entirely; do the memory step in Phase 7**: rejected.
  The seam is exactly the wrong thing to defer — every phase that lands
  before it has to special-case "memory not available yet". A thin SQL
  implementation removes that branch.
- **Use SQL Server full-text search instead of LINQ Contains**: an
  improvement, but adds a migration to enable FTS catalogues, and the
  ES swap is the real upgrade. The overlap between FTS + custom scoring
  and ES is large; not worth the detour.
- **Build a separate `Loom.Memory` project for the seam**: rejected for
  Phase 6a. Two files in `Loom.Application.Memory` is enough; pulling
  out a project pays off only when ES code lands.

## Consequences

- The kickoff workflow can already include a `memory-lookup` step — no
  workflow-engine changes were needed because `IInProcStep` already
  resolves by key.
- Multi-word query support is honest: the SQL backend probes with the
  first significant token then re-ranks the candidate set. Real BM25
  is an upgrade, not a feature swap.
- No global search box ships in Phase 6a. Per the plan's cut #4 it can
  ride along with Phase 7's UI work — at which point the ES backend
  will be there to serve it.
- The Notification Centre full version is also deferred; Phase 3 ships
  enough of the inbox shape for the kernel demo.

## Reversibility

Trivially reversible. `IMemorySearch` is the only consumer-facing surface;
swapping `SqlMemorySearch` for an `ElasticMemorySearch` is a DI
registration change. The `MemoryLookupStep` is unchanged across the swap.
