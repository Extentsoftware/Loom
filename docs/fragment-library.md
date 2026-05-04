# Loom Fragment Library — reference

The library that bootstraps with every Loom deployment. **31 fragments
across 5 categories**, calibrated to the Fortius / Artio stack.

## How fragments work (60-second refresher)

A fragment is a small, tagged, versioned chunk of prompt content. It
carries:

- a stable **key** (a slug like `po-discovery-assistant`);
- a **category** — one of `identity / methodology / project / domain /
  skill / context / feedback`;
- a **scope** — `global`, `project`, or `node`;
- one or more **versions** — content is **immutable per version**;
  edits publish a new version (per
  [docs/design/loom-design.md §7](design/loom-design.md)).

Workflows declare what they need by **selectors** (category + slug or
tags). The engine assembles the *effective fragment set* at a node by
walking project → ancestor chain → self, applying the fragments
declared at each level. Local overrides win over inherited.

## Where they live, where you edit them

| Surface | What | Where |
|---|---|---|
| **Seed source** | The C# definitions seeded on first run | [src/Loom.Application/Workflows/Kickoff/SeedFragments.cs](../src/Loom.Application/Workflows/Kickoff/SeedFragments.cs) |
| **Bootstrapper** | Inserts seed fragments if missing (idempotent) | [src/Loom.Infrastructure/Bootstrap/LoomBootstrapper.cs](../src/Loom.Infrastructure/Bootstrap/LoomBootstrapper.cs) — `SeedBootstrapFragmentsAsync` |
| **Editor** | Browse + publish new versions | `/fragments` (Library) → click any fragment → "publish a new version" |
| **API exposure** | What agents receive | MCP `loom.get_node_context(node_ref)` returns the assembled set; `loom.list_rules(scope)` returns compiled rules; CLAUDE.md / .cursorrules are written out by Git Sync |
| **Provenance** | Which version a run actually used | Each Run records `(FragmentId, FragmentVersionId, Version)` triples — see Run Detail page |

To **add** a new fragment to the seed set, append a `SeedFragmentDefinition`
in `SeedFragments.All`; the bootstrapper picks it up next start. To
**edit** an existing one, do it via the Library UI — the bootstrapper
won't overwrite a fragment that already exists, so seed edits only land
on a fresh DB. (For team-wide changes, edit both: seed for new
deployments, UI for existing ones.)

## The seeded library

### identity — *who the agent is*

Personas. Composed at the top of an assembled prompt to set tone,
scope, and self-imposed rules.

| Slug | Title | Used by |
|---|---|---|
| `po-discovery-assistant` | Product Owner Discovery Assistant | Kickoff `discovery` step |
| `po-architect-pair` | Product Owner / Architect pair | Kickoff `decompose` step |
| `dotnet-backend-engineer` | .NET backend engineer (Artio platform) | Build-time backend code generation |
| `blazor-frontend-engineer` | Blazor / Razor Components frontend engineer | Loom UI work |
| `react-spa-engineer` | React SPA engineer (Artio web frontends) | `blankappui`, `opportunitiesappui`, etc. |
| `integration-engineer` | Integration / event-driven engineer | MassTransit consumers, ServiceBus topology |
| `technical-architect` | Technical architect | Architecture sketches + ADR drafts at role gates |
| `qa-engineer` | QA engineer | Test plans + scenarios |

### methodology — *how this team thinks*

Process discipline. Compose into kickoff and gate-evaluation runs so
the agent applies the team's standards.

| Slug | Title | Triggers |
|---|---|---|
| `problem-framing` | Problem framing | Discovery extraction, gate review |
| `definition-of-ready` | Definition of ready | PO gate (kickoff) |
| `definition-of-done` | Definition of done | Phase advance to Done |
| `adr-when-to-write` | When to write an ADR | Tech-lead role gate |

### project — *stack & repo conventions*

Encodes the Fortius / Artio platform's actual conventions across the
repos surveyed in `C:\Data\Fortius`.

