# Loom Workflows — guide

A workflow is a **versioned, declarative sequence of steps** that take a
node (and some inputs) and produce Runs + artifacts. Two workflows ship
with Loom today: `kickoff` and `enrichment`. This doc explains how
they're constructed, executed, observed, and extended.

## TL;DR

- A **`Workflow`** is a domain aggregate with an immutable
  `(Key, Version)` identity and a list of ordered **`WorkflowStep`**s.
- Each step declares: a **kind** (`Agent` / `HumanGate` / `InProc`), a
  **gating** mode (`Auto` / `HumanPo` / `HumanUx` / `HumanLead`), an
  **engine preference**, an **output schema**, **budgets**, and
  **fragment selectors**.
- The **`WorkflowEngine`** (`src/Loom.Application/Workflows/`) walks the
  step list, creating a **`Run`** per step it executes. Inputs flow
  forward keyed by `<stepKey>.output`.
- Workflows are **code-as-data** today (defined in C# factories, seeded
  at bootstrap). Phase 7b ships the visual Workflow Designer; until
  then, "edit a workflow" means write code and bump `CurrentVersion`.

## 1. The mental model

Picture each workflow as a pipe:

```
inputs ──► [step] ──output──► [step] ──output──► [step] ──► …
                       │                  │
                       ▼                  ▼
                     Run                Run (paused at gate)
```

Each step:

- pulls **inputs** from a shared dictionary (`step.Key.output` from
  prior steps, plus any caller-supplied initial inputs);
- runs in one of three **kinds** (see §3);
- may pause the workflow at a **gate** waiting for a human;
- on completion, writes its `output` back into the dictionary so
  downstream steps can read it.

Every execution produces a **Run** record (per agent or gate step), so
the methodology layer has full provenance: which workflow, which step,
which fragments, which engine, what cost, who resolved which gates.

## 2. Where the pieces live

| Piece | Path |
|---|---|
| Workflow + WorkflowStep aggregates | [src/Loom.Domain/Workflows/](../src/Loom.Domain/Workflows/) |
| Step kind / gating enums | [WorkflowEnums.cs](../src/Loom.Domain/Workflows/WorkflowEnums.cs) |
| Engine + run state enums | [src/Loom.Domain/Runs/RunEnums.cs](../src/Loom.Domain/Runs/RunEnums.cs) |
| **WorkflowEngine** (the runner) | [src/Loom.Application/Workflows/WorkflowEngine.cs](../src/Loom.Application/Workflows/WorkflowEngine.cs) |
| AssembledPromptComposer | [src/Loom.Application/Workflows/AssembledPromptComposer.cs](../src/Loom.Application/Workflows/AssembledPromptComposer.cs) |
| RunService (lifecycle, budget gate, replay) | [src/Loom.Application/Runs/RunService.cs](../src/Loom.Application/Runs/RunService.cs) |
| AgentRouter (engine resolution, health/budget) | [src/Loom.Application/Agents/IAgentRouter.cs](../src/Loom.Application/Agents/IAgentRouter.cs) |
| Kickoff factory + seed | [src/Loom.Application/Workflows/Kickoff/KickoffWorkflowFactory.cs](../src/Loom.Application/Workflows/Kickoff/KickoffWorkflowFactory.cs) |
| Enrichment factory + seed | [src/Loom.Application/Workflows/Enrichment/EnrichmentWorkflowFactory.cs](../src/Loom.Application/Workflows/Enrichment/EnrichmentWorkflowFactory.cs) |
| Bootstrapper (seeds workflows on first run) | [src/Loom.Infrastructure/Bootstrap/LoomBootstrapper.cs](../src/Loom.Infrastructure/Bootstrap/LoomBootstrapper.cs) |
| In-proc step extension point | [src/Loom.Application/Workflows/IInProcStep.cs](../src/Loom.Application/Workflows/IInProcStep.cs) and [src/Loom.Agents.InProc/](../src/Loom.Agents.InProc/) |
| Workflow Library UI (read-only) | [src/Loom.Web/Components/Pages/WorkflowLibrary.razor](../src/Loom.Web/Components/Pages/WorkflowLibrary.razor) + `WorkflowDetail.razor` |
| Run Detail / Provenance UI | [src/Loom.Web/Components/Pages/RunDetail.razor](../src/Loom.Web/Components/Pages/RunDetail.razor) |

