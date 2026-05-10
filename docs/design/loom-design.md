# Loom — Design Overview

*Working name. A methodology hub for AI-augmented software development, from concept to production.*

---

## 1. Vision

Loom is the connective tissue between humans and AI tools across the full lifecycle of a software feature. It treats every feature as a living **context node** with structured intent, dynamic artifacts, an activity stream, and a fragment-assembled prompt environment that any AI tool — Claude, Cursor, ChatPRD, Foundry-hosted agents, headless Claude Code — can read from and contribute back to.

The methodology is not a process diagram printed on a poster. It is encoded directly in the data model, the workflows, and the fragment library. Loom is opinionated about how features are framed, decomposed, validated, and delivered, and it makes those opinions executable.

The product replaces three things: the proliferation of private AI conversations no one else can see, the ticket-board ceremony that fragments thinking into cards, and the manual labour of keeping PRDs, designs, code, and tests aligned with each other.

## 2. The lifecycle in one diagram

```
                    ┌─ TEAMS TRANSCRIPT ─┐
                    │     (or paste)     │
                    └─────────┬──────────┘
                              ▼
       ┌──────────────────────────────────────────┐
       │ KICKOFF                                   │
       │  normalize → extract → memory → decompose │
       └─────────────────────┬─────────────────────┘
                             ▼
                       ◇ PO REVIEW ◇
                             │
                             ▼
       ┌─────────────────────────────────────────┐
       │ FEATURE TREE                             │
       │   initiative ▸ feature ▸ capability ▸ slice
       └─────────────────────┬─────────────────────┘
                             ▼
       ┌─────────────────────────────────────────┐
       │ ENRICH (per node, parallel)              │
       │  acceptance · UX flows · arch · risks    │
       └─────────────────────┬─────────────────────┘
                             ▼
                ◇ ROLE GATES (UX, tech lead) ◇
                             │
                             ▼
       ┌─────────────────────────────────────────┐
       │ BUILD                                    │
       │  Cursor / Claude Code via MCP            │
       │  background agents via Foundry / API     │
       └─────────────────────┬─────────────────────┘
                             ▼
       ┌─────────────────────────────────────────┐
       │ TEST · MERGE · DEPLOY                    │
       │  ADO Pipelines · webhooks → Loom         │
       └─────────────────────┬─────────────────────┘
                             ▼
                       PRODUCTION
                             │
                             ▼
              FEEDBACK FRAGMENTS → library
```

Two human gates only. Everything else is automated but auditable. Every artifact carries provenance back to the fragments and inputs that produced it.

## 3. Users and primary jobs

| Role | Primary job in Loom | Lives in |
|---|---|---|
| **Product Owner** | Kicks off features from meetings, reviews proposals, owns intent | Loom UI, Teams, Confluence |
| **UX Designer** | Validates AI-generated flows, annotates wireframes, curates design fragments | Figma/Penpot, Loom UI |
| **Developer** | Pulls slice context into IDE, contributes code and edits artifacts | Visual Studio / Rider / Cursor / Claude Code, Loom MCP |
| **Tester** | Generates and validates test plans, traces tests to acceptance criteria | ADO Test Plans, Loom UI |
| **Tech Lead / Architect** | Reviews architecture sketches, owns project fragments and ADRs | Loom UI, git |
| **Methodology Owner** | Curates the fragment library, evolves the workflows | Loom UI |

Roles are not exclusive — a small team has people wearing several. Loom never enforces a role; it offers role-flavoured views and surfaces appropriate notifications.

## 4. System architecture

Layered architecture with strict separation between context, orchestration, and execution. The same data is exposed to humans (Blazor UI), to agents inside IDEs (MCP server), and to background runs (workflow engine + agent runtime).

