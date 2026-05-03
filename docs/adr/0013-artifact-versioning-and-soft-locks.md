# ADR-0013 — Artifact versioning and soft-lock model

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom team

## Context

Phase 4 turns `Artifact` from a stub descriptor into the durable carrier of
versioned content attached to a node — wireframes, criteria, ADRs, code
snippets, and so on. Two questions had to be answered:

1. **Versioning shape.** Does an artifact carry the chain of versions itself,
   or does each version live in its own table referenced loosely?
2. **Concurrent edit safety.** When a human and an agent both want to write
   to an artifact, what protects the human's in-flight changes?

The product hinges on the round-trip mechanic: an agent produces a version, a
designer annotates it, a re-run of the workflow incorporates those
annotations as feedback fragments, and the agent produces v2. That round
trip falls apart if version chains diverge silently or if a hot agent run
clobbers a designer's open editor.

## Decision

- `ArtifactVersion` is an entity inside the `Artifact` aggregate. Versions
  are append-only, numbered monotonically from 1, and carry an `Author` VO
  (`Human | Agent | System`, with `Id` resolving to a user id, run id, or
  system marker), a `BlobRef` for the content, an optional preview
  `BlobRef`, and a free-text reason.
- A "rollback" is a new version pointing at an older blob — never an
  in-place edit, never a deletion. The chain is the audit trail.
- Soft locks are time-based: `AcquireLock(userId, duration, now)` returns
  immediately if the artifact is unlocked or the existing lock is held by
  the same user (in which case the expiry is extended) or the existing
  lock is expired (in which case it is replaced). Active locks held by a
  *different* user reject.
- **Agents bypass human locks** when publishing versions. The lock protects
  against concurrent *human* edits; an agent run was already authorised by
  someone with permission to start it. A re-run that produces a version
  while a human is editing surfaces in the version chain — the human's
  unsaved local changes are not lost, just concurrent.
- Project admins may force-release a lock via
  `IArtifactService.ForceReleaseLockAsync`. The aggregate's
  `ForceReleaseLock` method trusts that the application service performed
  the role check.

## Alternatives considered

- **Versions as a sibling aggregate referenced by id.** Rejected — the
  monotonic numbering invariant must be enforced atomically with the
  parent's `UpdatedAt`, which is awkward across aggregates.
- **Optimistic concurrency tokens instead of locks.** Considered. Locks
  feel heavier but match the design's "lock holders introduce first
  per-resource permission" framing (§16.5) and give the UI a reliable
  signal to render ("you have the lock until 11:42"). Optimistic
  concurrency would be inferior for long-form designer editing where the
  conflict window is hours, not seconds.
- **Hard locks with keep-alives.** Rejected — keep-alive plumbing is
  fiddly, and a disconnected client should not block the artifact
  forever. Time-based expiry trades a little awkwardness for simplicity.
- **CRDT or operational transform.** Rejected for v1 (consistent with
  decision #13 in the master plan); the artifacts we ship are document-
  shaped, not text-stream-shaped.

## Consequences

- The version table is the audit trail for design round-trips: any agent
  run can query "what did v3 look like?" and re-derive accurately.
- Lock UI is the designer-side affordance: claim the lock to signal
  intent; release when done; let it expire if you wander off.
- Agent runs that produce versions must call into `ArtifactService` with
  `Author.Kind = Agent`. The aggregate enforces the semantics; callers
  can't accidentally spoof `Author.Kind = Human`.
- Migration `20260503171255_ArtifactVersionsLocks` is strictly additive —
  existing (empty) artifacts gain the lock columns and version table
  without backfill.

## Reversibility

Reversible. Versions are additive; we could rip out the chain and keep only
the latest by writing a follow-up migration that materialises the current
version into the parent row (with data loss for older versions). Locks are
purely additive columns.
