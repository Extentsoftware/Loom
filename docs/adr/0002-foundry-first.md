# ADR-0002 — Azure AI Foundry as the first agent runtime

- **Status**: Accepted
- **Date**: 2026-05-02
- **Decider(s)**: founding contributors

## Context

Loom needs at least one agent runtime to ship Phase 1 (the kickoff workflow
with PO gate). The `IAgentRuntime` abstraction in `Loom.Application` will
eventually have multiple implementations (Foundry, Anthropic API, headless
Claude Code, in-proc lightweight calls), but the team must pick a first one
to actually build against.

The forces at play:

- The team's stack is heavily Microsoft-centric: Azure DevOps, Microsoft
  365 / Teams, MSSQL, Entra ID. Source meeting transcripts and project
  context live there.
- Data residency and audit are concerns for some projects whose transcripts
  may contain confidential business content.
- Foundry is Entra-native; user identity and tool authorisation flow without
  a separate token model.
- Foundry's MCP-style tool-use surface differs in shape from Anthropic's
  native MCP integration. Building against either first means the second
  needs an adapter regardless.
- Code-heavy slice work (multi-file edits with bash/lint/test loops) is a
  weaker fit for Foundry-hosted models than for purpose-built agentic
  runtimes; this gap is acknowledged and accepted for v1, not solved.

## Decision

`FoundryAgentRuntime` is the first concrete `IAgentRuntime` implementation.
Phase 1 ships with Foundry as the only engine. The abstraction is built
even with one implementation, so a second engine can be added without
rewriting calling code.

## Alternatives considered

**Anthropic API first.** Rejected for v1 not because the model is weaker —
arguably it isn't, particularly on reasoning-heavy steps — but because the
operational and identity overhead of running it alongside an otherwise
all-Microsoft stack costs more than the model gain. Will be added in
Phase 5 specifically for code-heavy work via headless Claude Code.

**Both engines in parallel from day one.** Rejected. Building two
implementations doubles the integration surface before the abstraction has
been validated against a single concrete engine. The team will learn more
from running one, finding its sharp edges, and *then* designing the second.

**In-proc lightweight calls (no external engine).** Rejected as a primary
runtime — it cannot reason at the level the kickoff workflow needs. Will
be added later for cheap, high-volume tasks (digests, normalisation).

## Consequences

**Easier**

- Entra-native auth end-to-end. No separate LLM credential model.
- Data residency is a non-question: stays in the Azure tenant.
- Audit and compliance posture inherit from Azure tenant policy.
- Microsoft Graph access (Teams transcripts, SharePoint) flows from the same
  identity as the agent run.

**Harder**

- Code-heavy slice work in Phase 4 will feel weaker than it could until
  Phase 5 brings in Claude Code Headless. We accept this trade.
- Foundry's tool-use surface needs adapter code inside `FoundryAgentRuntime`
  to map our `ToolGrant` / `RunEvent` types to the engine-native shape.
  This adapter pattern is the template for future engines.

**New commitments**

- The `IAgentRuntime` interface must stay engine-agnostic in its shape, even
  while Foundry is the only consumer. Engine-specific concepts (Foundry
  thread IDs, Anthropic message IDs, Claude Code session paths) are mapped
  to a normalised `RunEvent` stream owned by Loom.
- Cost telemetry on `Run.Cost` carries both `Model` and `Deployment` because
  Foundry billing is per-deployment.

## Reversibility

**Fully reversible.** Adding the second engine is exactly the test of the
abstraction. If the abstraction holds, Phase 5 swaps in Anthropic without
disruption. If it doesn't, we learn something useful about where the seam
should actually be.
