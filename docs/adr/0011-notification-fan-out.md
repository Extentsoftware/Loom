# ADR-0011 — Notification fan-out via subscription rows + channel seam

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom team

## Context

Phase 3 needs to notify users when something they care about happens on a node:
a run paused at a gate, a run completed, a run failed, a node was edited.
The design (§10) calls for in-app, Teams, and email channels, with per-user
preferences for cadence (realtime vs digest). The product hinges on these
arriving promptly and reliably; if a PO misses a gate notification the whole
workflow stalls.

There are three orthogonal concerns:
1. *What* events flow through the notification path.
2. *Who* gets notified for a given event.
3. *Through which channel* delivery happens.

The kernel already emits domain events on the outbox. The question is how to
turn an outbox event into zero-or-more channel sends without coupling domain
code to channel implementations.

## Decision

- A relational `subscriptions` table holds per-user, per-node, per-event-type,
  per-channel rows with a `mode` (Realtime | Digest). The compound key
  `(UserId, NodeId, EventType, Channel)` is unique — one row decides delivery.
- `INotificationChannel` is a per-channel seam. Implementations register in
  DI; `NotificationService` discovers them via `IEnumerable<INotificationChannel>`
  and dispatches by enum.
- Outbox handlers (`NodeUpdatedNotificationHandler`,
  `RunPausedNotificationHandler`, etc.) are the *only* code that calls
  `INotificationService.DispatchAsync`. Domain code remains ignorant of
  channels and subscriptions.
- Phase 3 ships in-app (SignalR via `InAppNotificationChannel`) plus stubs
  for Teams and email. Stubs log + no-op so a Phase-5 swap-in is local.
- Digest-mode rows are skipped by the realtime path; a Phase-3.5 cron will
  read them in batches.

## Alternatives considered

- **MediatR + handler chain**: extra abstraction, no real benefit when the
  outbox already exists.
- **Configuration-file subscriptions**: rejected — users need to edit their
  own preferences in-app.
- **One handler per event type per channel**: combinatorial explosion; the
  channel seam keeps the matrix at events × 1.
- **Push to a queue (Service Bus)**: real option for Phase 6+ when the
  indexer also goes async, but premature now — the in-process dispatcher
  already lets us scale by handler count.

## Consequences

- Adding a new channel is a single `INotificationChannel` impl plus DI line.
- Adding a new event type is one outbox handler that calls
  `INotificationService.DispatchAsync` with a payload.
- The schema couples notification behaviour to nodes (good — that's the
  domain), so cross-cutting subscriptions (e.g. "all runs on the project")
  need explicit fan-out at write time. Acceptable for Phase 3.
- Digest delivery is deferred but the data model already supports it.

## Reversibility

Reversible. The seam is internal to `Loom.Application`; channels are
swappable. The subscription schema is additive — we can split it per channel
later without data loss.
