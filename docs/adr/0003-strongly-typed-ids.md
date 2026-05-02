# ADR-0003 — Strongly-typed IDs and value objects in the domain

- **Status**: Accepted
- **Date**: 2026-05-02
- **Decider(s)**: founding contributors

## Context

Loom's domain model contains many similar-looking identifiers (NodeId,
FragmentId, FragmentVersionId, RunId, ArtifactId) and several
small-but-meaningful concepts (Slug, Outcome, Hypothesis, Constraint). If
all of these were modelled as `Guid` and `string`, the compiler would
silently accept passing a `RunId` where a `NodeId` was expected, and a
malformed slug where a valid one was expected.

## Decision

- Every aggregate root has a strongly-typed ID modelled as a
  `readonly record struct` wrapping a `Guid` and implementing the
  `IEntityId` marker.
- IDs are minted via `Guid.CreateVersion7()` so they sort meaningfully by
  time and play well as primary keys.
- The `Slug` value object enforces format invariants at construction and is
  used wherever a human-readable URL-safe identifier appears.
- Domain value objects (`Outcome`, `Hypothesis`, `Constraint`,
  `Stakeholder`, `EngineHints`, `Budgets`, `Cost`, `FragmentRef`,
  `CanonicalPointer`, `ToolGrant`) are modelled as `sealed record` types.
- Strongly-typed IDs are converted to `Guid` at the EF Core boundary via
  centralised `ValueConverters` declared once in
  `Loom.Infrastructure.Persistence.Configurations`.

## Alternatives considered

**Plain `Guid` everywhere.** Rejected. The class of bug it permits — passing
the wrong ID type — is exactly the kind that survives review and detonates
in production. The cost of strong typing is low (~50 lines total of small
records).

**Vogen / StronglyTypedId source generators.** Considered. They would
remove some boilerplate. Rejected for v1 because the boilerplate is small
and adding a source generator adds a debugging surface we don't need yet.
We can adopt one later if the ID list grows substantially.

**Domain entities as records throughout.** Rejected. Records make sense for
immutable values; entities have lifecycles, invariants, and behaviour. The
rule we adopt: records for value objects, classes for entities (with
private setters and behaviour-bearing methods).

## Consequences

**Easier**

- Type-checked at compile time: passing a `RunId` where a `NodeId` is
  expected fails to compile.
- Reading domain code: `node.Reparent(newParent: someParentId)` is
  unambiguous.
- Refactoring is safer; ID-swapping refactors propagate through the type
  system.

**Harder**

- EF Core configuration carries one `ValueConverter` per ID type, plus
  nullable variants. This adds maintenance surface in
  `ValueConverters.cs`.
- JSON serialisation, when added at the API or MCP boundary, will need
  per-type converters or a small generic shim. Tracked as a follow-up.

**New commitments**

- Every new aggregate root introduces an accompanying strongly-typed ID
  type and ValueConverter pair.
- Domain types must never expose mutable collections. Owned collections
  use private fields with `IReadOnlyList<T>` accessors.

## Reversibility

**Reversible.** Replacing the strong-typed IDs with plain `Guid` is a
straightforward (if tedious) refactor. The reverse — adopting strong typing
late — is harder and is exactly why we adopt it now.
