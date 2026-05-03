# ADR-0012 — Conversation persistence and run replay

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom team

## Context

Phase 3 ships two adjacent capabilities the design treats as separate:

1. **Conversation/Message** persistence — a durable record of the multi-turn
   dialog between human and agent within a run, separate from `RunEvent`
   (which is the engine's audit trail). Conversation lets the UI render
   "what was said" cleanly, distinct from "what the engine did".
2. **Replay** — a button on Run Detail that re-queues a run with the same
   workflow, step, engine, and (importantly) the same fragment versions, so
   a reviewer can re-derive a result deterministically.

The two share a question: when we replay, do we replay the *fragments and
prompt* or the *conversation*? Different products land differently here.

## Decision

- **Conversation is its own aggregate**: `Conversation` (root) owns a list of
  `Message` entities with an author (Human | Agent | System), content, and
  timestamps. Bound to a `RunId` (nullable — supports out-of-band threads
  later). Persisted with EF `OwnsOne(m => m.Author)` for the author VO.
- **Replay re-queues with pinned fragment versions**, not pinned messages.
  `IRunService.ReplayAsync(sourceRunId, requestedBy)` clones the source
  run's `(NodeId, WorkflowId, StepId, Engine, Budgets, Fragments)` tuple
  and emits a `RunQueued` domain event for the fresh run. The source run
  is annotated with a `StepOutput` `RunEvent` whose payload encodes the
  `replayed_as` link, so Run Detail surfaces the chain.
- A Phase-7 variant of replay will let the caller opt into "latest fragment
  versions" via a flag. Phase 3 ships only the deterministic version.

## Alternatives considered

- **Replay copies messages forward**: too coupled to a particular engine's
  conversation shape; the engine should re-derive its own messages from the
  fragments + node context.
- **Conversation lives inside `Run`**: rejected — conversations can outlive
  a run (the design hints at follow-up Q&A on completed runs in §10).
  Keeping it separate makes that future natural.
- **Replay = nothing more than a new `QueueAsync`**: doable, but loses the
  provenance link from old to new run. The annotation event is cheap and
  closes the loop.

## Consequences

- Run Detail can show *both* the engine's `RunEvent` timeline (technical)
  and the conversation thread (human-readable) without one polluting the
  other.
- Replay is honest about determinism — same fragments in, same prompt
  composition, same engine. The agent's nondeterminism is the only
  remaining variable, which is the point of replay.
- Conversation tables (`conversations`, `messages`) are additive; the
  Phase-3 migration creates them empty. UI for conversation rendering
  lands incrementally.

## Reversibility

Reversible. Conversation is a separate aggregate with no foreign-key dependency
from `Run` itself; dropping it would only lose the human-readable thread
view. Replay is a single application-service method; a second variant can
land beside it without churn.
