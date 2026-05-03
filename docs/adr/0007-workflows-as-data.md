# ADR-0007 — Workflows live as data, not files

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom kernel team

## Context

Loom's design encodes the methodology in workflows: versioned DAGs of agent
+ in-proc + human-gate steps. Phase 1 ships a single hardcoded workflow
("kickoff/v1") with three steps. Phase 7 introduces the Workflow Designer,
where methodology owners author workflows through the UI without code
access. Between Phase 1 and Phase 7 we need a representation that can
support both.

Three obvious shapes for that representation:

1. C# code-as-data — a `KickoffWorkflowFactory.Build()` method.
2. YAML files in `docs/workflows/` — read at startup, persisted in memory.
3. **Workflow as a domain aggregate** persisted to MSSQL, seeded once per
   `(key, version)` and edited through application services later.

## Decision

Workflows are a domain aggregate (`Workflow` + `WorkflowStep`) persisted to
MSSQL. The `LoomBootstrapper` hosted service ensures `kickoff/v1` exists on
startup by running `KickoffWorkflowFactory.Build(now)` and saving it if the
`(key, version)` row is absent. The factory is the *seed source*, not the
*runtime source* — once seeded, the engine reads from MSSQL.

YAML editing arrives in Phase 7 as a serialiser onto the same aggregate;
the aggregate doesn't change shape.

## Alternatives considered

- **C# code-as-data only** — simplest. Rejected because Phase 7 needs to
  edit workflows without redeploys; that means a database-backed
  representation. Building it twice (once as code, once as data) is more
  work than building it once as data and seeding from code.

- **YAML in `docs/workflows/`** — clean source-controlled story but every
  edit needs a code deploy. Phase 7's "fork the kickoff workflow, swap a
  fragment selector, dry-run, publish v8" flow doesn't fit. Workflows-as-
  files also forces the Workflow Designer screen to learn about file-system
  layout, which is a leaky abstraction.

- **Hybrid (YAML files mirrored into the DB on startup)** — adds a sync
  question (who wins on conflict?) without solving the deploy problem.

## Consequences

- **Easier**: Phase 7's editing UI just calls `IWorkflowRepository.AddAsync`
  with the next version number. Versioning is per-aggregate; runs that
  reference v1 keep working when v2 is published.
- **Easier**: Per-project workflow pinning (Phase 7) is one column on a
  `WorkflowAdoption` table, queryable like any other data.
- **Harder**: workflow definitions live in two places (code factory +
  database). The bootstrapper bridges them and is the only writer in Phase 1;
  the rule is "code seeds, never overrides — database is the truth once
  bootstrap has run." Phase 7 adds the YAML serialiser and stops the
  factory.
- **Harder**: schema migrations apply to workflows. The shape (steps,
  selectors, budgets) is owned by `WorkflowConfiguration` and is
  immutable per migration once shipped.

## Reversibility

Reversible. The factory still exists, so reverting to code-as-data is
deleting the bootstrapper and reading workflows from the factory at runtime.
We'd lose Phase 7's editing capability; that's the point. Schema changes
made along the way (workflow_steps, workflow_adoption tables) would remain.
