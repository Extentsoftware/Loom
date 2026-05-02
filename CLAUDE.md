# Claude / AI tooling guide for the Loom repository

This file tells Claude Code, Cursor, and other AI assistants how to work in this repo. It is also mirrored to `.cursorrules` for Cursor compatibility. Eventually this file will be *generated* by Loom itself from fragment definitions; until then, edit it directly.

## What Loom is

Loom is a methodology hub for AI-augmented software development. The README has the user-facing description. The design overview (`docs/design/loom-design.md`) is the source of truth for architecture and intent.

## Architecture in one paragraph

Modular monolith, Clean / Onion shape. `Loom.Domain` has zero dependencies and contains entities, value objects, enums. `Loom.Application` orchestrates and depends only on `Loom.Domain`. `Loom.Infrastructure`, `Loom.Agents`, `Loom.Integrations`, and `Loom.Mcp` depend on `Loom.Application` via interfaces declared in `Loom.Application`. `Loom.Web` depends on `Loom.Application` and wires DI. `Loom.Contracts` carries DTOs that cross boundaries (notably the MCP server). Test projects mirror src.

## Code conventions

- **C# 13 / .NET 10**. File-scoped namespaces. Nullable reference types enabled everywhere. Treat warnings as errors.
- **Records over classes** for immutable data; classes for entities with behaviour. Use primary constructors where they read well.
- **No service locator**. Constructor injection only.
- **No static state** outside genuinely-pure helpers. Time, randomness, IDs go through abstractions (`ISystemClock`, `IGuidProvider`) so tests stay deterministic.
- **`async` all the way down**. No `.Result` or `.Wait()`. `CancellationToken` on every async public method.
- **Public surface is explicit**. Internal types stay internal. Don't widen access for tests — use `InternalsVisibleTo`.
- **EF Core** lives only in `Loom.Infrastructure`. Domain types must not reference EF. Repository interfaces are declared in `Loom.Application`; implementations live in `Loom.Infrastructure`.
- **Migrations are immutable once merged to `main`**. If a schema change is needed, add a new migration; do not edit a shipped one.
- **Tests use xUnit v3 + FluentAssertions**. Test names: `Method_State_Expected` or sentence-style for behavioural tests.
- **Domain tests** must run without any I/O — no database, no filesystem, no HTTP.
- **Infrastructure tests** that need MSSQL use Testcontainers; do not assume a local SQL Server.

## What to do before changing code

1. Read the relevant section of `docs/design/loom-design.md`. Architectural changes that contradict it require an ADR.
2. Skim recent ADRs in `docs/adr/`.
3. Look at neighbouring code first — match local conventions before introducing your own.

## What to do when adding a new feature inside this repo

1. Start in the domain. If the concept doesn't fit the existing entity model cleanly, that's a signal worth raising in PR description.
2. Application service interfaces declare the seams. Infrastructure implements them.
3. Tests first when changing domain rules; tests alongside when adding pure infrastructure plumbing.
4. Update the design doc if the change reaches outside a single feature's scope.

## Things to avoid

- **Don't** introduce new top-level projects without discussion in PR.
- **Don't** add a NuGet package without putting the version in `Directory.Packages.props`.
- **Don't** swallow exceptions. Log with structured properties (no `$"..."` interpolation in log messages — use `_logger.LogX("msg with {Param}", value)`).
- **Don't** put SQL strings in domain or application code. EF Core LINQ in infrastructure, raw SQL only when necessary and with a comment explaining why.
- **Don't** generate large speculative scaffolds. Build the smallest thing that compiles and passes tests, then iterate.

## When in doubt

Ask in PR description rather than picking silently. Loom is opinionated; AI-generated code that doesn't match the opinion will be rejected.

## Self-reference note

Loom's own development is *first-team dogfooding*: the methodology Loom encodes is the methodology used to build Loom. PR descriptions, ADRs, and commit messages are seed material for the eventual fragment library. Write them as if a future agent will read them — because one will.
