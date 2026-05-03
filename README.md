# Loom

*A methodology hub for AI-augmented software development, from concept to production.*

Loom is the connective tissue between humans and AI tools across the full lifecycle of a feature. It treats every feature as a living **context node** with structured intent, dynamic artifacts, an activity stream, and a fragment-assembled prompt environment that any AI tool — Claude, Cursor, Foundry-hosted agents, headless Claude Code — can read from and contribute back to.

## What problem this solves

AI tools today create private context bubbles: the PO's ChatPRD session, the dev's Cursor session, the tester's Claude conversation each hold context the others can't see. Loom is the shared substrate. Humans collaborate in their preferred tools (Figma, Visual Studio, Teams, ADO) while Loom keeps the *context*, the *provenance*, and the *methodology* coherent across them.

The product replaces three things:

1. **Private AI conversations no one else benefits from.** Loom captures the assembled prompt, fragments used, and outputs against a feature node so every run informs the next.
2. **Ticket-board ceremony that fragments thinking.** Features live as a hierarchical tree (initiative ▸ feature ▸ capability ▸ slice). Status is *derived*, not dragged.
3. **Manual labour keeping PRDs, designs, code, and tests aligned.** Each artifact has a canonical home (Figma, git, Confluence, Loom-native) and Loom is the index, not a duplicate store.

## Lifecycle in one diagram

```
Teams transcript → kickoff (extract, decompose) → ◇ PO gate ◇
   → enrich (acceptance, UX, arch, risks) → ◇ role gates ◇
   → build (Cursor / Claude Code via MCP, Foundry background)
   → test → merge → deploy → feedback fragments
```

Two human gates only. Everything else automated but auditable.

## Repository layout

```
src/
  Loom.Domain          entities, value objects, no dependencies
  Loom.Application     services, workflow engine, fragment composition
  Loom.Infrastructure  EF Core, MSSQL, Elasticsearch, Blob
  Loom.Agents          IAgentRuntime, FoundryAgentRuntime, router
  Loom.Integrations    ADO, Atlassian, Figma, Miro, Graph adapters
  Loom.Mcp             MCP server (read+write tools)
  Loom.Web             Blazor Server app, SignalR hubs
  Loom.Contracts       shared DTOs, MCP tool schemas

tests/                 mirrors src/ structure
docs/
  adr/                 architecture decision records
  design/              design overview and screen mockups
deploy/                bicep + docker
```

## Getting started

**Prerequisites**

- .NET 10 SDK (see `global.json`)
- SQL Server (LocalDB on Windows, or `mcr.microsoft.com/mssql/server` via Docker)
- An IDE: Visual Studio, Rider, or VS Code with the C# Dev Kit

**Build and test**

```bash
dotnet restore
dotnet build
dotnet test
```

**Apply database migrations**

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Loom.Infrastructure --startup-project src/Loom.Web
```

**Seed sample data (optional, for local development)**

```bash
dotnet run --project tools/seed
```

**Configure the Anthropic API key**

The Phase-1 kickoff workflow calls Anthropic for the discovery and decompose
agent steps. Set the API key in user-secrets (recommended for dev) or in
configuration:

```bash
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project src/Loom.Web
```

The default model is `claude-opus-4-7`; override with `Anthropic:DefaultModel`
in user-secrets or `appsettings.json`.

**Run the web app**

```bash
dotnet run --project src/Loom.Web
```

By default this listens on `https://localhost:5001`. In Development the
Entra ID auth flow is bypassed (`AzureAd:Enabled = false` in
`appsettings.Development.json`); a synthetic dev user is signed in
automatically. For any other environment, fill in the `AzureAd` section
with a real tenant id, client id, and domain before running.

**The Phase-1 demo path**

1. Visit `/` — the Operating Picture. On a fresh database the empty-state
   screen offers a kickoff link; the kickoff page auto-creates a default
   project on first use.
2. Click **+ Kickoff a new feature**. Paste a Teams-style transcript
   (lines like `Anna: We need express checkout.`) and submit.
3. Loom runs the workflow: `normalize` (in-proc) → `discovery` (Anthropic)
   → pause at the PO gate. The page redirects to `/runs/{id}/gate`.
4. Review the discovery, edit if needed, click **Accept and continue to
   decompose**. The engine runs the decompose step and pauses at the
   second gate.
5. Review the proposed children, accept the ones you want, click
   **Accept selected & finish kickoff**. The page redirects to the new
   feature's workspace at `/n/{node-id}` showing the populated tree.

## Working with AI tools in this repo

This repo is designed to be edited with AI assistance. See `CLAUDE.md` at the root — it describes how Claude Code, Cursor, and other AI tools should treat the codebase. The same content is mirrored to `.cursorrules` for Cursor users.

