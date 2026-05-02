# ADR-0001 — Modular monolith with Clean / Onion structure

- **Status**: Accepted
- **Date**: 2026-05-02
- **Decider(s)**: founding contributors

## Context

Loom is a methodology hub for AI-augmented software development. It needs to
serve a Blazor Server UI, an MCP server, an agent runtime layer with multiple
engines, and integrations with external systems (ADO, Atlassian, Figma, Miro,
Microsoft Graph). The team is small (a single Scrum-style team) and the
domain — feature nodes, fragment libraries, run provenance — is moderately
rich but not enormous. The product also needs to evolve quickly during the
methodology-discovery phase: the data model, workflow engine, and fragment
system will all change shape multiple times before settling.

The forces at play:

- A small team cannot operate many independently-deployed services without
  drowning in operational overhead.
- The fragment system and workflow engine are pure-domain logic and benefit
  enormously from being unit-testable without spinning up a database.
- Real-time collaboration (presence, agent run streaming) wants a stateful
  server tier, which fits Blazor Server naturally.
- Multiple consumers (Blazor UI, MCP server, agent runtime callbacks) need to
  share the same domain model. Duplicating it across services would be costly
  to keep in sync.
- The codebase will be edited substantially by AI tools; clean module
  boundaries and explicit interfaces help AI agents stay within the lines.

## Decision

Loom is built as a **modular monolith** with a **Clean / Onion** dependency
shape, deployed as a single ASP.NET Core process.

Concretely:

- `Loom.Domain` — entities, value objects, domain rules. Zero dependencies.
- `Loom.Application` — service interfaces, workflow engine, fragment composition.
  Depends only on `Loom.Domain`.
- `Loom.Infrastructure` — EF Core, MSSQL, Elasticsearch, Blob. Implements
  interfaces from `Loom.Application`.
- `Loom.Agents` — `IAgentRuntime` abstraction, engine implementations, router.
  Depends on `Loom.Application`.
- `Loom.Integrations` — adapters for ADO, Atlassian, Figma, Miro, Graph.
  Depends on `Loom.Application`.
- `Loom.Mcp` — MCP server (read+write tools). Depends on `Loom.Application`.
- `Loom.Web` — Blazor Server app, SignalR hubs. Depends on `Loom.Application`
  and wires DI for the others.
- `Loom.Contracts` — shared DTOs that cross boundaries (notably the MCP
  surface). Depends on nothing.

Test projects mirror `src/`. Domain tests run without I/O. Infrastructure
tests use Testcontainers for MSSQL.

## Alternatives considered

**Microservices from day one.** Rejected. The team is too small and the
domain too unsettled. The cost — service-to-service contracts, distributed
tracing, multiple deployments, environment proliferation — would dominate.
We can carve out services later (e.g. an isolated agent runner, a separate
MCP server) once the boundaries are proven.

**A single-project, layered-by-folder structure.** Rejected. The fragment
system and workflow engine deserve to be testable without spinning up the
database, and that requires a hard separation between domain and
infrastructure. Folder discipline alone has not historically held in this
team's experience; project boundaries enforce it.

**Vertical-slice / feature-folder organisation.** Rejected for now. Vertical
slices work well when the feature shape is settled; Loom's data model is
still moving. Layering by concern keeps the domain model coherent during
this phase. We may revisit when the product stabilises.

## Consequences

**Easier**

- Pure-domain changes are testable and reviewable in isolation.
- New engine implementations slot in behind `IAgentRuntime` without touching
  workflow code.
- The MCP server and the Blazor UI naturally share the same application
  services rather than duplicating logic.
- AI tooling working in this repo has clear module boundaries to respect.

**Harder**

- Cross-cutting refactors that span domain and infrastructure require
  coordinated changes to multiple projects.
- Solution-level build times grow more than they would for a single project.
  Partial builds and `dotnet watch` mitigate this.
- New contributors must internalise where things belong (a recurring pain in
  any layered architecture). The `CLAUDE.md` and this ADR exist partly to
  reduce that cost.

**New commitments**

- Domain code must not reference EF Core, ASP.NET Core, or any infrastructure
  package. CI enforces this implicitly through `Loom.Domain.csproj` carrying
  no such references.
- Migrations live only in `Loom.Infrastructure`. Once merged to `main`, they
  are immutable; schema changes are additive.
- The `IAgentRuntime` interface in `Loom.Application` is the single seam for
  any LLM engine. Even when we have one engine, we build behind the interface.

## Reversibility

**Reversible with effort.** Splitting the monolith into services later is a
larger project than starting with services would have been, but the module
boundaries we keep clean make it tractable. Collapsing the layered structure
into a single project is trivial and would be the right move if the team
ever halves in size or the product narrows dramatically.