```
┌──────────────────────────────────────────────────────────────────┐
│  PRESENTATION                                                      │
│   Blazor Server UI · SignalR hubs · Teams/Slack notifiers         │
├──────────────────────────────────────────────────────────────────┤
│  APPLICATION                                                       │
│   Feature service · Workflow engine · Fragment service ·          │
│   Run service · Artifact service · Notification service           │
├──────────────────────────────────────────────────────────────────┤
│  INTEGRATION                                                       │
│   MCP Server (read+write) · MCP Client orchestrator ·             │
│   Webhook intake · External adapters (ADO, Atlassian, Figma,      │
│   Miro, Graph/Teams, Confluence)                                  │
├──────────────────────────────────────────────────────────────────┤
│  AGENT RUNTIME                                                     │
│   IAgentRuntime abstraction · Router · Implementations:           │
│   Anthropic API · Foundry · Headless Claude Code · in-proc        │
├──────────────────────────────────────────────────────────────────┤
│  PERSISTENCE                                                       │
│   MSSQL (state) · Elasticsearch (search/memory) ·                 │
│   Blob (artifacts/transcripts) · Outbox (events)                  │
└──────────────────────────────────────────────────────────────────┘
```

### Why Blazor Server
SignalR is built in, presence and live updates come essentially for free, a single C# codebase shares types with the rest of the system, and the latency profile (server-rendered with streaming updates) suits collaborative editing of structured data better than a SPA. WASM is a future option for offline read; not needed initially.

### Cross-cutting concerns
- **Identity**: Microsoft Entra ID (Azure AD). Every Loom action runs as a known user; Loom never escalates beyond the user's own access on connected systems.
- **Audit trail**: every state change writes to an outbox; outbox publishes to an event log table and to the search index.
- **Tenancy**: single-tenant per project initially. Multi-tenant later via row-level security on the project key.
- **Configuration**: per-environment via Azure App Configuration; secrets in Key Vault.

## 5. Domain model

Eleven entities. The data model is deliberately small — most behaviour comes from how these compose, not from many specialized types.

```
Project ───┬─< FeatureNode (recursive: parent_id) >─── Artifact
           │            │                                  │
           │            │                                  └─< ArtifactVersion
           │            │
           │            ├─< NodeFragment (link)
           │            ├─< Run >─< RunEvent
           │            ├─< Annotation
           │            └─< Conversation >─< Message
           │
           ├─< Fragment >─< FragmentVersion
           │                       │
           └─< Workflow            └── tags, scope, owner
                  │
                  └─< WorkflowStep
```

### FeatureNode
The unit of context. Same shape at every level (initiative / feature / capability / slice). Hierarchy via `parent_id`, type via enum.

The four levels are not interchangeable — each one names a different thing:

- **Initiative** — top-level intent for a project, theme, or quarter outcome. Sits directly under a Project. *"Make checkout faster."*
- **Feature** — a coherent capability bundle delivered to users; child of an Initiative (or directly under a Project for one-off work). *"Express checkout."*
- **Capability** — a discrete behaviour or system competence required by a feature; child of a Feature. *"Saved-card surfacing."* Capabilities never sit beside features as peers — if a proposal puts them at the same level, decompose ran wrong.
- **Slice** — a thin, end-to-end deliverable that exercises one capability; child of a Capability. The unit a developer pair picks up. *"Render saved card on the checkout page (read-only)."*

Decompose steps must respect this nesting: a feature decomposition produces capabilities under that feature, not capability siblings of it.

```csharp
public record FeatureNode(
    Guid Id,
    string Slug,
    Guid? ParentId,
    NodeType Type,
    string Title,
    string? Intent,
    IReadOnlyList<Outcome> Outcomes,
    IReadOnlyList<Hypothesis> Hypotheses,
    IReadOnlyList<Constraint> Constraints,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<Guid> Stakeholders,
    Phase Phase,                    // discovery | enrich | build | test | done
    StatusSignals Status,           // derived
    Guid OwnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Status is **derived**, never set. It composes from child phases, recent run states, gate validations, and recency. This is how the system avoids ticket-board theatre: nothing's status moves because someone dragged a card.

### Fragment
A small, single-purpose chunk of prompt content with metadata.

```csharp
public record Fragment(
    Guid Id,
    string Key,                     // "identity:po-discovery-assistant"
    FragmentCategory Category,      // identity | methodology | project | domain | skill | context | feedback
    string Title,
    IReadOnlyList<string> Tags,
    FragmentScope Scope,            // global | project | node
    Guid? ScopeId,
    Guid OwnerId,
    Guid? CurrentVersionId);

public record FragmentVersion(
    Guid Id,
    Guid FragmentId,
    int Version,
    string Content,                 // the actual prompt text
    OutputSchema? OutputSchema,     // optional JSON schema
    EngineHints EngineHints,        // prefers_extended_thinking, max_context, etc.
    string ChangeNote,
    Guid AuthorId,
    DateTimeOffset CreatedAt,
    bool IsDeprecated);
