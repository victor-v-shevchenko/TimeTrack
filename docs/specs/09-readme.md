# Step 9: README and clean-clone verification

## Goal
A reviewer who has only Docker can clone the repo, follow the README and use the app in a few
minutes. The README also covers the AI usage the task asks for.

## Depends on
Steps 1–8.

## Files

    README.md
    docs/screenshot.png                      # employees page with a running timer, seeded data

## README structure (PLAN.md §11)

1. **TimeTrack**: a one-paragraph overview and the screenshot.
2. **Quick start**

       git clone <repo> && cd TimeTrack
       cp .env.example .env          # PowerShell: Copy-Item .env.example .env
       # edit POSTGRES_PASSWORD in .env
       docker compose up --build
       # optional: load demo data (see seed/README.md)

   Open http://localhost:8080. Add one line on persistence: `down` keeps data, `down -v` wipes it.
3. **Development**: prerequisites (Docker, VS Code + Dev Containers); copy
   `.devcontainer/.env.example` to `.devcontainer/.env`; "Reopen in Container"; the dev loop
   commands; how to run the tests. Swagger UI is at http://localhost:5000/swagger.
4. **Configuration and secrets**: a short version of PLAN.md §6. Credentials come from `.env`;
   connection strings are Compose secrets at `/run/secrets`; nothing secret is committed; the
   limits of local Compose secrets; how to run the API outside Docker
   (`ConnectionStrings__Default` env var).
5. **Design notes**: one running timer (service check + partial unique index → 409); UTC
   everywhere and local time only in the UI; half-open report periods with clipping and running
   entries counted to now; migrations applied on startup; `TimeProvider` for testable time.
6. **API**: the endpoint table from PLAN.md §4 and an example `curl` for a report.
7. **Tests**: what is covered, mapped to the task's required scenarios (restart timer, empty
   report, persistence), and the use of real Postgres with a database per test class.
8. **AI tools**: Claude Code with Opus 5.5 (implementation, based on PLAN.md + specs +
   CLAUDE.md) and Sonnet 5.5 (`/code-review`). Explain why a different model was used for review.
9. **Example prompts**: 3–5 real prompts, copied verbatim from the session history (e.g. the
   one that produced PLAN.md, a step implementation prompt, a review prompt, a bug-fix prompt).
10. **Errors found and fixed**: a summary of `docs/ai-log.md` (a table: bug → found by → fix),
    with a link to the full log.
11. **Possible improvements**: editing and deleting entries, CSV export, authentication,
    pagination, timezone settings per user, an external secret store, splitting the dev
    container per frontend/backend, CI pipeline.

Keep it factual and short; link to PLAN.md and `docs/specs/` for depth.

## Clean-clone verification
Do this in a fresh directory on the Windows host, not in the working copy:

1. `git clone` the repo; confirm no `.env` exists.
2. Follow the Quick start **literally**, copying the commands from the README.
3. Run the seed. Go through: add an employee → start → stop → restart → report for this
   week → report for next week (empty state).
4. `docker compose down` → `up` → the data and the running timer are still there.
5. Open the clone in the Dev Container following the README; run `dotnet test backend` and `npm test`.
6. Fix any README gap you find in the same commit.

## Acceptance criteria
- [x] All clean-clone steps succeed without steps that aren't documented. (Verified from a GitHub clone in the Linux dev container with a remapped port, and on the Windows host in PowerShell: clone, `Copy-Item`, `compose up --build`, PowerShell seed, report, start/stop/restart, empty next-week report, `down` → `up` keeps data and the running timer. Reopen in Container checked by the user: `dotnet test backend` 59 passed, `npm test` 33 passed, `npm start` OK on :4200. Swagger returned 404 until `containerEnv` `ASPNETCORE_ENVIRONMENT=Development` was added to `devcontainer.json` (no `launchSettings.json`, so `dotnet watch` ran as Production); after a rebuild Swagger loads at :5000/swagger.)
- [x] Every command in the README was actually run. (Run: clone, `cp` and `Copy-Item`, compose up/down, seed in bash and PowerShell, dotnet test, npm test; the report `curl` in bash in the Linux container and via PowerShell on the host; dotnet watch, npm start and Swagger UI in the rebuilt Dev Container.)
- [x] The README links to PLAN.md, `docs/specs/`, `docs/ai-log.md` and `seed/README.md`, and all of them resolve.
- [x] No placeholder text is left (`TODO`, `<repo>` replaced by the real URL if it is known).

## Commit
`Step 9: README`