## 3. The Workflow data model

```
Workflow                            (aggregate root)
├─ Id          (WorkflowId)
├─ Key         (Slug, e.g. "kickoff")
├─ Version     (int)
├─ Title       (string)
└─ Steps[]
   └─ WorkflowStep
      ├─ Id, Key (e.g. "discovery")
      ├─ Order   (int)
      ├─ Kind    (Agent | HumanGate | InProc)
      ├─ Gating  (Auto | HumanPo | HumanUx | HumanLead)
      ├─ EnginePref (EngineName? — required for Agent steps)
      ├─ OutputSchemaName (string? — e.g. "DiscoveryObject")
      ├─ Budgets (max input/output tokens, max wall-clock, max $)
      └─ FragmentSelectors[]   (category + slug to pull at composition time)
```

Two enums you'll touch a lot:

**`WorkflowStepKind`** — what the step *does*:

| Kind | Effect |
|---|---|
| `Agent` | Resolves an `IAgentRuntime` via the router, composes a prompt from selected fragments, sends it, streams events, persists a Run with full provenance. |
| `HumanGate` | Creates a Run that immediately enters `PausedForHuman`. The UI surfaces a gate page; resolving it (with optional edits) emits a `GateResolved` event and lets the engine continue. |
| `InProc` | Runs a registered `IInProcStep` keyed by `step.Key`. Cheap, deterministic transformations (e.g. transcript normalisation). No agent, no run-tracker overhead. |

**`WorkflowStepGating`** — whether the step needs a human:

| Gating | Behaviour |
|---|---|
| `Auto` | Step proceeds without human intervention. |
| `HumanPo` | Engine pauses; PO must accept (with optional edits) at the PO Gate page. |
| `HumanUx` | UX role gate (Phase 4). |
| `HumanLead` | Tech-lead role gate (Phase 4+). |

Domain invariants enforced by `WorkflowStep.Create`:

- `Agent` kind **requires** `EnginePref`.
- `InProc` kind **forbids** `EnginePref` (the step key is the routing key).
- `HumanGate` kind **forbids** `Auto` gating (it's pointless — a gate that doesn't gate).

## 4. Lifecycle: definition → trigger → execution

### 4a. Definition (compile time)

A workflow lives in code as a factory class that returns a fully-built
`Workflow` aggregate. See `KickoffWorkflowFactory.Build(now)` for the
canonical example.

### 4b. Seeding (first run)

`LoomBootstrapper.SeedKickoffWorkflowAsync` runs at startup, looks up
`(Slug.From(WorkflowKey), CurrentVersion)`, and inserts the workflow if
it isn't already in the DB. **Idempotent** — existing rows are never
overwritten.

To **change** a workflow definition: edit the factory, **bump
`CurrentVersion`**, restart. The bootstrapper sees the new
`(key, version)` doesn't exist and seeds it. The old version stays in
the DB; in-flight runs against it keep working.

### 4c. Triggering

| Workflow | Trigger |
|---|---|
| `kickoff` | PO submits a transcript on `/kickoff` — `WorkflowEngine.StartAsync(rootNodeId, kickoffWorkflow.Id, { ["transcript"] = ... })` |
| `enrichment` | Auto-queued for newly-accepted child nodes (Phase-3 handler) — `EnrichmentAutoQueueHandler` |

### 4d. Execution loop

`WorkflowEngine.StartAsync(nodeId, workflowId, initialInputs)`:

