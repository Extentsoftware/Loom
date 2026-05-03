# ADR-0006 — Outbox + in-process dispatcher for domain events

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom kernel team

## Context

Loom's domain services need to broadcast state changes to other parts of the
system: SignalR pushes to UI clients (`NodeUpdated`, `RunStateChanged`), the
notification service (Phase 3), the Elasticsearch indexer (Phase 6), Teams
cards (Phase 3). The broadcast must be *atomic* with the aggregate change —
a node update without its event ships is worse than no event at all, because
downstream views go stale silently.

We considered an in-memory pub/sub (MediatR) and a message broker
(MassTransit / Azure Service Bus) before settling on the chosen shape.

## Decision

Domain events are written to an **outbox table** in MSSQL inside the same
transaction as the aggregate change. A separate **in-process dispatcher**
(`OutboxDispatcher`, a `BackgroundService`) polls the table, deserializes
each row's payload to its concrete `IDomainEvent` type, resolves all
registered `IDomainEventHandler<T>` for that type from a per-batch DI scope,
and calls them.

The flush happens inside the `LoomDbContext.SaveChangesAsync` override:
`IDomainEventCollector` accumulates events during a unit of work; the
override drains the collector and inserts the rows just before EF commits.
This keeps the event-write atomic with the aggregate changes — they hit the
same SQL transaction.

The outbox row carries:
- `Id` (Guid v7)
- `Sequence` (monotonic IDENTITY column — Phase 6's indexer reads in order)
- `OccurredAt`, `EventType` (full type name), `PayloadJson`, `ProcessedAt?`,
  `Attempts`

## Alternatives considered

1. **MediatR / in-memory pub/sub** — simplest to wire. Rejected because
   handlers run in the request's lifecycle, so a handler failure can roll
   back the aggregate change; we want at-least-once delivery decoupled from
   the originating transaction. Also doesn't survive a process restart with
   pending notifications.

2. **MassTransit / Azure Service Bus** — production-grade, durable, but
   over-engineered for v1. Adds a broker dependency, ops surface, schema
   versioning concerns. Phase 5+ may layer this on top of the outbox if we
   need cross-process fan-out.

3. **EF Core `SavedChanges` event without an outbox table** — events live in
   memory, dispatched after commit. Rejected because a process crash between
   commit and dispatch loses the event silently; the outbox makes the loss
   recoverable.

4. **Outbox + dedicated worker process** (the eventual Phase 6 shape) —
   chosen for the indexer in Phase 6 but not for Phase 1 because it adds
   deploy complexity for no immediate benefit. Phase 6 swaps the in-process
   dispatcher for a worker that reads the same table.

## Consequences

- **Easier**: handlers are idempotent-by-design (the outbox guarantees
  at-least-once); a handler failure increments `Attempts` and retries on the
  next poll, with a configurable max-retries dead-letter.
- **Easier**: Phase 6's indexer reads the same outbox by `Sequence` —
  no duplicate event-publish path.
- **Harder**: handlers must be idempotent. SignalR broadcasts already are
  (clients handle duplicates fine); the indexer projection will need
  upserts.
- **Harder**: we own the dispatch loop, retry policy, dead-letter handling,
  observability. Acceptable in Phase 1; Phase 5+ revisits if we hit scale.

## Reversibility

The outbox table and the dispatcher are independent. Switching to
MediatR-style pub/sub means deleting the dispatcher and the override in
`LoomDbContext.SaveChangesAsync`; the table can stay (events still get
written, just not dispatched). Switching to a broker is additive: register
a `BrokerOutboxDispatcher` instead. The schema doesn't change.
