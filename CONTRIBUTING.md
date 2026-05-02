# Contributing to Loom

Loom is an opinionated codebase. Read this in full before your first PR.

## Before you start

1. Read the README for orientation.
2. Read `docs/design/loom-design.md` if you'll touch architecture.
3. Skim `docs/adr/`. Don't propose anything that contradicts an accepted ADR
   without proposing a new ADR alongside that supersedes it.
4. Read `CLAUDE.md` even if you're not using AI tooling — the conventions
   apply to all contributors.

## Working on the code

```bash
git clone <this repo>
cd loom
dotnet restore
dotnet build
dotnet test
```

The .NET SDK version is pinned in `global.json`. If your installed SDK is
older, install the version specified there.

For integration tests you need Docker running locally (Testcontainers
spins up MSSQL).

For the agent runtime work, you will eventually need an Azure subscription
with Foundry access, but Phase 1 work mostly does not require this —
domain, application, infrastructure, and migrations can all be developed
without an LLM credential.

## Branch and PR flow

- Branch off `main`.
- Conventional commit messages preferred:
  - `feat:` user-visible feature
  - `fix:` bug fix
  - `refactor:` no behaviour change
  - `chore:` tooling, deps, config
  - `docs:` docs only
  - `test:` tests only
- One logical change per PR. Multiple unrelated changes get hard to review.
- PR description follows the template; the "why" matters more than the "what".

## Domain code rules

- No I/O. Domain tests must run without a database, filesystem, or HTTP.
- No service-locator. Constructor injection only.
- Behaviour belongs on the entity. If a service wants to mutate an entity's
  state directly, the entity probably needs a method.
- Records for value objects, classes for entities. Records that need
  invariants get a private constructor and a `Create` factory.

## Migrations

- Once a migration ships in `main`, it is immutable. Schema change → new
  migration.
- See `src/Loom.Infrastructure/Persistence/Migrations/README.md` for the
  current state and how to regenerate the initial migration.

## Methodology changes

The team's *working practices* (how features are framed, how decisions get
recorded, how reviews work) belong in fragment definitions inside Loom
itself, not in this repository's code. As Loom matures it will be the
authoritative source for its own methodology — the dogfooding angle.

For now, propose methodology changes by drafting an ADR or by editing
`CLAUDE.md` directly with reasoning in the PR description.
