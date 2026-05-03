# ADR-0005 — Anthropic runtime uses HttpClient, not Anthropic.SDK

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom kernel team

## Context

Phase 1 needs exactly one agent runtime: an `IAgentRuntime` that talks to the
Anthropic Messages API. The original plan named the community NuGet package
`Anthropic.SDK` (by tghamm) as the reference implementation behind a
`IAnthropicChatClient` seam. While building 1B.1 we considered the trade-off
again.

The Anthropic Messages API surface we actually use is small:
- POST `/v1/messages` with `{system, messages, max_tokens, stream: true,
  thinking?}`
- Server-Sent Events stream of `message_start`, `content_block_delta`,
  `message_delta`, `message_stop`, `error`
- A handful of token-usage and stop-reason fields

The SDK exposes batches, files, vertex variants, content-block types for
images/tools/code-execution, prompt caching, and a partial
`Microsoft.Extensions.AI.IChatClient` adapter. Phase 1 uses none of this.

## Decision

Implement `AnthropicChatClient` against `HttpClient` + `System.Text.Json`
directly, hidden behind the `IAnthropicChatClient` seam declared in
`Loom.Agents.Anthropic`. Do not take a runtime dependency on `Anthropic.SDK`.

The seam stays — `IAnthropicChatClient` keeps the door open for swapping back
to the SDK if Phase 5 needs tool-use, prompt caching, or batch endpoints.

## Alternatives considered

1. **Use `Anthropic.SDK` (tghamm)** — recommended in the original plan.
   Mature, broad surface, supports streaming and tools. Drawbacks: a thick
   public surface to keep stable across SDK minor versions, transitive
   dependencies (Microsoft.Extensions.AI in current versions), and the SDK's
   own object model (MessageParameters / MessageResponse / ContentBase) that
   would need a translation layer anyway. The SDK earns its keep when you use
   tools or batch; we don't, yet.

2. **Use the official Microsoft.Extensions.AI abstractions** — would let us
   swap chat providers more uniformly. Rejected because Phase 1 only ships
   one provider, and the abstraction adds a layer without reducing the wire
   work; we still need to translate to / from the application's
   `AssembledPrompt` and `AgentRunEvent` types. Phase 5 may revisit if the
   router routes against multiple providers via M.E.AI.

3. **Raw HttpClient (chosen)** — no transitive deps beyond what the runtime
   already uses, full control over SSE parsing, single file (`AnthropicChatClient.cs`).
   The cost is ~150 lines of stable code that we maintain ourselves; the
   benefit is no SDK-version drift.

## Consequences

- **Easier**: no third-party runtime upgrade work; the wire format is
  versioned by the `anthropic-version` request header (currently `2023-06-01`)
  and changes infrequently.
- **Harder**: tool-use, prompt caching, batch endpoints all require us to
  hand-roll the SSE / JSON shapes when we get there. Acceptable; the same
  `IAnthropicChatClient` seam abstracts the additions.
- The seam means a future swap back to `Anthropic.SDK` is a one-class change
  (`AnthropicChatClient.cs` becomes `AnthropicChatClient_Sdk.cs`).

## Reversibility

Reversible at single-class cost. Add the SDK package, write a new
`IAnthropicChatClient` impl, swap the DI registration. No data shapes leak
across the seam.
