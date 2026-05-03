# ADR-0008 — MCP server hosted in-process inside Loom.Web

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom kernel team

## Context

Phase 2 of Loom exposes its node + fragment + search surface to IDE clients
(Claude Code, Cursor) over the Model Context Protocol. The official C# SDK
(`ModelContextProtocol` + `ModelContextProtocol.AspNetCore`, v1.2.0)
supports two transports: HTTP/SSE (mounted via `MapMcp`) and stdio (a
process per client).

We considered three deployment shapes:

1. **In-process inside Loom.Web** — register the MCP server in DI, mount
   transport at `/mcp`. Tools execute against the same `IFeatureService`,
   `IFragmentService`, `IFeatureNodeRepository` instances the UI uses.
2. **Separate process `Loom.Mcp.Host`** — a small ASP.NET Core host that
   only serves the MCP endpoints. Talks to the kernel via REST or shared
   DB.
3. **Stdio-only**, distributed as a CLI binary that IDE clients launch
   per-session.

## Decision

Host the MCP server in-process inside Loom.Web, exposing HTTP/SSE at
`/mcp`. The MCP tools resolve `IFeatureService`, `IFragmentService`, and
`IFeatureNodeRepository` from the same DI container as the Razor pages —
no shadow API, no second deployment.

## Alternatives considered

- **Separate process** — better isolation if the MCP surface gets DDoSed,
  but doubles deploy complexity and requires a second auth + observability
  pipeline. Phase 1 has none of those concerns; rate-limiting at the
  `/mcp` endpoint covers the realistic case. We can split out
  `Loom.Mcp.Host` later if traffic shape forces it; the `Loom.Mcp`
  assembly is already a clean library so the move is mechanical.

- **Stdio-only** — simplest auth (file-system identity) but doesn't work
  for hosted Claude Code / Cursor instances and forces us to ship a
  per-client binary. SSE handles the same sessions inside a single
  Kestrel host.

## Consequences

- **Easier**: tool handlers are async methods on attribute-decorated
  classes, discovered by `WithToolsFromAssembly`. Adding a tool is one
  file; no contract sync between processes.
- **Easier**: Phase 5's `get_workflow_state` tool ships in the same
  assembly with no plumbing change.
- **Harder**: rate-limiting is now a Loom.Web concern. We add the
  `/mcp` endpoint to whatever rate-limiter the host chooses.
- **Harder**: a runaway MCP request can starve the UI's thread pool. The
  SDK's per-request lifetime helps; observability via OpenTelemetry will
  light up cases that need attention.

## Reversibility

Reversible. `Loom.Mcp` is a class library; pulling it into a separate
ASP.NET host is a `dotnet new web` + `app.MapMcp("/mcp")` away. The DI
graph reachable from MCP tools (`IFeatureService` etc.) is the seam — if
we move to a separate process, those interfaces become a REST/gRPC client
that hits Loom.Web instead.
