# ADR-0009 — Inbound webhooks authenticate via HMAC, intake lives in Loom.Web

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom kernel team

## Context

Phase 2 wires Loom to Azure DevOps repos: pushes against an integrated
repo should appear in the Feature Workspace activity strip. Phase 3 adds
Atlassian / Graph; Phase 4 adds Figma; each adds an inbound webhook
contract. The choices that need pinning before we ship Phase 2:

1. **Authentication**: how does Loom verify a webhook came from the
   advertised system?
2. **Endpoint topology**: do controllers live in Loom.Web or in a new
   `Loom.Webhooks` project?

## Decision

Inbound webhooks authenticate by HMAC-SHA256 over the raw request body,
keyed against a per-`IntegrationConnection` shared secret stored in the
`integration_connections.WebhookSecret` column. The validator
(`Loom.Integrations.Webhooks.HmacSignatureValidator`) accepts both wire
shapes seen in the wild: raw base64 digest (ADO) and `sha256=<hex>`
(GitHub, Atlassian).

Intake controllers live in `Loom.Web`, under `/webhooks/{system}/{connectionId}`.
There is no separate `Loom.Webhooks` project. Dispatch logic — translating
the validated payload into domain events — lives in `Loom.Integrations`.

## Alternatives considered

- **JWT-bearer or mTLS** — strictly stronger, but requires the upstream
  system to support it. ADO and GitHub both ship HMAC out of the box;
  forcing JWT would mean a pre-deployment proxy step we're not building.
  We can layer mTLS on a per-endpoint basis later if a customer requires
  it.
- **Unauthenticated with payload-hash deduplication** — works for
  systems that won't HMAC (some Atlassian flavours), but trusts every
  POST to that URL. Reserved for a Phase-3 fallback when no other auth is
  available; never the default.
- **Separate `Loom.Webhooks` project** — would isolate the HTTP surface
  but adds another deployable. Loom.Web already hosts SignalR + MCP +
  Razor; webhooks fit alongside without surface-area cost. The dispatch
  logic is in `Loom.Integrations`, so the controllers are thin.

## Consequences

- **Easier**: every integration is configured with one row in
  `integration_connections` (owns the shared secret) and one delivery
  audit trail in `webhook_deliveries`. Operators replay failed deliveries
  by id.
- **Easier**: per-connection secret rotation is a single column update;
  nothing else needs to coordinate.
- **Harder**: HMAC requires reading the raw body, so the controller has
  to opt into request buffering (`Request.EnableBuffering`). Documented
  in the controller; future routes must do the same.
- **Harder**: because the upstream computes the HMAC over the bytes the
  network delivered, any HTTP middleware that rewrites the body (e.g.
  decompression filters) breaks validation. We pin no body-rewriting
  middleware in front of `/webhooks/*`.

## Reversibility

Reversible per integration. Switching ADO to JWT-bearer means adding a
new auth scheme and a new route alongside `/webhooks/ado/{id}`; the old
HMAC route can drop after upstream is migrated. Moving controllers to a
separate project is a refactor, not a redesign.