When AI-assisted changes warrant it, record an architecture decision under `docs/adr/`. Decisions about *methodology* (how the team works) belong in fragment definitions inside Loom itself; decisions about the *codebase* (how Loom is built) belong as ADRs here.

## Project status

Pre-alpha. See `docs/design/loom-design.md` for the full design overview and `docs/design/loom-screens.html` for screen drafts. Build sequence is in the design doc — Phase 1 is the kernel (current).

## License

Proprietary, internal use.





# Loom — how it works, and why

## The thinking behind it

Software teams now have powerful AI tools, but each one creates a private context bubble. The product owner's ChatPRD session, the developer's Cursor session, the tester's Claude conversation — each holds context the others can't see. Work that should compound across the team instead evaporates at the edges of individual sessions. Meanwhile, the older problems haven't gone away: PRDs drift from designs, designs drift from code, code drifts from tests, and the methodology that's supposed to hold it all together lives in a wiki nobody reads.

Loom is built on three convictions about how to fix this.

The first is that **the unit of work is not a ticket**. Tickets fragment thinking into cards that move through columns; the moment you break a feature into Jira tasks, you've lost the thread of *why* it exists. Loom's unit is the **context node** — a living workspace that holds intent, outcomes, constraints, open questions, attached artifacts, and a stream of activity. The same shape applies at every level: an initiative is a context node, a feature is a context node, a slice ready for a developer is a context node. Status is *derived* from what's actually happening, not set by someone dragging a card.

The second is that **methodology should be executable, not aspirational**. Most teams have a written methodology and a real one, and they aren't the same. Loom encodes the team's working practices as a library of small, reusable **prompt fragments** — identity ("you are a PO discovery assistant"), methodology ("separate problems from solutions"), project ("this codebase uses these conventions"), domain ("our glossary"), skill ("extract acceptance criteria"). Every AI run on every node assembles these fragments into context. When the team learns a new lesson, it becomes a fragment, and every future run benefits. The methodology compounds instead of decaying.

The third is that **AI tools should compete on what they're good at, not on owning context**. Loom doesn't replace Claude, Cursor, or Copilot — it sits underneath them. Through the Model Context Protocol, any AI tool the team already uses can read the current node's assembled context and write contributions back. The PO's ChatPRD draft, the developer's Cursor session, and the tester's Claude conversation are all working from the same substrate. People stay in their preferred tools; context stays coherent across them.

## Two human gates, everything else automated

Loom makes one structural commitment about where humans get involved: there are exactly **two gates** between concept and build. After kickoff, the PO reviews and accepts the proposed feature tree. After enrichment, the relevant role (UX, tech lead, security) reviews and accepts the artifacts produced for each node. Everything else — extracting structure from a transcript, retrieving similar prior work, decomposing into capabilities, drafting acceptance criteria, generating wireframes, sketching architecture — happens automatically but auditably.

The discipline isn't "remove humans from the loop". It's "spend human attention where it matters most". The PO gate is where the team commits to a shape; the role gates are where craft expertise lands. Everywhere else, the cost of automation is low and the value of speed is high.

## The lifecycle, end to end

A feature begins with a meeting. The PO drops the Teams transcript into Loom, and a kickoff workflow runs in seconds. It reads the transcript, extracts a structured **Discovery object** — problem statement, desired outcomes, constraints, stakeholders, hypotheses, open questions, and crucially any *dissent* from the meeting. It searches the team's history for similar prior work and surfaces what was decided last time. Then it proposes a tree: probably a feature with a few candidate capabilities under it, each with its own intent and questions.

The PO sits down to the **kickoff gate** with three panes: the transcript on the left, the extracted discovery in the middle, the proposed tree on the right. They edit any field, drag nodes around, split or merge them, then accept. The whole gate is intentionally friction-bearing — this is the moment where the team commits to a shape, and one-click acceptance would be exactly the wrong design.

Once accepted, each node enters **enrichment**. Multiple agent runs fire in parallel against the same node, each composing different fragments. A PO assistant drafts acceptance criteria. A UX designer agent generates wireframes — real React stubs or Figma frames, attached to the node. An architect agent sketches a high-level approach and identifies risks. A risk reviewer enumerates what could go wrong. Each artifact is attached to the node with full provenance: which fragments produced it, which inputs it consumed, which run it came from.

The relevant humans get notified — UX about the wireframes, the tech lead about the architecture sketch — and they open the node in their preferred tool. UX inspects the wireframes in Figma, leaves structured annotations ("this CTA hierarchy is wrong because…"). Tech lead annotates the architecture. Annotations don't just sit there: they automatically become **feedback fragments** attached to the node. When the agent re-runs to iterate, it composes the original fragments plus the new feedback, and the next version carries memory of what was rejected and why. Recurring annotations across many features eventually get promoted to permanent project fragments — which is how the team's design taste gets encoded in the system rather than living in one person's head.

