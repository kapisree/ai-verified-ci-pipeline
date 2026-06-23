# AI-Verified CI Pipeline — Design

## Purpose

Demonstrate an automated "verified coding" gate: a CI pipeline that blocks
merge unless a pull request (1) complies with a written spec, as judged by
Claude Code, (2) passes its test suite, (3) passes lint/static analysis, and
(4) passes a security scan. This repo is a self-contained, runnable demo of
the pattern — not a production application, and not a generic reusable
GitHub Action. It exists to show the pattern working end-to-end, including
the failure case (a spec-violating PR getting blocked).

## Scope

In scope:
- A small ASP.NET Core sample API to give the gate something real to check.
- A written spec (`SPEC.md`) precise enough for Claude to judge compliance.
- A GitHub Actions workflow with four required checks: Claude spec review,
  tests, lint/analyzers, security scan.
- Branch protection configuration (documented, since it's a repo setting,
  not a file) requiring all four checks before merge.
- A README walking through how to demo the gate, including how to trigger
  a deliberate spec violation to show the block in action.

Out of scope:
- Turning this into a publishable/reusable GitHub Action for other repos.
- A production-grade application (auth, persistence beyond in-memory,
  deployment).
- Any CI platform other than GitHub Actions.

## Sample App: Task API

A minimal ASP.NET Core Web API (.NET 8, minimal API style, in-memory store)
implementing CRUD for simple tasks. Chosen because it's small enough for
`SPEC.md` to describe completely, while still offering enough surface
(validation, status codes, business rules) for the spec-compliance check,
tests, lint, and security scan to each have something meaningful to do.

Endpoints:
- `POST /tasks` — create a task. Body: `{ "title": string, "description"?: string }`.
  - `title` is required, 1–200 chars. Missing/empty/too-long → `400`.
  - On success → `201` with the created task (`id`, `title`, `description`,
    `isComplete: false`, `createdAt`).
- `GET /tasks` — list all tasks → `200`, array (empty array if none).
- `GET /tasks/{id}` — get one task → `200` with task, or `404` if not found.
- `PUT /tasks/{id}` — update `title`/`description`. Same validation as
  create. `200` with updated task, `404` if not found, `400` if invalid.
- `POST /tasks/{id}/complete` — mark a task complete (idempotent: calling
  twice is not an error) → `200` with updated task, `404` if not found.
- `DELETE /tasks/{id}` — delete a task → `204`, or `404` if not found.

Non-functional rules (also part of the spec, deliberately checkable):
- Every mutating endpoint (`POST /tasks`, `PUT /tasks/{id}`) must validate
  `title` and return `400` with a body describing the validation error on
  failure — never throw an unhandled exception for bad input.
- No endpoint may leak stack traces or internal exception details in a
  response body.

## Written Spec (`SPEC.md`)

Lives at the repo root. Contains exactly the endpoint table and
non-functional rules above, written as the contract Claude checks PRs
against. It is intentionally precise (specific status codes, specific
validation bounds) so a future PR can plausibly drift from it — e.g.
someone "simplifies" validation and returns `500` instead of `400`, or
removes the idempotency guarantee on complete — giving the demo a concrete,
realistic failure to show.

## CI Gate Design

### Trigger
`pull_request` events (opened, synchronize, reopened) targeting `main`.

### Jobs (all run in parallel, all required status checks)

1. **`test`** — `dotnet test` against the xUnit test project. Fails the
   check on any test failure.
2. **`lint`** — `dotnet format --verify-no-changes` (style) plus build with
   Roslyn analyzers enabled and warnings-as-errors for analyzer-flagged
   rules. Fails on any formatting diff or analyzer error.
3. **`security`** — Trivy filesystem/dependency scan (`trivy fs .` or
   equivalent action) against the repo, failing on HIGH/CRITICAL findings.
4. **`claude-spec-review`** — Anthropic's `claude-code-action` (official
   GitHub Action), given:
   - `SPEC.md` as the contract to check against.
   - The PR diff (base...head) as the change to evaluate.
   - A review prompt (`.github/claude-review-prompt.md`) instructing Claude
     to: identify any way the diff violates `SPEC.md`, post a PR comment
     summarizing findings, and exit non-zero if any violation is found
     (zero/pass if compliant). Requires an `ANTHROPIC_API_KEY` repository
     secret.

### Branch Protection
`main` is configured (documented in README, since this is a GitHub repo
setting rather than a file) to require all four checks above to pass
before merge is allowed, and to require the branch to be up to date.

## Repo Layout

```
/SPEC.md
/src/TaskApi/                          (ASP.NET Core minimal API project)
/tests/TaskApi.Tests/                  (xUnit test project)
/.github/workflows/verified-ci.yml     (the four-job pipeline)
/.github/claude-review-prompt.md       (instructions for the Claude review job)
/README.md                             (demo walkthrough, including how to
                                         trigger and observe a blocked PR)
```

## Demonstrating the Failure Case

The README documents a concrete recipe: branch from `main`, change the
`complete` endpoint (or validation behavior) in a way that contradicts
`SPEC.md` but still compiles and passes existing tests, open a PR, and show
the `claude-spec-review` check fail with Claude's PR comment explaining
which spec clause was violated — while `test`/`lint`/`security` may still
pass, illustrating why spec-compliance review is a distinct, necessary
gate alongside conventional checks.

## Testing Strategy

- Unit/integration tests (xUnit + `WebApplicationFactory`) cover each
  endpoint's success and validation-failure paths, directly mirroring
  `SPEC.md`'s status-code rules.
- The CI workflow itself is validated by exercising it on a real PR against
  this repo (both a compliant PR and the deliberate spec-violating PR from
  the demo recipe above) rather than by separate workflow unit tests.

## Open Questions / Risks

- `claude-code-action` requires `ANTHROPIC_API_KEY` to be set as a repo
  secret before the `claude-spec-review` job can run; this is a manual
  one-time setup step called out in the README, not something committable.
- Branch protection rules are a GitHub repo setting, not version-controlled
  config; the README documents the exact settings to enable manually (or
  optionally via `gh api`) since GitHub does not support committing branch
  protection as a file in this repo's plan scope.
