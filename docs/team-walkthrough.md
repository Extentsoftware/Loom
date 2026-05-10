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
| **2. MCP & IDE round-trip** — MCP server (`loom_get_node_context`, `loom_list_rules`, `loom_search_nodes`, `loom_list_project_artifacts`, `loom_get_artifact`, `loom_attach_artifact`), Git Sync writes `CLAUDE.md` / `.cursorrules`, ADO + webhooks | ✅ shipped |
| **3. Decomposition & enrichment** — Decompose stage, PO Gate UI, per-node enrichment (acceptance criteria + risks), Run Detail / Provenance, Teams notification channel | ✅ shipped |
| **4. Design round-trip** — Figma adapter + plugin, wireframe step, UX gate, feedback-fragment auto-derivation | ⛔ not started |
| **5. Multi-engine + background** — `IAgentRuntime`, router, health monitor, **Foundry runtime (just landed)**, project budget circuit breaker, Agent Activity screen | 🟡 partial — Claude Code Headless still pending |
| **6. Memory & similarity** — `IMemorySearch` seam, memory-lookup workflow step | 🟡 partial — current impl is SQL keyword fallback; Elasticsearch indexer is the real Phase-6 target |
| **7. Workflow Designer & methodology evolution** — Visual DAG editor, fragment usage stats, annotation→fragment promotion | 🟡 partial — Workflow *Library* (read-only) exists, the *Designer* does not |

Screens (per design §14): 9 of 10 shipped and interactive. Workflow Designer
(#7) is the only remaining screen.

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
and hits *Analyse*. A scope classifier reads the transcript and
suggests a mode:

- **Single feature** — the conversation is about one feature, even if
  several capabilities are mentioned. Root is a `Feature`, decompose
  produces capabilities under it.
- **Multi-feature initiative** — the conversation covers two or more
  independently-shippable features. Root is an `Initiative`, decompose
  returns a tree of features (each with their capabilities).

For Express Checkout it picks **Single** (high confidence). Andy
clicks the button to start the workflow. If the transcript had
covered checkout *and* saved-card *and* fraud panels, the suggestion
would be **Multi** and Andy would see them listed for confirmation.
The kickoff workflow then runs three stages:

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
OAuth via Entra). Marcus runs `loom_get_node_context` for that node; the
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

This list is current as of the project-scoped subscriptions slice.
Anything that says *done* here is wired end-to-end and verified by
build + tests passing — see the "delivered since last walkthrough"
list at the end.

**P0 — close-out items**

1. **Foundry pricing calibration** — `FoundryCostCalculator.Models`
   still has placeholder GPT-5.4 numbers. Confirm the team's Foundry
   list price for `marketplace-prompt` and update. *~30 minutes once
   we have the price.*
2. **Fix dual-EF-provider conflict in `Loom.Web.Tests`** — three host
   smoke tests are broken by the SQLite-dev work; the test factory's
   `RemoveAll(DbContextOptions<LoomDbContext>)` doesn't strip EF
   Core 10's internal services, so InMemory and SqlServer collide.
   *~1 day.*

**P1 — Phase 4: Design round-trip**

The skinny path is in: a wireframing workflow that produces an HTML
envelope, projects it as a `Wireframe` artifact, and pauses at a UX
gate which renders accept/reject in the existing PoGate page.
What's *missing* is the real Figma round-trip:

3. **Figma adapter + plugin** — REST adapter in
   `src/Loom.Integrations/Figma/`, custom plugin that reads/writes
   wireframes by node URN. Replaces the canonical store on the
   `Wireframe` artifact from `HubNative` to `Figma`. Same workflow
   shape; the projector's pointer changes. *~2–3 weeks.*
4. **Feedback-fragment auto-derivation** — promote recurring
   annotations on artifacts into `feedback:*` fragments after N
   occurrences. The annotation surface exists (`ArtifactInspector`);
   the deduplication + fragment-create flow doesn't. *~1 week.*

**P1 — Phase 6: Real memory**

5. **Elasticsearch indexer** — `IMemorySearch` ships, `SqlMemorySearch`
   is the SQL fallback. Ship a Lucene/ES indexer fed by the outbox so
   memory-lookup is genuinely "have we seen this before?" — currently
   it's a keyword search. *~1 week.*
6. **Search UI** — node/run/ADR search across the project. The seam
   exists, the screen does not. *~2 days.*

**P2 — Phase 5b: extend the developer loop**

The pull-claim MCP path (`loom_list_my_tasks` / `loom_claim_task` /
`loom_complete_task` / `loom_release_task`) gives developers a way to
take work through Claude Code today; ClaudeCodeHeadless is no longer
required to demo the loop, but the runtime is still useful for batch
slice-execution.

7. **Per-user MCP auth** — today the MCP tools accept a `userId`
   parameter and trust the caller. Real per-user tokens (mapped to
   Loom user ids) need to land before this is shareable beyond a
   single dev box. *~3 days.*
