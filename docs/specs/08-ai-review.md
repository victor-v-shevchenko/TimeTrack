# Step 8: AI review pass and fixes

## Goal
An independent AI review (a different model from the one that wrote the code), a manual
pass over the PLAN.md §10 checklist, fixes with tests, and an up-to-date `docs/ai-log.md`.

## Depends on
Steps 1–7.

## Process
1. Run `/code-review` with Claude Sonnet 5.5 over the whole codebase (or over the range from
   the first commit to HEAD). Save the raw findings temporarily; they are not committed.
2. For each finding, decide: **fix**, **won't fix** (with a reason) or **false positive**.
   Fix only real problems. For each real bug, first add a test that fails, then fix it.
3. Go through the manual checklist below. Treat every issue found as a finding.
4. Add an entry to `docs/ai-log.md` for every real bug found during the whole project (steps
   1–8), not only in this step. Use the template from step 1. Link the commit that fixed it.
   Aim for honesty, not volume. If AI output was wrong only in a minor way, say so.
5. You may make several commits for the fixes. The final commit of the step updates the log.

## Manual checklist (from PLAN.md §10, plus secrets)

| Check | How |
|-------|-----|
| No local/unspecified `DateTime` written to the DB | `git grep -n "DateTime\b" backend/TimeTrack.Api` and review each hit |
| No wall-clock access in the backend | `git grep -nE "DateTime(Offset)?\.(Utc)?Now" backend/TimeTrack.Api` → nothing |
| Report uses overlap and clipping | read `ReportService`; boundary tests exist |
| Running entries counted, no null crash | test exists |
| Partial unique index exists and is mapped to 409 | migration SQL; race test |
| No aggregation EF can't translate | no client-evaluation warnings in logs during tests |
| Frontend date conversion correct | `time.spec.ts` covers the exclusive `to` and DST |
| Live timer cleaned up | `DestroyRef` usage; manual DevTools check |
| Compose waits for a healthy db; service names, not `localhost` | read compose files |
| Tests isolated | each class has its own database; run `dotnet test` twice in a row |
| **No tracked secrets** | `git grep -nIiE "password\s*=\|POSTGRES_PASSWORD:" -- ':!*.example' ':!docs/**' ':!*.md'` → nothing |
| **`.env` never committed** | `git log --all --diff-filter=A --name-only -- .env .devcontainer/.env` → empty |
| **Secrets not in images or env** | step 6 checks 6–7 repeated |
| **Connection string not logged** | `docker compose logs api` and test output contain no `Password=` |

## Acceptance criteria
- [ ] Every review finding has a decision. Those decisions are summarized in the commit body
      (counts per category, plus one line each for the fixes).
- [ ] Every fixed bug has a regression test.
- [ ] `docs/ai-log.md` has at least the real bugs met so far, each with how it was found and a commit link.
- [ ] `dotnet test backend` and `npm test` are green; `docker compose up --build` still works.

## Commit
`Step 8: AI review fixes and AI log` (plus any `fix: …` commits before it)