```

Scope + inheritance: a node assembles its effective fragment set by walking from project → parent chain → self, applying the fragments declared at each level. Local overrides win over inherited.

### Run
A single agent execution against a node. The run record is the durable provenance object.

```csharp
public record Run(
    Guid Id,
    Guid NodeId,
    Guid WorkflowId,
    Guid StepId,
    EngineName Engine,              // anthropic | foundry | claude_code | inproc
    string ExternalRunId,           // engine's native id
    AssembledPrompt Prompt,         // serialized fragments + content
    IReadOnlyList<ToolGrant> Tools,
    Budgets Budgets,
    RunState State,                 // queued, running, paused_for_human, completed, failed, cancelled
    IReadOnlyList<Guid> ProducedArtifacts,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    Cost Cost);
```

### Artifact
A pointer to canonical content plus a cached preview. Artifacts have versions; versions track human vs agent authorship.

```csharp
public record Artifact(
    Guid Id,
    Guid NodeId,
    ArtifactKind Kind,              // criteria | wireframe | code | diagram | test_plan | adr | doc
    Urn Urn,                        // hub://feature/{node}/artifact/{id}
    CanonicalPointer Canonical,     // figma | git | miro | confluence | hub_native
    Lock? Lock,
    DateTimeOffset LastSyncedAt,
    Guid? CurrentVersionId);

public record ArtifactVersion(
    Guid Id,
    Guid ArtifactId,
    int Version,
    Author Author,                  // (kind: human|agent, id: userId|runId)
    string? Reason,                 // human-supplied edit reason
    BlobRef ContentRef,
    BlobRef? PreviewRef);