1. Loads the workflow + node.
2. Iterates `workflow.Steps.OrderBy(Order)`:
   - **InProc**: invokes the registered `IInProcStep`, writes its output
     to `inputs[$"{step.Key}.output"]`. Returns to the loop.
   - **Agent**: see §5 below.
   - **HumanGate**: queues an "empty" gate Run, immediately pauses it,
     emits `RunPausedForHuman`, returns the gate Run id to the caller
     (the UI redirects to `/runs/{id}/gate`).
3. If a step pauses at a gate, the engine returns. The outer caller
   (PoGate page) calls `engine.ResolveGateAsync(runId, stepKey, edits)`
   when the human accepts; that resumes the run, emits a
   `GateResolved` event, and continues from the next step.

### 4e. Run lifecycle within a step

A Run goes:

```
Queued ──► Running ──► (PausedForHuman ──► Running)? ──► Completed | Failed | Cancelled
```

`RunService` enforces transitions and emits domain events
(`RunQueued`, `RunStarted`, `RunPausedForHuman`, `RunCompleted`, etc.)
that flow through the outbox to handlers — most importantly
`NodeHubBroadcaster` (live UI updates) and
`EnrichmentAutoQueueHandler` (queues enrichment after PO acceptance).

## 5. How an Agent step actually runs

`WorkflowEngine.RunAgentStepAsync` is the most-asked-about path:

```
1. Resolve runtime: router.Resolve(step.EnginePref)
   (Phase-5 path: ResolveWithFallback when multi-engine + health checks light up.)

2. Compose effective fragments:
   fragmentService.GetEffectiveFragmentsAsync(node.Id, step.FragmentSelectors)
   ─ walks project → ancestor chain → self
   ─ applies the selectors at each level
   ─ returns the deduplicated, ordered fragment set with their pinned versions

3. Queue the run:
   runService.QueueAsync(node.Id, workflowId, stepId, EnginePref, step.Budgets, ...)
   ─ checks the project budget circuit breaker (ADR-0014)
   ─ persists the Run with state = Queued + emits RunQueued

4. Build the assembled prompt:
   composer.Compose(step, fragments, nodeContext, inputs, run.Id, now)
   ─ orders fragments by category (identity → methodology → project → domain → skill → context → feedback)
   ─ appends the live node context (intent, outcomes, hypotheses, open questions)
   ─ adds inputs as user messages (e.g. <transcript>...</transcript>)
   ─ snapshots the exact (FragmentId, FragmentVersionId, Version) triples used

5. Attach prompt + mark Running:
   runService.AttachAssembledPromptAsync(run.Id, prompt)
   runtime.StartAsync(request) → externalId
   runService.MarkRunningAsync(run.Id, externalId)

6. Stream events from the runtime:
   ─ AgentRunEvent.Output   → accumulates output text
   ─ AgentRunEvent.TokenUsage → recorded as RunEvent.TokenUsage
   ─ AgentRunEvent.Failed   → runService.FailAsync, abort the workflow
   ─ AgentRunEvent.Cancelled → runService.CancelAsync, abort
   ─ AgentRunEvent.Completed (with final Cost) → set finalCost

7. Persist completion:
   runService.AppendEventAsync(...) for StepOutput + StepCompleted
   runService.CompleteAsync(run.Id, finalCost)
   ─ records spend against the project budget

8. If the step's gating is Auto, return — the engine continues.
   If gating is HumanPo/HumanUx/HumanLead, queue a sibling gate Run,
   pause it, emit RunPausedForHuman, return the gate run id to the
   caller.
```

The output text from step 6 becomes `inputs[$"{step.Key}.output"]` for
the next step.

## 6. The two seeded workflows

### 6a. `kickoff` (v2)

Three steps. Initial input: `transcript`.

| # | Step | Kind | Gating | Engine | Output schema | Fragments |
|---|---|---|---|---|---|---|
| 1 | `normalize` | InProc | Auto | — | `TranscriptTurns` | `skill:transcript-normalize` |
| 2 | `discovery` | Agent | HumanPo | Foundry | `DiscoveryObject` | `identity:po-discovery-assistant`, `methodology:problem-framing`, `methodology:definition-of-ready`, `skill:transcript-discovery-extraction` |
| 3 | `decompose` | Agent | HumanPo | Foundry | `DecompositionProposal` | `identity:po-architect-pair`, `skill:feature-decomposition` |