When a slice has cleared its gates, it's **ready for build**. The developer in Cursor or Claude Code asks for context on that slice, and Loom's MCP server returns the full assembled package: intent, accepted criteria, wireframe links, architecture notes, parent feature rationale, similar prior code. The handoff from concept-side to build-side is a single tool call. The developer codes; commits push back through the same provenance machinery; tests get drafted from acceptance criteria; the build runs; the merge happens. Each step updates the node, so anyone glancing at the **Operating Picture** — Loom's tree home — sees real status pulses derived from what's actually happening, not what someone claimed in standup.

## What the team experiences

The PO opens Loom in the morning and sees the Operating Picture: a tree of every initiative, with phase pulses showing which nodes are moving and which have stalled. Two notifications at the top: "your kickoff for CVV revalidation is ready to review" and "Jules approved the saved-card wireframes". She opens the kickoff, scans the discovery, edits one outcome, and accepts.

The UX designer sees an annotation request on a wireframe. He opens it in Figma directly from the Loom notification, leaves four annotations, and goes back to other work. Behind the scenes, those annotations have already become a feedback fragment; the next wireframe iteration runs overnight on cheaper background compute and is waiting for him in the morning.

The developer is in Cursor, finishing a different feature. She types "load context for saved-card-list-component". Cursor pulls the full node context via MCP — acceptance criteria the PO accepted, wireframes UX approved, architecture notes the tech lead signed off on, the relevant project conventions for React and MSSQL patterns. She codes against real specifications, not interpreted ones, and her PR description is generated from the node's intent.

The tester opens the node when the PR lands. The same acceptance criteria that anchored development now anchor testing; test cases get drafted automatically from them; he reviews and tightens them. The trace from intent through criteria through tests is a query, not a hunt.

The methodology owner — usually the tech lead or a senior PO — opens the **fragment library** weekly. She sees usage statistics: which fragments compose into the most runs, which produce consistently good output, which generate annotations. Three new feedback patterns have recurred enough times to be promoted to permanent fragments; she reviews and accepts. The team's methodology improved this week, automatically, because the system noticed what the team was correcting.

## What's different about this

Three things, beyond the surface-level "AI tools that talk to each other".

The **fluid hierarchy** matters more than it looks. There are no tickets, no columns, no story points to argue about. A feature is a tree of nodes that can be split, merged, promoted, or demoted at any time without losing history. Status emerges from real activity. The PO is freed from being a card-shuffler and can focus on intent and decisions.

The **fragment library** matters most of all. It's the difference between a tool that helps you do this feature faster and a tool that makes the team better at every subsequent feature. Each annotation, each correction, each accepted artifact teaches the system; recurring patterns become fragments; fragments compose into every future run. The methodology you wish your team practised becomes the methodology your team actually practises, because it's executed every time anyone does anything.

The **provenance discipline** matters when something goes wrong. Every artifact — a draft, a wireframe, a piece of code — carries a chain back to the run that produced it, the fragments composed into that run, the inputs consumed, the human edits applied since. When a wireframe looks wrong six weeks later, you can answer not just "what did it look like" but "why did it end up that way, and who accepted it". This is what makes AI-augmented teams trustworthy at scale; without it, the work is fast but the team can't reason about it.

## What Loom is not

It is not a project management tool. It does not track velocity, sprint goals, or time. It does not replace Jira or Azure DevOps for compliance, audit, or whatever the broader organisation needs from a system of record — features link out to those systems and stay in sync via webhooks. It is not a chat product; the AI conversations it captures are work artifacts attached to nodes, not standalone threads.

It is also not a finished product. It is a working hypothesis about a methodology, encoded as software, that will earn its keep only if the team using it is honest about which parts make work better and which parts add ceremony. The fragment library is the heart of that test: if the team adds fragments and runs improve, Loom is doing its job. If the library bloats with unused content, that's a signal the methodology needs rethinking, not the tool.

## In one paragraph

Loom is a methodology hub for AI-augmented software development. Features live as a hierarchical tree of context nodes, each with structured intent and a stream of attached artifacts. The team's working practices — how they frame problems, decompose work, validate designs, write tests — are encoded as a library of versioned prompt fragments. AI tools across the team's stack (Claude, Cursor, Foundry-hosted agents) read assembled context from Loom and write contributions back, with full provenance for every artifact. Two human gates anchor each feature: the PO accepts the proposed shape after kickoff, and role-specific gates accept the artifacts after enrichment. Everything else is automated. The methodology compounds as feedback annotations become reusable fragments, so every feature makes the team better at the next one.