```

### Annotation
Structured feedback attached to an artifact. Annotations get promoted into feedback fragments when re-running.

### Conversation / Message
Persisted AI conversations associated with a node. Lets a follow-on agent or human read what was discussed earlier without re-prompting.

### Workflow / WorkflowStep
A workflow is a DAG of steps. A step declares: identity, fragment selectors (by tag), runtime requirements, output schema, gating (auto / human-required). Workflows are versioned like fragments.

## 6. Component breakdown

### Application services

| Service | Responsibility | Key collaborators |
|---|---|---|
| **FeatureService** | CRUD on nodes, hierarchy operations (split/merge/promote), phase transitions | MSSQL, EventOutbox |
| **FragmentService** | Fragment library, versioning, scope/inheritance resolution, compilation to flat files (CLAUDE.md, .cursorrules) | MSSQL, GitSyncService |
| **WorkflowEngine** | Executes workflow DAGs, manages step gating, persists Run records | RunService, AgentRouter |
| **RunService** | Run lifecycle, event ingestion from engines, heartbeat watchdog, cost tracking | AgentRuntime impls |
| **ArtifactService** | Artifact descriptors, lock management, canonical sync, version history | External adapters, Blob |
| **NotificationService** | Subscriptions, digest batching, fan-out to Teams/Slack/SignalR | SignalR, Graph API |
| **MemoryService** | Indexes nodes, runs, artifacts into Elasticsearch; serves similarity queries | Elasticsearch |
| **AuthorizationService** | Per-node ACLs, role-based capabilities, broker-tokens for external systems | Entra |

### Integration components

| Component | Direction | Purpose |
|---|---|---|
| **MCP Server** | inbound | Exposes Loom context to Claude/Cursor; read and write tools |
| **MCP Client orchestrator** | outbound | Calls Atlassian/ADO/GitHub MCP servers from agent runs |
| **Webhook Intake** | inbound | Receives engine progress, ADO events, Figma plugin events |
| **External adapters** | outbound | ADO REST, Confluence, Figma, Miro, Microsoft Graph |
| **Git Sync** | bidirectional | Compiles fragments to repo files; reads repo state for code artifacts |

### Agent runtime layer

```csharp
public interface IAgentRuntime {
    string Name { get; }
    EngineCapabilities Capabilities { get; }
    Task<RunHandle> StartAsync(AgentRunRequest req, CancellationToken ct);
    Task CancelAsync(string runId);
    IAsyncEnumerable<RunEvent> StreamEventsAsync(string runId);
}
```

Implementations: `AnthropicAgentRuntime`, `FoundryAgentRuntime`, `ClaudeCodeHeadlessRuntime`, `InProcLlmRuntime`. The router picks per-step based on declared requirements + current capacity + cost budgets.

## 7. The fragment system in depth

The fragment system is the most distinctive part of Loom. Get it right and the whole methodology compounds; get it wrong and you have a wiki with extra steps.

### Categories
- **identity** — *who* the agent is acting as ("PO discovery assistant", "frontend dev")
- **methodology** — *how* this team thinks (problem framing, definition of ready/done)
- **project** — stack conventions, repo layout, design tokens, naming
- **domain** — business glossary, personas, regulatory rules
- **skill** — discrete capabilities ("extract acceptance criteria", "write ADR")
- **context** — live node content (auto-injected, not authored)
- **feedback** — annotations from validation gates (auto-derived from human input)

### Scope and inheritance
- **global** — applies to all projects
- **project** — applies within one project
- **node** — applies to one node and its descendants

Effective fragment set at a node = global fragments selected by step + project fragments selected by step + ancestor node-scoped fragments + this-node-scoped fragments + auto-injected context/feedback fragments.

### Compilation surfaces
The same fragment library serves three surfaces:

1. **MCP `get_node_context`** — agents call this and receive the assembled prompt for their step
2. **`CLAUDE.md` and `.cursorrules`** — git-sync writes compiled views into each repo
3. **Loom UI editor** — humans browse, edit, version fragments

### Versioning rules
- Fragments are immutable per version; "edit" creates a new version
- Runs record the exact version IDs they used
- Replay re-runs against either the same or latest versions and shows a diff
- Deprecation marks a version as "no longer recommended" but keeps it readable

### Curation discipline
- Every fragment has a category owner
- Quarterly fragment review: which are unused, which are duplicated, which produced consistently bad output
- Annotations from validation gates that recur across N features auto-propose new fragments to the owner

## 8. Workflow engine

Workflows are versioned, declarative, and executed step-by-step.

```yaml
id: workflow.kickoff
version: 7
steps:
  - id: normalize
    runtime: { engine_pref: [inproc, anthropic_haiku], expected_runtime_s: 5 }
    fragments: [skill:transcript-normalize]
    output_schema: TranscriptTurns
    gating: auto

  - id: discovery
    runtime: { engine_pref: [anthropic, foundry], reasoning: medium }
    fragments:
      - identity:po-discovery-assistant
      - methodology:problem-framing
      - methodology:definition-of-ready
      - domain:product-glossary
      - skill:transcript-discovery-extraction
    output_schema: DiscoveryObject
    gating: auto

  - id: memory_lookup
    runtime: { engine_pref: [inproc] }
    fragments: [skill:similar-feature-search]
    inputs: [{ from: discovery, field: problem_statement }]
    output_schema: SimilarityReport
    gating: auto

  - id: decompose
    runtime: { engine_pref: [anthropic], reasoning: high }
    fragments:
      - identity:po-architect-pair
      - skill:feature-decomposition
    inputs: [discovery, memory_lookup]
    output_schema: DecompositionProposal
    gating: human:po