| Slug | Title | What it pins |
|---|---|---|
| `fortius-stack-conventions` | Fortius / Artio stack conventions | .NET 8/10, EF Core 8/10, MSSQL, MassTransit + ASB, Serilog, NSwag, FluentValidation, central package management |
| `fortius-clean-architecture` | Clean architecture invariants | Domain → Application → Persistence/Infrastructure → Web/Api layering |
| `fortius-cqrs-mediatr` | CQRS + MediatR pattern | Command/Query records, handler placement, FluentValidation pipeline behaviour |
| `fortius-events-masstransit` | Event-driven patterns with MassTransit | Contract shape, idempotency, saga state machines |
| `fortius-test-conventions` | Test conventions | xUnit v3 (Loom) / NUnit 3 (legacy), Testcontainers, naming |
| `fortius-azure-pipelines` | Azure Pipelines + AKS deployment | Shared template repo, Helm + ACR + AKS, Key Vault for secrets |

### domain — *Artio business glossary & rules*

Knowledge fragments. Compose into runs that touch a specific business
area so the agent doesn't fabricate domain terms.

| Slug | Title | Domain |
|---|---|---|
| `artio-business-glossary` | Artio business glossary | Cross-cutting platform terms (Supplier, Buyer, Goal, Procurement, Marketplace, Opportunity, Notification, Tenancy) |
| `supplier-compliance` | Supplier compliance domain | Accreditations, self-declared answers, per-buyer compliance rules |
| `supply-chain-visibility` | Supply chain visibility domain | Multi-hop tier-N supplier graph, partial-knowledge semantics |
| `procurement-lifecycle` | Procurement lifecycle | Need → Requisition → Approval → PO → Receipt → Invoice → Payment saga |
| `sustainability-goals` | Sustainability goals domain | `goals-api` shape: type / target / baseline / status |

### skill — *discrete agent capabilities*

The "do exactly this" fragments. Each maps to a workflow step.

| Slug | Title | Output |
|---|---|---|
| `transcript-discovery-extraction` | Discovery extraction from a transcript | `DiscoveryObject` JSON |
| `transcript-normalize` | Transcript normalisation | Speaker-tagged turn list (in-proc, no LLM) |
| `feature-decomposition` | Feature decomposition | `DecompositionProposal` JSON |
| `acceptance-criteria-from-intent` | Acceptance criteria from intent + outcomes | Gherkin `Given/When/Then` lines |
| `adr-writer` | Draft an ADR | Markdown matching `docs/adr/_template.md` |
| `efcore-migration-author` | Author an EF Core migration | C# migration class |
| `masstransit-consumer-author` | Author a MassTransit consumer | C# consumer class |
| `openapi-spec-extractor` | Extract / refine an OpenAPI spec | NSwag-shaped spec |
| `react-component` | Generate a React component | TSX/JSX component |
| `test-plan-from-acceptance` | Generate ADO Test Plan from acceptance criteria | ADO Test Plan draft |
| `similar-feature-search` | Find similar prior features | Top-N matches with rationale |

## Notes for the methodology owner

- The seed list is the **floor**, not the ceiling. Once a deployment
  is up, edits happen via the Library UI and produce new versions.
  `LoomBootstrapper` does not overwrite — it only fills in missing
  fragments — so on an existing DB you'll need to bump versions via
  the UI to get changes live.
- Some fragments started life **project-scoped** in the original
  design (e.g. `react-component`, `supplier-compliance`) but are
  seeded **global** because projects don't exist at first-run time.
  Once the team's projects are created, the methodology owner can
  re-scope by publishing a project-scoped version with the same slug
  and deprecating the global one — the engine's scope-walking picks
  the project-scoped variant when assembling at a node within that
  project.
- Annotation-to-fragment promotion (Phase 7) is the long-term
  evolution path: when a feedback annotation recurs across N
  features, the system proposes a `feedback:*` fragment for the
  methodology owner to accept. Until that ships, growing the library
  is a manual exercise on the Library page.
