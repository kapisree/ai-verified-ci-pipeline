You are reviewing a pull request for compliance with the contract in
`SPEC.md` at the root of this repository.

1. Read `SPEC.md` in full.
2. Read the diff for this pull request (e.g. via `git diff` against the
   PR's base ref, or `gh pr diff`).
3. Determine whether the diff introduces any behavior that contradicts
   `SPEC.md` — wrong status codes, missing validation, changed response
   shapes, broken idempotency, leaked exception details, or any other
   deviation from a stated rule.
4. Post a PR comment (e.g. via `gh pr comment`) listing each violation found,
   quoting the specific `SPEC.md` clause it breaks. If there are no
   violations, post a comment confirming the diff is spec-compliant.
5. Return your final answer in the required structured JSON format: set
   `compliant` to `false` if you found at least one violation, `true`
   otherwise, and list every violation found (as plain strings) in
   `violations` (empty array if none).