End-to-end:

1. PO pastes transcript → `WorkflowEngine.StartAsync(rootNodeId, kickoff.Id, { transcript })`.
2. `normalize` runs in-proc; its output goes to `inputs["normalize.output"]`.
3. `discovery` composes its prompt (4 fragments + node context + transcript), sends to Foundry, streams a `DiscoveryObject` JSON, completes. Gating is `HumanPo` so the engine queues a gate Run, pauses, and returns its id.
4. UI redirects to `/runs/{gateId}/gate`. PO reviews on `PoGate.razor`; accepts (with edits) which calls `WorkflowEngine.ResolveGateAsync(gateId, "discovery", edits)`. The engine applies the edited discovery onto the node (`FeatureService.ApplyDiscoveryAsync`) and continues.
5. `decompose` runs the same way; PO accepts the proposed children at the second gate, which creates the child `FeatureNode`s.
6. `enrichment` workflow auto-queues for each accepted child node (Phase-3 `EnrichmentAutoQueueHandler`).

### 6b. `enrichment` (v2)

Two steps. Auto-gated. Triggered per child node after kickoff acceptance.

| # | Step | Kind | Gating | Engine | Output schema | Fragments |
|---|---|---|---|---|---|---|
| 1 | `acceptance` | Agent | Auto | Foundry | `AcceptanceCriteria` | `identity:po-discovery-assistant`, `methodology:definition-of-ready` |
| 2 | `risks` | Agent | Auto | Foundry | `RiskRegister` | `identity:po-architect-pair` |

No human gates — enrichment is meant to populate scaffolding the PO will
review on the Feature Workspace, not interrupt them.

## 7. How fragment composition actually works

Fragment selectors look like `(FragmentCategory.Identity, Slug.From("po-discovery-assistant"))`.
The composer at run time:

1. Calls `IFragmentService.GetEffectiveFragmentsAsync(nodeId, selectors)`.
   That method walks scope:
   - **Global** scope (the bootstrapped library);
   - then **Project** scope (any fragments scoped to the project);
   - then each **ancestor node** in the tree from root down to `nodeId`.
   At each level it picks fragments matching the selectors. Local
   (more-specific scope) overrides ancestor (broader scope) when keys
   collide.
2. Returns the deduplicated set with each fragment's *currently
   published version*.
3. The composer orders them by category (`identity → methodology →
   project → domain → skill → context → feedback`) and concatenates
   their content into the system prompt.
4. The node's live context (title, intent, outcomes, open questions)
   is appended as a trailing system block.
5. Step inputs become user messages.
6. The exact `(FragmentId, FragmentVersionId, Version)` triples are
   captured on `AssembledPrompt.Fragments` so a replay can reproduce
   the run against the same versions, or against latest with a
   deliberate diff. See [ADR-0012](adr/0012-conversation-and-replay.md).

This is the mechanism that makes methodology *executable*: when the
methodology owner publishes a new version of `methodology:problem-framing`
via the Library UI, every kickoff `discovery` step from then on
automatically composes the new content — without code changes.

## 8. Adding a new workflow

The pattern, as a checklist:

1. **Write a factory** under `src/Loom.Application/Workflows/<Area>/<Name>WorkflowFactory.cs`.
   Use `KickoffWorkflowFactory` as the template. Define a `WorkflowKey`,
   `CurrentVersion`, step keys, and a `Build(DateTimeOffset now)` static
   method that returns a `Workflow` via `Workflow.Create(...)`.
2. **Declare any new `IInProcStep` implementations** (if you have InProc
   steps) under `src/Loom.Agents.InProc/`. Register them via
   `AddLoomInProcSteps()` in `Loom.Web/Program.cs`.
3. **Reference the fragments you need** by `(FragmentCategory, Slug)`.
   If a fragment doesn't exist yet, add it to `SeedFragments.All` (see
   [fragment-library.md](fragment-library.md)).