```

Gates pause the run and notify the assigned human. Resuming requires explicit acceptance with optional edits — every edit is recorded against the run.

## 9. MCP surface

### Read tools (consumed by Claude/Cursor in IDEs)
- `loom.get_node_context(node_ref)` — full assembled context including parent inheritance, fragments, recent decisions
- `loom.search_nodes(query)` — semantic search over the project
- `loom.get_artifact(urn)` — canonical content + preview + version history
- `loom.list_rules(scope)` — compiled rules for a scope (returns the same content as `.cursorrules`)
- `loom.get_workflow_state(node_ref)` — what's running, what's gated, what's done

### Write tools (require auth scope, optional human-confirm)
- `loom.update_node_field(node_ref, field, value, reason)` — edits with audit
- `loom.attach_artifact(node_ref, urn, kind)` — link external artifact
- `loom.submit_artifact_revision(urn, content, reason)` — push human-mediated change
- `loom.add_annotation(artifact_urn, body, target)` — structured feedback
- `loom.advance_phase(node_ref, to_phase, justification)` — gated phase transition
- `loom.propose_fragment(category, content, rationale)` — suggest new fragment from observed pattern

Auth: OAuth 2.0 with PKCE, scopes per tool family, tokens bound to user identity.

## 10. Real-time and notifications

SignalR hubs:
- **NodeHub** (`/hubs/node/{id}`) — presence, field-level concurrent edit indicators, artifact updates, run events
- **AgentHub** (`/hubs/agents`) — global agent activity stream for ops view
- **NotificationHub** (`/hubs/me`) — per-user notification feed

Notification rules:
- Default to **digest** (one summary per node per hour to subscribers)
- Real-time pings opt-in per node, per role, per event type
- Cross-channel delivery: in-app feed, Teams card via Graph, optional email
- Run completion notifications include the artifact URN, the diff summary, and a one-click review link

## 11. Persistence

| Store | What lives here | Why |
|---|---|---|
| **MSSQL** | Nodes, fragments, runs, artifacts, annotations, ACLs | Strong consistency, transactional, joinable |
| **Elasticsearch** | Indexed nodes, run summaries, ADRs, full-text artifact previews | Memory lookup, similarity, full-text |
| **Azure Blob** | Transcripts, artifact content, AI conversation captures, large preview images | Cheap, append-only, lifecycle policies |
| **Outbox table** | Domain events for reliable fan-out | At-least-once event publishing |

Indexing pipeline: domain event → outbox → background processor → Elasticsearch. Search queries always go through MemoryService, never direct ES access from UI.

## 12. Auth and security

- **Identity**: Entra ID (Azure AD); SSO via OIDC
- **Authorization model**: project-level membership + per-node ACL + role-based capabilities
- **External tokens**: Loom never stores user passwords; OAuth tokens for Figma, ADO, Atlassian stored encrypted in Key Vault, refreshed per-user
- **MCP auth**: OAuth 2.0 PKCE for IDE clients; tokens scoped to user + workspace
- **Audit**: every action recorded with actor (human or agent run), timestamp, before/after for state changes
- **Data classification**: transcripts and conversations may contain confidential business content; classification tag drives storage region and engine routing (e.g., confidential → Azure Foundry only)
- **Prompt injection defence**: agent outputs are treated as untrusted; write actions through MCP require explicit user confirmation in UI for sensitive operations (phase advance, deletions)

## 13. External integrations

| System | Direction | Mechanism |
|---|---|---|
| **Microsoft Teams** | inbound (transcripts), outbound (notifications) | Graph API |
| **Microsoft 365 / SharePoint** | bidirectional | Graph API |
| **Azure DevOps** | bidirectional (work items, repos, pipelines, test plans) | ADO REST + webhooks + ADO MCP server |
| **Atlassian (Jira, Confluence)** | bidirectional | Atlassian MCP server + REST |
| **Figma** | bidirectional via plugin + REST | Figma plugin (custom) + REST |
| **Miro** | bidirectional | Miro REST + webhooks |
| **GitHub** | optional | GitHub MCP server |
| **Slack** | outbound notifications (optional) | Webhooks |

## 14. Screen inventory

The first four are mocked separately. The remainder are described.

| # | Screen | Primary user | Key data |
|---|---|---|---|
| 1 | **Operating Picture** (tree home) | All | Hierarchical tree, status pulses, recent runs |
| 2 | **Feature Workspace** | Node owner + collaborators | Intent, outcomes, children, artifacts, runs, conversation |
| 3 | **Kickoff & PO Gate** | Product Owner | Transcript, discovery object, proposed tree |
| 4 | **Fragment Library** | Methodology owner, all editors | Categories, scopes, versions, usage stats |
| 5 | Run Detail / Provenance | All | Assembled prompt, fragments used, inputs, outputs, timeline, cost |
| 6 | Artifact Inspector | UX, Dev, Tester | Canonical pointer, version history, annotations, lock state |
| 7 | Workflow Designer | Methodology owner | DAG editor, step config, fragment selectors, dry-run |
| 8 | Agent Activity | Ops, lead | Live stream of all runs, queue depth, engine load, cost burn |
| 9 | Settings & Integrations | Admin | Connections, tokens, webhook health, engine credentials |
| 10 | Notification Centre | All | Inbox view, filters, digest preview |

## 15. Build sequence

Each phase ships standalone value; the team can stop after any of them and still benefit.

### Phase 1 — The kernel (4–6 weeks)
- Blazor Server scaffold, Entra auth, MSSQL schema for nodes/fragments/runs
- FeatureService with tree CRUD, basic Operating Picture and Feature Workspace screens
- Fragment library with versioning, basic Fragment Library screen
- Single agent runtime: Anthropic API only
- Workflow engine with hardcoded kickoff workflow (stages 0, 2, 5)
- **Success criterion**: a PO can paste a transcript and get a reviewable Discovery + accept it into a feature node.

### Phase 2 — MCP and IDE round-trip (3–4 weeks)
- MCP Server with `get_node_context`, `list_rules`, `search_nodes`
- Git Sync writes `CLAUDE.md` and `.cursorrules`
- Webhook intake; ADO repo + work item adapter
- **Success criterion**: a developer in Cursor or Claude Code can pull node context and code against it.

### Phase 3 — Decomposition and enrichment (3–4 weeks)
- Decompose stage; PO Gate UI for proposed tree review
- Per-node enrichment runs (acceptance criteria, risks)
- Run Detail / Provenance screen
- Notification fan-out (in-app + Teams)
- **Success criterion**: kickoff produces a populated tree with draft acceptance criteria; PO reviews and accepts node-by-node.

### Phase 4 — Design round-trip (3–4 weeks)
- Figma adapter + custom plugin
- Wireframe generation step (UX fragments)
- Artifact Inspector with annotations
- UX validation gate; feedback fragment auto-derivation
- **Success criterion**: UX designer can iterate on AI-generated wireframes inside Figma; annotations feed back into agent re-runs.

### Phase 5 — Multi-engine and background (3–4 weeks)
- IAgentRuntime abstraction; Foundry and Claude Code Headless implementations
- Router with declared requirements + capacity-aware routing
- Agent Activity screen
- Cost tracking and budget caps
- **Success criterion**: routine background work runs on Foundry; code-heavy interactive work routes to Claude Code Headless; failures fail over.

### Phase 6 — Memory and similarity (2–3 weeks)
- Elasticsearch indexing pipeline
- Memory lookup stage with similar-feature surfacing
- Search across nodes, runs, ADRs in UI
- **Success criterion**: kickoff surfaces relevant prior features; team stops re-litigating settled questions.

### Phase 7 — Workflow Designer and methodology evolution (3–4 weeks)
- Visual workflow editor
- Fragment review tooling (usage stats, deprecation flow)
- Annotation-to-fragment promotion flow
- **Success criterion**: methodology owner can change workflows without code; the library improves measurably from feedback.

## 16. Open questions

1. **Single-tenant first?** Initial deployment is one team; multi-tenant hardening can wait. Confirm.
2. **Where does code generation actually run?** Headless Claude Code in a sandboxed runner, or Claude Code on the developer's machine triggered by Loom? The first is more controllable; the second matches how devs actually work today.
3. **How aggressive is auto-decomposition?** Should the kickoff propose all the way down to slices, or stop at capabilities and let humans drill in? Recommend stopping at capabilities for v1.
4. **Confluence vs ADO Wiki vs Loom-native** for long-form docs (ADRs, narratives)? Recommend Loom-native primary, with Confluence sync optional.
5. **Real-time collaborative editing** of node fields — field-level optimistic concurrency with presence indicators (recommended) or full CRDT (overkill for v1)?
6. **Engine credential model** — per-user OAuth where supported, per-tenant service credentials elsewhere. Verify against Foundry's real model.
7. **Privacy classification of transcripts** — does the team want a manual classification step or auto-detect with override?

## 17. Glossary

- **Node** — a FeatureNode, the unit of context. Has a type (initiative/feature/capability/slice).
- **Fragment** — a tagged, versioned piece of prompt content.
- **Run** — a single agent execution against a node, with full provenance.
- **Workflow** — a versioned DAG of steps that produce artifacts.
- **Gate** — a step that requires human acceptance before the run continues.
- **URN** — stable address for an artifact (`hub://feature/{node}/artifact/{id}`).
- **Engine** — a backend that executes agent runs (Anthropic API, Foundry, Claude Code Headless, in-proc).
- **Canonical store** — the system where an artifact really lives (Figma for designs, git for code, etc.).
- **Provenance** — the chain of (run + fragments + inputs + author) that produced an artifact.

---

*End of design overview.*
