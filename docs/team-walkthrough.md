# Loom — Team Walkthrough

*A guided tour of how the methodology hub looks from each role's seat,
written for the people who will use it day-to-day.*

---

## TL;DR

Loom turns every feature into a **living context node** with structured
intent, dynamic artifacts, an activity stream, and a fragment-assembled
prompt environment. Any AI tool — Claude, Cursor, Foundry-hosted agents,
headless Claude Code — reads from it and contributes back. Two human
gates anchor each feature (PO kickoff acceptance and a UX/tech gate);
everything else is automated but auditable. The methodology is encoded
*in* the data and workflows, not stuck on a wiki page next to it.

We're now wired to **Microsoft Foundry** (Azure OpenAI Chat Completions,
deployment `marketplace-prompt`, France Central) as the default agent
engine. The Anthropic API runtime stays registered alongside so anything
already pinned to Claude keeps working through the cutover.

## 1. Where we are today

This section is a status snapshot, not a TODO list — see §5 for what's next.

| Phase (per `docs/design/loom-design.md` §15) | State |
|---|---|
| **1. Kernel** — Blazor Server, Entra auth, MSSQL schema, FeatureService, Operating Picture, Feature Workspace, Fragment Library, Anthropic runtime, kickoff workflow | ✅ shipped |
| **2. MCP & IDE round-trip** — MCP server (`get_node_context`, `list_rules`, `search_nodes`), Git Sync writes `CLAUDE.md` / `.cursorrules`, ADO + webhooks | ✅ shipped |
| **3. Decomposition & enrichment** — Decompose stage, PO Gate UI, per-node enrichment (acceptance criteria + risks), Run Detail / Provenance | ✅ shipped (Teams notification channel still stubbed) |
| **4. Design round-trip** — Figma adapter + plugin, wireframe step, UX gate, feedback-fragment auto-derivation | ⛔ not started |
| **5. Multi-engine + background** — `IAgentRuntime`, router, health monitor, **Foundry runtime (just landed)**, project budget circuit breaker, Agent Activity screen | 🟡 partial — Claude Code Headless still pending |
| **6. Memory & similarity** — `IMemorySearch` seam, memory-lookup workflow step | 🟡 partial — current impl is SQL keyword fallback; Elasticsearch indexer is the real Phase-6 target |
| **7. Workflow Designer & methodology evolution** — Visual DAG editor, fragment usage stats, annotation→fragment promotion | 🟡 partial — Workflow *Library* (read-only) exists, the *Designer* does not |