4. **Seed it on bootstrap**. Add a clause to
   `LoomBootstrapper.SeedKickoffWorkflowAsync` (or rename that method
   when you grow past two workflows) that checks for
   `(Slug.From(YourFactory.WorkflowKey), YourFactory.CurrentVersion)`
   and inserts via `Workflow.Create(...)` if absent.
5. **Trigger it** from somewhere — a UI button, a domain event handler,
   an API endpoint. Triggering means calling
   `WorkflowEngine.StartAsync(nodeId, workflow.Id, inputs)`.
6. **Test it** with the existing fakes (see
   `tests/Loom.Application.Tests/Integration/EndToEndKickoffTests.cs`).
   You don't need EF or a runtime — fake repositories and a stubbed
   chat client cover the whole engine path.

## 9. Evolving an existing workflow

The discipline:

- **Bump `CurrentVersion`** on the factory whenever the step list,
  step kinds, gating, fragments, or budgets change in a way that
  affects new runs.
- The bootstrapper inserts the new version on next start. The old
  version stays in the DB so:
  - In-flight runs against the old version keep their semantics.
  - The Workflow Library UI can show both — historical for audit, new
    for live.
- Fragments referenced by selectors are looked up by *current
  published version* at run time. Bumping a fragment version doesn't
  require bumping the workflow version.

If a change is *additive* (a new fragment selector that didn't exist),
you can sometimes get away without bumping the workflow version — but
the safer rule is "bump on any change" so the methodology trail is
unambiguous.

## 10. Observing workflows at runtime

| Surface | What it shows |
|---|---|
| `/workflows` | Read-only list of registered workflows + their step graphs. Phase 7b ships an editor here. |
| `/workflows/{id}` | Full step list for one workflow — kind, gating, engine pref, output schema, budgets, fragment selectors. |
| `/runs/{runId}` | Run Detail. Assembled prompt (every fragment version), event timeline, cost, replay button. |
| `/n/{nodeId}` | Feature Workspace — recent runs against this node, with state pills and links to gates. |
| `/activity` | Cross-engine ops dashboard — live run queue, engine health, per-project budget burn. |

## 11. What's still missing (the Workflow Designer gap)

Today, workflows are **code-as-data**: defined in C#, seeded by code,
edited by editing C# and bumping a version constant. That's deliberate
for v1 — it keeps the data model honest while the team learns what
shapes of workflow it actually needs.

[ADR-0016](adr/0016-workflow-library-and-multitenancy-deferral.md)
defers the visual Workflow Designer to Phase 7b. When that ships, you'll
edit workflows the same way you edit fragments — through the UI, with
versioned saves, dry-run preview, and migration tooling for in-flight
runs. Until then, the methodology owner pairs with a developer to land
workflow changes.

## 12. Glossary

- **Workflow** — a versioned DAG (today a list, tomorrow a graph) of
  steps that produce artifacts.
- **Step** — one node in the workflow. Has a `Kind` (what it does), a
  `Gating` (whether a human reviews), and step-specific config
  (`EnginePref`, `OutputSchemaName`, `Budgets`, `FragmentSelectors`).
- **Run** — a single execution of one step against one node, with full
  provenance.
- **Gate** — a step that pauses for a human. The PO accepts at kickoff
  gates; the UX/tech-lead accept at enrichment / role gates.
- **Engine** — the backend that executes an Agent step. Today: Foundry
  (default), Anthropic (kept registered), InProc (stage-0 normalize).
  Future: Claude Code Headless (Phase 5b).
- **Effective fragment set** — the deduplicated, scope-walked,
  category-ordered list of fragment versions actually composed into a
  given run. Persisted on `AssembledPrompt.Fragments`.
- **Replay** — re-running a prior Run with the same inputs (or edited
  inputs). Used to diff prompt edits, fragment edits, or just reproduce
  failures. See [ADR-0012](adr/0012-conversation-and-replay.md).
