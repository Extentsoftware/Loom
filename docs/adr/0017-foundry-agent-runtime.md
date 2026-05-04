# ADR-0017 — Foundry agent runtime targets Azure OpenAI Chat Completions

- **Status**: Accepted
- **Date**: 2026-05-04
- **Decider(s)**: Loom kernel team

## Context

ADR-0002 named **Microsoft Foundry** as the *intended* first agent runtime. In
practice we shipped `Loom.Agents.Anthropic` first (ADR-0005), validating the
`IAgentRuntime` seam against the Anthropic Messages API. Phase-5 multi-engine
routing (ADR-0014) is now in place: `DefaultAgentRouter` does multi-registration
of `IAgentRuntime`, `EngineCapabilities` lets steps express requirements, and
`Cost.Deployment` is reserved for Foundry's per-deployment billing.

Adding a Foundry runtime is registration-only. The remaining decision was
*which* Foundry surface to target. Three were on the table:

- **A. Azure OpenAI Chat Completions** (`/openai/deployments/{name}/chat/completions`).
  Stateless, OpenAI wire format, `api-key` or Entra auth.
- **B. Foundry Agent Service.** Stateful threads + runs hosted by Microsoft;
  built-in tool execution.
- **C. Foundry-hosted Anthropic Claude** (`/anthropic/v1/messages`).
  Native Anthropic Messages API, identical wire shape to ADR-0005.

The team's Azure footprint constrains the choice further: an Azure OpenAI
resource is provisioned in **France Central**, deployment name
`marketplace-prompt`, model GPT-5.4. Foundry-hosted Claude (Surface C) is gated
on East US 2 / Sweden Central — not available on the team's existing resource.

## Decision

`FoundryAgentRuntime` targets **Surface A — Azure OpenAI Chat Completions**.
The runtime POSTs to
`{Endpoint}/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}`,
parses the OpenAI SSE stream, and translates `FoundryStreamEvent` into
`AgentRunEvent` exactly the way `AnthropicAgentRuntime` does. Auth is
**API-key only** for v1 (`api-key` request header), supplied via user-secrets
in dev.

The Foundry runtime registers **alongside** the Anthropic runtime. The
`KickoffWorkflowFactory` and `EnrichmentWorkflowFactory` switch their
`WorkflowStep.EnginePref` from `Anthropic` to `Foundry`; existing workflow
versions persisted in databases keep their original pref unless re-seeded.

`Cost.Deployment` is populated from `FoundryOptions.Deployment`, satisfying
ADR-0002's per-deployment billing commitment.

## Alternatives considered

1. **Surface C (Foundry-hosted Claude)** — would have reused
   `AnthropicChatClient`'s SSE parser almost as-is (auth header swap only).
   Rejected because the team's Azure resource is in France Central where
   Foundry Claude isn't available; provisioning a separate resource in EUS2
   or Sweden Central was out of scope for v1.

2. **Surface B (Foundry Agent Service)** — server-stateful threads + runs.
   Rejected because the model would push state of record into Microsoft's
   tenant, conflicting with Loom's domain ownership of `Run` and forcing a
   sync layer for `StreamEventsAsync` resume semantics. Defer until a
   workflow specifically demands hosted MCP fan-out or Foundry-native memory.

3. **Wrap `Azure.AI.OpenAI` SDK** — rejected for the same reasons ADR-0005
   rejected `Anthropic.SDK`: the Phase-1 surface we use is small, the SDK
   adds transitive dependencies and an object model that we'd translate
   anyway, and the seam (`IFoundryChatClient`) keeps a future SDK swap a
   one-class change.

4. **Replace `Loom.Agents.Anthropic` outright** — rejected. The multi-engine
   router exists for exactly this scenario; deleting a working runtime to
   satisfy a literal reading of "instead of" loses optionality the team
   built deliberately. If after a settling period nothing pins
   anthropic.com directly, the Anthropic project can be deleted in a single
   follow-up PR.

5. **`DefaultAzureCredential` / Managed Identity for v1** — deferred. The
   team's preferred posture is API-key always (per current guidance), so
   the runtime accepts only API-key auth. Adding an Entra branch later is
   a one-class change in `FoundryChatClient`.

## Consequences

**Easier**

- Adding more Foundry deployments (a sibling resource for a different model)
  is a new options binding, not a new project.
- Per-deployment cost reporting works out of the box because `Cost.Deployment`
  is populated from configuration.
- The kickoff and enrichment paths now exercise the multi-engine router for
  the first time — validates ADR-0002's "fully reversible" claim.

**Harder**

- The OpenAI Chat Completions wire format differs from Anthropic's: there is
  no `event:` line, the terminator is `data: [DONE]`, content lives at
  `choices[0].delta.content`, finish reasons are `stop` / `length` / etc.
  The SSE parser in `FoundryChatClient` is therefore distinct code from
  `AnthropicChatClient`; this is intentional but means SSE bugs need to be
  fixed in two places.
- Pricing for GPT-5.4 in `FoundryCostCalculator` is a placeholder pending
  confirmed Foundry list price for the team's deployment. Until that lands,
  cost telemetry on Foundry runs is approximate.
- Existing seeded `Workflow` rows in production databases still pin
  `EngineName.Anthropic`. New seeds use `EngineName.Foundry`; running
  databases need re-bootstrap or workflow-version bump to pick up the
  switch. This is captured as an operational follow-up, not a code change.

**New commitments**

- The default `EnginePref` on new workflow templates is `Foundry`.
- Cost-pricing entries in `FoundryCostCalculator.Models` are kept current as
  Microsoft publishes Foundry list prices for new deployments.
- If a workflow ever needs a non-Claude *and* non-OpenAI Foundry catalogue
  model (e.g. Llama, Mistral, Phi), it routes through the same Surface A
  endpoint via a different deployment — no new runtime needed.

## Reversibility

Reversible at low cost. To swap back to Anthropic-only:

1. Change `EnginePref` on the kickoff and enrichment factories back to
   `EngineName.Anthropic`.
2. Remove the `AddFoundryAgentRuntime` registration in `Loom.Web/Program.cs`.
3. Optionally delete `Loom.Agents.Foundry` and its test project.

To swap to Surface B or C, implement a new `IFoundryChatClient` (or a peer
runtime in a sibling `Loom.Agents.Foundry.Claude` project) and register it
under a new `EngineName` value — the router accommodates many engines.