Screens (per design §14): 8 of 10 shipped and interactive. Workflow Designer
(#7) and Notification Centre (#10) are the remaining two.

## 2. The lifecycle from the team's seat

Two human gates only. Everything else runs while you're doing other things.

```
  TEAMS TRANSCRIPT
        │
        ▼
   ┌────────────┐
   │  KICKOFF   │   ← stage 0–2: normalize → discovery → decompose
   └─────┬──────┘
         ▼
    ◇ PO GATE ◇    ← human #1: PO accepts the proposed tree
         │
         ▼
   ┌──────────────────────┐
   │ ENRICHMENT (parallel)│   ← per node: acceptance, UX flows, arch, risks
   └─────────┬────────────┘
             ▼
        ◇ ROLE GATES ◇   ← human #2: UX or tech-lead review
             │
             ▼
   ┌─────────────────────┐
   │  BUILD              │   ← Cursor / Claude Code / headless agents over MCP
   └─────────┬───────────┘
             ▼
   ┌─────────────────────┐
   │  TEST · MERGE · DEPLOY  │   ← ADO Pipelines feed back via webhooks
   └─────────┬───────────┘
             ▼
        PRODUCTION → feedback fragments back into the library
```

## 3. A worked example — "Express Checkout" for Artio Marketplace

A concrete walk-through using the same shape as our `EndToEndKickoffTests`
acceptance test, dressed up to match a real Artio product surface.

### 3.1 Setup (Tuesday, 9:30am)

A 30-minute Teams call between Andy (PO, Marketplace), Adam (UX), and
Claudio (dev team leader, supplier-api). Topic: cart abandonment is hurting goal
conversions for sustainability suppliers — buyers drop the cart at
checkout because the saved-card surface isn't right. The recording
lands in OneDrive automatically.

### 3.2 Andy kicks off in Loom (9:55am)

Andy opens [Kickoff](src/Loom.Web/Components/Pages/Kickoff.razor) at
`/kickoff`, picks the **Marketplace** project, pastes the transcript
into the box, and hits *Start*. The kickoff workflow runs three stages:

1. **`normalize`** — In-proc transcript splitter into turn objects.
   Auto, ~50 ms. No tokens.
2. **`discovery`** — Foundry agent (deployment `marketplace-prompt`,
   GPT-5.4) assembles fragments for `identity:po-discovery-assistant`,
   `methodology:problem-framing`, `methodology:definition-of-ready`,
   `skill:transcript-discovery-extraction`, plus the auto-injected
   project/domain fragments. Streams a `DiscoveryObject` JSON. Pauses
   at the **PO gate**.
3. **`decompose`** — Will run after Andy accepts discovery.

By 9:56 the discovery output is on the Run Detail screen. Andy sees
the assembled prompt (every fragment used, pinned by version), the
streaming output, and the cost (~$0.04, deployment `marketplace-prompt`).

### 3.3 Andy reviews the proposal at the PO Gate (9:58am)

He opens [PoGate](src/Loom.Web/Components/Pages/PoGate.razor) at
`/runs/{id}/gate`. The proposed `DiscoveryObject` shows:

- **Title**: "Express checkout"
- **Intent**: "Reduce friction at the payment step for repeat sustainability suppliers."
- **Outcomes**: "Cart-to-purchase conversion +5%" *(measurable)*; "Saved-card surfacing for top-20 EU markets"
- **Open questions**: "EU/UK saved-card storage parity? PSD2 SCA exemption coverage?"
- **Stakeholders**: Andy (PO), Adam (UX), Claudio (dev team leader)

He edits the conversion target down to +3% (more honest), accepts the
gate, and the engine continues to `decompose`. A second gate pauses
with a proposed tree:

```
Express checkout (feature)
├─ Guest checkout path (capability)
├─ Saved-card surfacing (capability)
└─ PSD2 SCA exemption flow (capability)
```

Andy keeps the first two and drops the third (out of scope for this
quarter; he logs a follow-up node under `Initiative: Payment
compliance Q3`). He accepts the gate; the `Express checkout` node and
its two child capabilities are created. Total wall-clock so far:
**eight minutes**.

### 3.4 Enrichment runs in the background (10:00–10:03am)

The enrichment workflow auto-queues for the two new capabilities. Each
runs two agent steps:

- `acceptance` — generates Gherkin-shaped acceptance criteria from the
  node's intent + outcomes.
- `risks` — surfaces risks + mitigations.

Both steps assemble fragments scoped to the project (`project:fortius-stack-conventions`,
`project:fortius-cqrs-mediatr`, `domain:supplier-compliance`) plus the
node's parent context. The Foundry router picks the GPT-5.4 deployment;
the budget circuit breaker confirms today's spend on the Marketplace
project is under $10 (cap is $50). Andy's inbox shows two
`run.completed` notifications.

### 3.5 Adam reviews UX flows (later that morning)

Adam opens [FeatureWorkspace](src/Loom.Web/Components/Pages/FeatureWorkspace.razor)
for *Saved-card surfacing*. He sees the proposed flows (text now —
Phase 4 will land Figma round-trip), highlights where the flow misses
PSD2 ambient-authentication scenarios, and adds an annotation. The
annotation persists; if it recurs across three more features, the
methodology owner (Andy again, in our team) will see a prompt to
promote it to a `feedback:saved-card-psd2` fragment.

### 3.6 Marcus pulls context into his IDE (afternoon)

Claudio assigns *Saved-card surfacing* to Marcus for the backend slice
(Zivko picks up the sibling guest-checkout node; Jamie will take the
Razor surfacing once the API contract is firm). Marcus has the Loom
MCP server registered in Cursor (URL `https://loom.fortius.local/mcp`,
OAuth via Entra). Marcus runs `loom.get_node_context` for that node; the
server returns:

- The full intent + outcomes + acceptance criteria (the node is the
  context).
- The compiled fragment set (identity, methodology, project, domain)
  scoped to the node.
- Recent decisions from sibling nodes (so he doesn't re-litigate
  whether to use `Artio.Common.Authentication` again — Claudio curates
  those `project:*` fragments so they stay current).
- The relevant ADRs (`docs/adr/0009-webhook-hmac-auth.md`, the
  marketplace-api repo's own ADRs).

He also runs `git pull` on `marketplace-api`; Git Sync (Loom →
ADO repo) wrote a refreshed `CLAUDE.md` and `.cursorrules` overnight,
so Cursor reads the project-scoped fragments without an MCP round-trip.

Marcus codes. Tests pass. He pushes; Claudio reviews the PR. Loom
catches the ADO webhook for the PR creation; the
`Express checkout > Saved-card surfacing` node moves to phase
`Build → InProgress`.

### 3.7 Test plan + merge (next day)

Anna (tester) opens the *Saved-card surfacing* node, clicks
*Generate test plan*. The `skill:test-plan-from-acceptance` fragment
produces an ADO Test Plan draft mapped to the acceptance criteria
generated in §3.4; she edits two scenarios, syncs to ADO. After PR
merge, the build → test → deploy pipeline emits webhooks; the node's
status moves to `Done` and the `Run` log carries every artifact URN
back to the canonical store (Figma when we have it, ADO repo for code,
Confluence for narrative).

### 3.8 Production feedback (a week later)

Sustainability suppliers in DE/FR start using the saved-card surface.
Conversion lifts ~2.4%. Andy logs the actual outcome on the node. The
delta from the predicted 3% is captured automatically when the next
quarterly review runs `methodology:outcome-vs-prediction-postmortem`,
which surfaces the systematic over-prediction across the last 14
features and proposes a calibration tweak to
`methodology:problem-framing`. Methodology evolves itself.

## 4. What each role does

| Role | What you do in Loom | Where you spend time |
|---|---|---|
| **Product Owner** | Kick off from transcripts, review proposals at gates, own intent and outcomes per node. Approve or edit at the two human gates. | Loom UI, Teams, Confluence |
| **Developer** | Pull node context into your IDE via MCP. Code with the fragments compiled into `CLAUDE.md` / `.cursorrules`. Push back through your usual Git/ADO flow — Loom intakes the webhooks. | Cursor / Claude Code / Rider, plus the Feature Workspace and Run Detail screens for context. |
| **UX Designer** | Review AI-generated flows on the node. Annotate. (Phase 4 will let you do this in Figma directly.) Validate UX gates. | Figma + Loom UI |
| **Tester** | Generate test plans from acceptance criteria. Sync to ADO Test Plans. Trace tests back to acceptance through the artifact graph. | ADO Test Plans + Loom UI |
| **Tech Lead / Architect** | Review architecture sketches at the role gate. Curate `project:*` fragments. Author ADRs (Loom can draft them via `skill:adr-writer`). | Loom UI, git, the Fragment Library |
| **Methodology Owner** | Curate the fragment library. Approve auto-proposed fragments from recurring annotations. Evolve workflows when the team's working practice changes. | Fragment Library, Workflow Library/Designer |

Roles are not exclusive. A six-person team might have one person
wearing PO + Methodology Owner hats, and developers stepping into UX
review; Loom doesn't enforce — it surfaces role-flavoured views.

**On this team**

- Product Owner — **Andy**
- Dev team leader / Tech Lead / Architect — **Claudio**
- Backend developers — **Marcus**, **Zivko**
- Frontend developer — **Jamie**
- UX Designer — **Adam**
- Tester — **Anna**
- Methodology Owner — **Andy** (doubles up with the PO hat)

## 4a. What each menu item is for

The intent of three nav items isn't obvious from the UI alone. The
short answer for each:

- **Library** — *is* the fragment editor, just with different
  semantics than a wiki. Browse fragments by category; click into a
  fragment to see its version history; "edit" means **publishing a
  new version** of that fragment, not mutating the current one. This
  is intentional (`docs/design/loom-design.md` §7 *Versioning rules*):
  fragments are immutable per version so every prior run stays
  reproducible against its exact pinned version. Bumping a version
  makes the change observable, replayable, and revertable. New
  fragment? Use the *New fragment* button on the library page; that
  creates the fragment and opens the editor for v1.
- **Workflows** — read-only viewer today. Click a workflow to see
  its full step list (kind, gating, engine pref, output schema,
  budgets, fragment selectors). The visual editor (Workflow Designer)
  is deferred to Phase 7b per
  [ADR-0016](adr/0016-workflow-library-and-multitenancy-deferral.md).
  For now, workflows are *code-as-data*: see
  [KickoffWorkflowFactory.cs](../src/Loom.Application/Workflows/Kickoff/KickoffWorkflowFactory.cs)
  and `EnrichmentWorkflowFactory` for the seed-time definitions.
  Bump `CurrentVersion` when the definition changes; the
  bootstrapper seeds the new version on next start. Full guide in
  [workflows.md](workflows.md).
- **Activity** — operational dashboard. Cross-engine health
  (success/failure rate over the last 20 outcomes per engine), live
  active runs grouped by state, and per-project budget burn against
  daily $-cap. Auto-refreshes every 5s. Read-only by design — this
  is a watch-the-system surface, not an intervene-with-the-system
  one. If a run is misbehaving, fix it through Run Detail (replay,
  cancel) or the underlying workflow definition.

## 5. What's left to do

**P0 — finish the demo loop**

1. **Teams notification channel** — `INotificationChannel` for Teams is
   stubbed in `src/Loom.Application/Notifications/`. Wire it through
   Microsoft Graph so run-completion and gate-paused events land as
   adaptive cards. *~2–3 days.*
2. **Notification Centre UI** — design §14 screen #10. The InApp
   channel works (`NodeHub` SignalR), the inbox view is missing.
   *~3 days.*
3. **Foundry pricing calibration** — `FoundryCostCalculator.Models`
   has placeholder GPT-5.4 numbers. Confirm the team's Foundry list
   price for `marketplace-prompt` and update. *~30 minutes once we
   have the price.*
4. **Foundry workflow re-seed** — existing seeded workflows in
   running databases still pin `Anthropic`. Either bump
   `KickoffWorkflowFactory.CurrentVersion` to `2` and re-seed, or
   wipe and re-bootstrap. *~1 hour.*

**P1 — Phase 4: Design round-trip (the biggest gap)**

5. **Figma adapter + plugin** — REST adapter in
   `src/Loom.Integrations/Figma/`, custom plugin that reads/writes
   wireframes by node URN. Adds the `wireframe` step and a UX gate to
   the kickoff workflow. *~2–3 weeks.*
6. **UX validation gate + feedback-fragment auto-derivation** — extend
   the gate machinery to call into Figma; promote recurring
   annotations into `feedback:*` fragments after N occurrences.
   *~1 week.*

**P1 — Phase 6: Real memory**

7. **Elasticsearch indexer** — `IMemorySearch` ships, `SqlMemorySearch`
   is the SQL fallback. Ship a Lucene/ES indexer fed by the outbox so
   memory-lookup is genuinely "have we seen this before?" — currently
   it's a keyword search. *~1 week.*
8. **Search UI** — node/run/ADR search across the project. The seam
   exists, the screen does not. *~2 days.*

**P2 — Phase 5b + 7**

9. **Claude Code Headless runtime** — `Loom.Agents.ClaudeCodeHeadless`
   to run multi-file edit + bash/lint/test loops. The router will
   pick it for code-heavy slice work. *~1 week.*
10. **Workflow Designer** — visual DAG editor + YAML view + dry-run.
    Phase 7 enabler so methodology evolves without code. *~2 weeks.*
11. **Annotation → fragment promotion** — auto-propose new feedback
    fragments when an annotation recurs N≥3 times across nodes; the
    methodology owner accepts/rejects. *~3 days.*
12. **Fragment usage stats + deprecation flow** — quarterly fragment
    review tooling. *~3 days.*

**P3 — Operational hardening**

13. **Multi-tenant by project** — single-tenant today (ADR-0016 defers
    multi-tenancy). Row-level security on `ProjectId` when we onboard
    the second team.
14. **Fix dual-EF-provider conflict in `Loom.Web.Tests`** — three host
    smoke tests broken by the recent SQLite-dev work. Already filed as
    a side task in this session.
15. **OpenTelemetry + proper health checks** — currently
    `/healthz` is a one-liner. Stand up traces, metrics, and per-engine
    health in App Insights.

**Demo-ready scope**: P0 plus the existing Phase 1-3 surface is enough
to take Andy's eight-minute kickoff demo to a live audience. P1 is
what unlocks the full methodology loop including the design round-trip
and "we've seen this before" memory.

## 6. Glossary

- **Node** — a `FeatureNode`, the unit of context (initiative /
  feature / capability / slice).
- **Fragment** — a tagged, versioned piece of prompt content. See
  [fragment-library.md](fragment-library.md) for the seeded library.
- **Run** — a single agent execution against a node, with full
  provenance.
- **Workflow** — a versioned DAG of steps that produce artifacts.
- **Gate** — a step that requires human acceptance before the run
  continues. Loom has two: PO at kickoff, UX/tech-lead at enrichment.
- **URN** — stable address for an artifact
  (`hub://feature/{node}/artifact/{id}`).
- **Engine** — a backend that executes agent runs. Today: Foundry
  (default for new work), Anthropic API (kept registered for
  pin-back compatibility), in-proc (for stage-0 normalization).
- **Canonical store** — the system where an artifact really lives
  (Figma for designs, git/ADO for code, Confluence for narrative).
- **Provenance** — the chain of (run + fragments + inputs + author)
  that produced an artifact.
