# Loom

*A methodology hub for AI-augmented software development, from concept to production.*

Loom is the connective tissue between humans and AI tools across the full lifecycle of a feature. It treats every feature as a living **context node** with structured intent, dynamic artifacts, an activity stream, and a fragment-assembled prompt environment that any AI tool — Claude, Cursor, Foundry-hosted agents, headless Claude Code — can read from and contribute back to.

## What problem this solves

AI tools today create private context bubbles: the PO's ChatPRD session, the dev's Cursor session, the tester's Claude conversation each hold context the others can't see. Loom is the shared substrate. Humans collaborate in their preferred tools (Figma, Visual Studio, Teams, ADO) while Loom keeps the *context*, the *provenance*, and the *methodology* coherent across them.

The product replaces three things:

1. **Private AI conversations no one else benefits from.** Loom captures the assembled prompt, fragments used, and outputs against a feature node so every run informs the next.
2. **Ticket-board ceremony that fragments thinking.** Features live as a hierarchical tree (initiative ▸ feature ▸ capability ▸ slice). Status is *derived*, not dragged.
3. **Manual labour keeping PRDs, designs, code, and tests aligned.** Each artifact has a canonical home (Figma, git, Confluence, Loom-native) and Loom is the index, not a duplicate store.

## Lifecycle in one diagram

```
Teams transcript → kickoff (extract, decompose) → ◇ PO gate ◇
   → enrich (acceptance, UX, arch, risks) → ◇ role gates ◇
   → build (Cursor / Claude Code via MCP, Foundry background)
   → test → merge → deploy → feedback fragments
```

Two human gates only. Everything else automated but auditable.

## Repository layout

```
src/
  Loom.Domain          entities, value objects, no dependencies
  Loom.Application     services, workflow engine, fragment composition
  Loom.Infrastructure  EF Core, MSSQL, Elasticsearch, Blob
  Loom.Agents          IAgentRuntime, FoundryAgentRuntime, router
  Loom.Integrations    ADO, Atlassian, Figma, Miro, Graph adapters
  Loom.Mcp             MCP server (read+write tools)
  Loom.Web             Blazor Server app, SignalR hubs
  Loom.Contracts       shared DTOs, MCP tool schemas

tests/                 mirrors src/ structure
docs/
  adr/                 architecture decision records
  design/              design overview and screen mockups
deploy/                bicep + docker
```

## Getting started

**Prerequisites**

- .NET 10 SDK (see `global.json`)
- SQL Server (LocalDB on Windows, or `mcr.microsoft.com/mssql/server` via Docker)
- An IDE: Visual Studio, Rider, or VS Code with the C# Dev Kit

**Build and test**

```bash
dotnet restore
dotnet build
dotnet test
```

**Apply database migrations**

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Loom.Infrastructure --startup-project src/Loom.Web
```

**Seed sample data (optional, for local development)**

```bash
dotnet run --project tools/seed
```

**Run the web app**

```bash
dotnet run --project src/Loom.Web
```

By default this listens on `https://localhost:5001`. In Development the
Entra ID auth flow is bypassed (`AzureAd:Enabled = false` in
`appsettings.Development.json`); for any other environment, fill in the
`AzureAd` section in configuration with a real tenant id, client id, and
domain before running.

## Working with AI tools in this repo

This repo is designed to be edited with AI assistance. See `CLAUDE.md` at the root — it describes how Claude Code, Cursor, and other AI tools should treat the codebase. The same content is mirrored to `.cursorrules` for Cursor users.

When AI-assisted changes warrant it, record an architecture decision under `docs/adr/`. Decisions about *methodology* (how the team works) belong in fragment definitions inside Loom itself; decisions about the *codebase* (how Loom is built) belong as ADRs here.

## Project status

Pre-alpha. See `docs/design/loom-design.md` for the full design overview and `docs/design/loom-screens.html` for screen drafts. Build sequence is in the design doc — Phase 1 is the kernel (current).

## License

Proprietary, internal use.