8. **Claude Code Headless runtime** — `Loom.Agents.ClaudeCodeHeadless`
   to run multi-file edit + bash/lint/test loops as an *agent engine*
   (separate from the dev's own Claude Code instance). The router
   will pick it for code-heavy slice work. *~1 week.*

**P2 — Phase 7: methodology evolution**

9. **Workflow Designer** — visual DAG editor + YAML view + dry-run.
    Phase 7 enabler so methodology evolves without code. *~2 weeks.*
10. **Annotation → fragment promotion** — auto-propose new feedback
    fragments when an annotation recurs N≥3 times across nodes; the
    methodology owner accepts/rejects. *~3 days.*
11. **Fragment usage stats + deprecation flow** — quarterly fragment
    review tooling. *~3 days.*

**P3 — Operational hardening**

12. **Multi-tenant by project** — single-tenant today (ADR-0016 defers
    multi-tenancy). Row-level security on `ProjectId` when we onboard
    the second team.
13. **OpenTelemetry + proper health checks** — currently
    `/healthz` is a one-liner. Stand up traces, metrics, and per-engine
    health in App Insights.

---

### Delivered since the last walkthrough revision

These items either replace, satisfy, or sit alongside earlier P0/P1/P2
entries. Listed for quick orientation, not as future work.

- **Teams notification channel** ([TeamsWebhookChannel.cs](../src/Loom.Application/Notifications/TeamsWebhookChannel.cs))
  — replaces the stub. Posts adaptive cards via an Incoming Webhook
  configured under `Loom:Notifications:Teams:WebhookUrl`.
- **Notification Centre UI** — bell icon in `MainLayout` with live
  push from `NotificationHub`; full feed at `/notifications`;
  mark-read / mark-all-read.
- **Project- and role-scoped subscriptions** — `Subscription` carries
  optional `ProjectId` and `Role` (`WorkflowStepGatingRole`); a UX
  reviewer can subscribe once at project scope and only get pinged on
  UX gates. UI: `/settings/subscriptions`.
- **Foundry as the default engine** — kickoff/v2 + enrichment/v2 +
  wireframing/v1 all pin `EngineName.Foundry`.
- **Multi-engine routing + failover (Phase 5a)** — `WorkflowEngine`
  walks a fallback chain on per-engine failure; `IEngineHealthMonitor`
  tracks rolling success/failure + in-flight counts; `DefaultAgentRouter`
  picks the least-loaded healthy engine.
- **Step-output projection seam** — `IStepOutputProjector` resolves by
  `OutputSchemaName`; ships projectors for `AcceptanceCriteria`,
  `RiskRegister`, `Wireframe` writing into the existing `Artifact`
  aggregate. Re-runs update the canonical pointer (no longer
  silently no-op).
- **Composer schema teaching** — `AssembledPromptComposer` appends a
  hard-coded "Required output shape" block when the step declares a
  known schema; no longer relies on fragments to teach JSON shape.
- **Per-kind Artifact Inspector renderers** — Wireframe → sandboxed
  iframe; AcceptanceCriteria → checklist; RiskRegister → table;
  fallback → `<pre>`.
- **UX validation gate** — `WorkflowStepGating.HumanUx` rendered in
  PoGate.razor with deep-link to the projected wireframe + accept /
  reject controls.
- **Wireframing workflow + launcher** — `wireframing/v1` (one Foundry
  agent step + one UX gate); `Run wireframing` button on the
  workspace.
- **Pull-claim MCP path** — `Run.AssigneeUserId/AssignedAt`,
  `RunAssignmentService`, four MCP tools (`loom_list_my_tasks`,
  `loom_claim_task`, `loom_complete_task`, `loom_release_task`).
- **Artifact MCP tools** — `loom_list_project_artifacts`,
  `loom_get_artifact`, `loom_attach_artifact` for browsing and
  attaching artifacts from IDE agents.
- **Project DAG dashboard** — `/projects/{id}/dag`, cytoscape-driven
  (built-in `breadthfirst` layout, no plugin), phase-coloured nodes,
  click-to-navigate.
- **Operating Picture project mgmt** — create / rename / archive
  projects from the dashboard header.
- **Discovery editors** — Hypotheses, Outcomes, Stakeholders,
  Constraints, OpenQuestions all editable from the workspace.
- **Audit trail** — `NodeUpdated.Actor` + `NodeEditKind` carry who
  changed what.
- **Fragment library viewing/editing** — full CRUD at `/fragments`
  including the seeded library.
- **SQLite dev provider** — no-install dev path; provider auto-detect
  from connection string. `EnsureCreated` from model on Sqlite,
  `MigrateAsync` on SqlServer.
- **Blazor concurrency hardening** — InteractiveServer pages no
  longer prerender-against-the-circuit-scoped DbContext;
  `NotificationBell` and the wireframing launcher use per-call
  `IServiceScopeFactory` so layout-level + page-level DB calls don't
  race.

**Demo-ready scope**: P0 + the delivered list is enough for the
eight-minute kickoff demo *plus* the developer-loop (notification →
MCP claim → complete) walk-through. P1 is what unlocks the design
round-trip via Figma and the "we've seen this before" memory.

## 6. Glossary

- **Node** — a `FeatureNode`, the unit of context. The hierarchy is
  strict: **initiative ▸ feature ▸ capability ▸ slice**. Same shape at
  every level, distinguished by `NodeType`.
  - **Initiative** — top-level intent for a project, theme, or quarter
    outcome. Sits directly under a Project.
  - **Feature** — a coherent capability bundle delivered to users;
    child of an Initiative.
  - **Capability** — a discrete behaviour or system competence required
    by a feature; child of a Feature. Never a peer of a feature.
  - **Slice** — a thin end-to-end deliverable that exercises one
    capability; child of a Capability. The unit a developer pair picks
    up.
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
