# ADR-0014 — Budget circuit breaker and multi-engine router

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom team

## Context

Phase 5 introduces the multi-engine routing layer and the cost controls that
go with it. The plan flagged budget caps as "do not cut" — without them,
an outage in the workflow engine that loops a run indefinitely turns into
a bill. Two concerns are coupled:

1. **Cost ceiling**: how does Loom refuse to spend more than $X on a project
   today, even if a workflow misbehaves?
2. **Engine selection**: when a workflow step expresses an `engine_pref`,
   how does the router walk it — and what does it do when the preferred
   engine is degraded?

The plan also defers the heaviest piece — Claude Code Headless workspace
runner — to a Phase 5b. The router needs to be designed so adding a new
engine is registration only, not surgery.

## Decision

- **`ProjectBudget`** is a per-project aggregate with a daily UTC-anchored
  spend tally and an optional `DailyCapUsd`. The aggregate's
  `TryReserve(estimatedUsd, now)` returns false when today's spend plus
  the estimate would exceed the cap; the aggregate auto-rolls over to a
  fresh day on first touch after midnight UTC. No keep-alive, no
  background reset job — the lazy roll-over avoids a clock job and is
  correct as long as something queries the budget at least once per day.
- **`IBudgetService`** wraps the repository + clock and exposes
  `TryReserveAsync` / `RecordSpendAsync` / `SetDailyCapAsync`. Project
  budget rows are created lazily on first use with no cap, so existing
  projects keep working without manual setup.
- **`RunService.QueueAsync`** calls `TryReserveAsync` before persisting
  the run and throws a domain exception if the breaker is open. The Run
  is never created — failures here are loud, not silent.
- **`RunService.CompleteAsync` / `FailAsync(partialCost: …)`** call
  `RecordSpendAsync` so today's tally tracks reality. Recording happens
  *after* `SaveChangesAsync` for the run, in a separate UoW round-trip;
  if it fails the run is still complete and the next event will retry.
- **`IAgentRouter.ResolveWithFallback(preferences)`** walks the engine
  preference list and returns the first registered + healthy runtime.
  If all preferred engines are registered but unhealthy, it returns the
  first registered one anyway — recent failure history is a hint, not
  ground truth, and a "no engines available" error would be worse for
  the user than letting the engine make its own decision.
- **`IEngineHealthMonitor`** ships as an in-memory rolling window
  (`InMemoryEngineHealthMonitor`, last 20 outcomes per engine, "unhealthy"
  if failure rate > 50% with at least 5 samples). Phase-6+ can swap in a
  Redis-backed implementation; the seam is the same.

## Alternatives considered

- **Hard $-cap with synchronous rejection at engine level**: rejected — the
  router is the wrong place to enforce cost; it has no project context.
- **Lazy budget reservation that also debits at queue time**: rejected
  because it conflates estimate with actual. Estimates are imprecise and
  reservations would drift from reality. Recording on completion is
  honest about uncertainty.
- **Background daily reset job**: rejected — adds a hosted service for
  what a single line of code in `EnsureCurrentDay` handles correctly.
- **Health monitor in MSSQL**: rejected for Phase 5a — engine health is
  hot-path data, not durable state. In-memory is faster and simpler.
- **Reject when all engines unhealthy**: rejected (see decision) — the
  engine still gets a chance; degraded ≠ broken.

## Consequences

- A misbehaving workflow that loops will hit the $-cap and stop spending.
  The PO sees a clear "circuit breaker open" signal on the Agent Activity
  page; the run is refused at queue time with a domain exception.
- Adding a new engine is two steps: implement `IAgentRuntime` in its own
  `Loom.Agents.X` project, and register it in DI. No router changes.
- Engine preference order in `WorkflowStep.EnginePref` is currently a
  single name (the existing schema). Phase 5b can extend that to a list
  without breaking callers.
- Migration `20260503173414_ProjectBudgets` is strictly additive: a new
  table with `(ProjectId)` unique index. No existing schema changes.

## Reversibility

Reversible. The budget gate is opt-in via constructor parameters on
`RunService`; tests that don't supply `IBudgetService` get the original
behaviour. The router's `ResolveWithFallback` is a new method that
co-exists with the original `Resolve(EngineName)`. A future ADR can swap
the in-memory health monitor without changing any callers.
