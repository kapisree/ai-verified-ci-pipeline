# AI-Verified CI Pipeline (Demo)

A small ASP.NET Core "TaskApi" plus a GitHub Actions pipeline that blocks
merge unless a pull request passes four required checks: a Claude Code
spec-compliance review, tests, lint/analyzers, and a security scan. This
is a demo of the pattern, not a production service.

## What's here

- `SPEC.md` — the written contract `claude-spec-review` checks PRs
  against.
- `src/TaskApi` — the sample app: an in-memory task CRUD API.
- `tests/TaskApi.Tests` — xUnit tests mirroring `SPEC.md`.
- `.github/workflows/verified-ci.yml` — the four-job pipeline.
- `.github/claude-review-prompt.md` — instructions given to Claude for
  the spec-compliance job.

## One-time setup

1. Add an `ANTHROPIC_API_KEY` repository secret (Settings → Secrets and
   variables → Actions → New repository secret). Required for the
   `claude-spec-review` job.
2. Enable branch protection on `main` (Settings → Branches → Add rule):
   - Require status checks to pass before merging.
   - Select all four checks: `test`, `lint`, `security`,
     `claude-spec-review`.
   - Require branches to be up to date before merging.

## Running locally

```bash
dotnet test TaskApi.sln
dotnet format TaskApi.sln --verify-no-changes
```

## Demonstrating a blocked merge

1. Branch from `main`: `git checkout -b demo/spec-violation`.
2. Edit `src/TaskApi/Program.cs` so the `complete` route is no longer
   idempotent in a way that still compiles and still passes the existing
   tests — for example, change `POST /tasks/{id}/complete` to return
   `409 Conflict` on the second call instead of `200`. This contradicts
   the idempotency rule in `SPEC.md` but isn't covered by every test, so
   it can slip past `test`/`lint`/`security`.
3. Push the branch and open a PR against `main`.
4. Watch the `claude-spec-review` check: it reads `SPEC.md`, finds the
   idempotency rule violated, posts a PR comment quoting the rule, and
   fails the check — blocking merge even though the other three checks
   are green.
5. Revert the change to see the same PR pass once it's spec-compliant
   again.
