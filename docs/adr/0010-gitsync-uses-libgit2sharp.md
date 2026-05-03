# ADR-0010 — GitSync uses LibGit2Sharp, not shell-out to `git`

- **Status**: Accepted
- **Date**: 2026-05-03
- **Decider(s)**: Loom kernel team

## Context

Phase 2's GitSync writer compiles the project's effective fragment set
into `CLAUDE.md` and `.cursorrules` and commits the result into a target
repository. Eventually it pushes to ADO (Phase 2) or GitHub (Phase 5+).
The implementation needs to do the standard read-write-commit-push cycle.

## Decision

Use `LibGit2Sharp` (managed bindings to libgit2). All git operations —
init, stage, commit, push — happen in-process. No `git` CLI dependency.
`LibGit2GitSyncService` is the only consumer; the public seam is
`IGitSyncService`.

## Alternatives considered

- **Shell out to `git`** — universally available on dev machines, but
  unreliable on container hosts (the base image we use for Loom.Web
  doesn't ship `git`) and slow per-call (process spawn). Authentication
  story is also worse: we'd need to manage `~/.gitconfig` /
  credential-helper state for the host process.
- **GitHub / ADO SDK push paths** — work for some operations (e.g. ADO
  has a `Pushes — Create` REST endpoint) but only for systems with that
  REST surface. LibGit2Sharp works against any git remote.
- **Roll our own git wire protocol client** — laughable; mentioned only
  to be dismissed.

## Consequences

- **Easier**: deterministic, in-process behaviour. The Phase-2
  `GitSyncTests.SyncAsync_IsIdempotent` test runs on every CI build
  against a temp working copy without Docker.
- **Easier**: credential providers plug into LibGit2Sharp directly,
  including SSH-key authentication for self-hosted git servers.
- **Harder**: LibGit2Sharp ships a native binary per platform. The
  package handles win-x64 / linux-x64 / osx-x64 (and arm variants) in
  the standard NuGet runtime-targets pattern, so this is invisible in
  practice — but a future build on an exotic platform (alpine-linux-arm
  was missing as recently as 0.27.x) might need extra work.
- **Harder**: object files written to the working copy are read-only;
  Windows test cleanup needs `File.SetAttributes(... Normal)` before
  `Directory.Delete`. Documented in `GitSyncTests`.

## Reversibility

Reversible. `IGitSyncService` is the seam; swapping `LibGit2GitSyncService`
for a shell-out implementation is one new class. The wire format we write
(`CLAUDE.md` / `.cursorrules`) is unchanged either way — the
`FragmentRulesCompiler` is independent of how we commit.
