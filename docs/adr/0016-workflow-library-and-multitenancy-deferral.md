# ADR-0016 — Workflow Library, and the deferral of multi-tenancy + visual designer

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom team

## Context

Phase 7 of the master plan gathers three large work-streams: a Workflow
Designer (Monaco YAML + read-only DAG view), workflow dry-run +
fragment-review tooling, and multi-tenancy with row-level security.
Pressure cut #1 in the plan removes the visual DAG view, saving ~1 week.
The plan also notes multi-tenancy retrofit is bounded but not free
("retrofitting RLS into a year-old single-tenant system is painful and
dangerous; the cost in Phase 7 is bounded ~1 week").

Two questions at this point in the project:
1. What's the smallest Phase-7 surface that ties off the design's
   methodology-owner story?
2. When does multi-tenancy land — now, or as a deliberate Phase 7b?

## Decision

Ship a **Phase 7a** that closes out the read-side of methodology
ownership and explicitly defers two deeper pieces:

- **Workflow Library page** at `/workflows` — lists every workflow
  registered in the database (key, version, title, step count, created
  date). The bootstrapper currently seeds `kickoff/v1` and
  `enrichment/v1`; Phase-7b's editor adds new ones from the UI.
- **Workflow Detail page** at `/workflows/{WorkflowId:guid}` — read-
  only inspector showing every step's kind, gating role, engine
  preference, output schema, budgets, and the full fragment-selector
  list that contributes to the assembled prompt. This is the "show me
  what kickoff actually does" page methodology owners need before
  they're ready to fork it.
- **Workflow dry-run** — deferred. The page would compose prompts
  against a sample transcript without side effects. The infrastructure
  (`AssembledPromptComposer` already exists) means it's a small UI;
  but it's UI work that should land alongside the editable view, not
  alone.

**Defer to Phase 7b**:
- Editable Workflow Designer (Monaco YAML, fragment selector
  autocomplete, per-project version pinning).
- Read-only DAG view (cut #1).
- Multi-tenant row-level security: `tenant_id` columns on every
  aggregate root, EF global query filters, RLS predicates, support
  "switch tenant" surface, default-tenant backfill.
- Annotation-to-fragment promotion flow (the Phase-4 schema is in
  place; the workflow that turns recurring annotations into proposed
  feedback fragments needs the editor).
- Fragment review surface with usage stats from a Phase-6b ES index.

## Alternatives considered

- **Ship Phase 7 in one piece**: rejected per pressure cut #1 and the
  user's stated frustration with sprawl. Multi-tenancy alone is a
  full week of careful retrofit work; coupling it to the designer
  multiplies risk.
- **Skip Phase 7 entirely**: rejected — the methodology-owner story is
  the design's stated v1 goal, and a read-only library is small.
- **Multi-tenancy now, designer later**: considered. The argument is
  that retrofit cost grows with time. We chose deferral on the grounds
  that v1 is single-tenant, the schema retrofit is the bulk of the
  work (data is contained, no production migration to plan), and the
  cost ceiling is the same whether we land it in 7b or in a v1.5.
- **Designer as Razor with no Monaco**: an option for 7b. Default
  textarea editing of YAML works; Monaco is polish, not capability.
  Decision lives in the 7b ADR.

## Consequences

- A methodology owner can navigate `/workflows → /workflows/{id}` and
  see exactly which fragments compose into each step. Combined with
  the existing Fragment Library this gives a complete read-only
  picture of "what the agents will do".
- The workflow-as-data architecture pays off here: zero new domain
  changes were needed, only two Razor pages.
- Phase 7b stays well-scoped: editor + dry-run + multi-tenancy.
  Annotation-to-fragment promotion can ride along or split out further
  depending on time.
- The product can ship a Phase-7a "v1 candidate" once Phase 5b's
  deferred work (Foundry/Claude Code Headless runners) lands; nothing
  in the Phase-7a slice blocks a release.

## Reversibility

Trivially reversible. Both pages are read-only Razor components against
existing application surfaces; deleting them is harmless. The deferred
multi-tenancy work is an additive schema change when it does land — the
retrofit cost is the same in 7b as it would have been in 7a.
