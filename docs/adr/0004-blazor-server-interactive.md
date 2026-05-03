# ADR-0004 — Blazor Server, interactive mode end-to-end

- **Status**: Accepted
- **Date**: 2026-05-02
- **Decider(s)**: founding contributors

## Context

The Loom Web layer needs to support real-time collaboration: presence
indicators on feature workspaces, live agent run progress, field-level
"someone is editing" hints, push notifications when gates need attention.
Blazor offers several render modes — Server, WebAssembly, Auto, plus the
mix-and-match supported by Blazor Web App in .NET 8+.

The forces at play:

- The team's stack is C# end-to-end. Sharing types between server and
  client (which Blazor enables) is genuinely valuable here; the domain
  model and DTOs need to stay coherent across boundaries.
- Real-time collaboration wants a stateful server tier. SignalR is built
  in to Blazor Server; presence and live updates come essentially for free.
- The audience is internal — small numbers of authenticated users on
  trusted networks. The latency-sensitivity arguments for WASM (low-quality
  external networks, public-facing apps) do not apply.
- Mixing render modes (Auto, per-component overrides) adds genuine
  conceptual overhead: developers must reason about whether code runs on
  the server, the client, or both, and serialisation across the boundary.

## Decision

Loom.Web uses **Blazor Server with interactive Server components** for
every page. No WebAssembly, no Auto mode, no per-component overrides.
The whole UI runs server-side; SignalR carries DOM diffs to the browser.

## Alternatives considered

**Blazor WebAssembly.** Rejected. The latency profile is wrong for
collaborative editing (every state update round-trips to the server
anyway), the cold-start cost is real, and the domain model would need to
be made WASM-compatible (no EF Core references etc.). The benefits — offline
support, lower server load — do not apply to the internal-tool audience.

**Blazor Web App with Auto / per-component render modes.** Rejected for v1.
The complexity of reasoning about where code runs, when, and what crosses
the wire is a tax on every contributor. We can introduce per-component
WASM later for specific pages (e.g. an offline read-only Operating
Picture) if a clear case arises.

**Server-rendered with HTMX / Hotwire-style interactivity.** Considered.
Would be lighter weight but loses the C# end-to-end story and the SignalR
collaboration hooks. The team's preference for a single language won out.

## Consequences

**Easier**

- Real-time features (presence, live runs, push notifications) sit
  naturally on top of the Blazor Server SignalR connection without a
  separate hub.
- Domain types are shared between repository queries and UI rendering
  without serialisation boundaries.
- Authentication is straightforward: cookie-based after OIDC sign-in.
- Hot reload during development is fast.

**Harder**

- Servers carry per-user state (the SignalR circuit). Capacity planning
  must account for concurrent users; horizontal scaling needs sticky
  sessions or a backplane.
- Connection drops require explicit reconnection UX. The default Blazor
  reconnect modal is acceptable for v1; we will customise later.
- Server-side state means a careless `OnInitializedAsync` can hold open
  resources unnecessarily. Code review must watch for this.

**New commitments**

- Pages that need long-running async work use `IDisposable` or
  `IAsyncDisposable` for cleanup.
- No `[StreamRendering]` for now — every page is fully interactive or
  fully static; we do not mix.
- Tests use `WebApplicationFactory<Program>` with the in-memory provider
  for routing/render checks; behavioural tests of components use bUnit
  (added in a later slice when the first interactive component lands).

## Reversibility

**Reversible with effort.** Switching individual pages to WebAssembly
later is a per-component change. Switching the whole app to WASM is a
significant refactor — the domain types currently reference EF Core via
the repository layer, and that boundary would have to move. Given v1
requirements, this is the right trade.
