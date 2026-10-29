# AI log

Bugs and wrong output produced by AI tools, how they were found, and how they were fixed.

## Template
### <short title>
- **Step / commit:** <step N, hash>
- **Tool / model:** Claude Code, Opus 5.5 | /code-review, Sonnet 5.5
- **What the AI produced:** <code or behavior>
- **How it was found:** failing test | /code-review | manual check
- **Fix:** <what changed, commit link>

### Report listed running entries for periods in the future
- **Step / commit:** step 3, `667d316`
- **Tool / model:** Claude Code (writing model not recorded)
- **What the AI produced:** the overlap filter treated a running entry (`stopped_at IS NULL`) as open-ended, so it
  matched any period after its start. A report for next week listed every running entry with 0 s instead of showing
  the empty state, although the duration was correctly clipped to 0.
- **How it was found:** manual check in the clean-clone run of the seeded stack (step 9): next-week report returned 2 entries.
- **Fix:** a running entry ends at `now` for the overlap test (`(StoppedAt ?? now) > from`), with a failing test first,
  `Step 9: README`.

### Seed script numbered weekdays before applying LIMIT
- **Step / commit:** step 7, `Step 7: Seed data`
- **Tool / model:** Claude Code, Sonnet 5.5
- **What the AI produced:** `days` CTE computed `row_number()` over all 15 candidate weekdays in the same
  query as `ORDER BY … LIMIT 10`, so `n = 5` (used for the midnight-crossing entry) did not point at
  one of the 10 selected days and the entry was silently not inserted.
- **How it was found:** manual check — querying for entries with `stopped_at::date <> started_at::date`
  after the first run returned only Alice's running entry.
- **Fix:** select the 10 days in a subquery and number them in the outer query (same commit).

### Exception handler depended on the Accept header
- **Step / commit:** step 4, `d4eac25`
- **Tool / model:** Claude Code (writing model not recorded)
- **What the AI produced:** `AppExceptionHandler` returned the result of `IProblemDetailsService.TryWriteAsync`,
  which is `false` when `Accept` excludes JSON. The exception then escaped as a bodyless 500 instead of the 404/409/422.
- **How it was found:** /code-review, Sonnet 5.5; reproduced with a failing test (`Accept: text/html`).
- **Fix:** write `application/problem+json` directly, `ba8456b`.

### Timer start validated the body before the employee existed
- **Step / commit:** step 3, `667d316`
- **Tool / model:** Claude Code (writing model not recorded)
- **What the AI produced:** `StartAsync` checked the description first, so `POST /employees/9999/timer/start` with a
  blank description returned 422, while `Stop` correctly returned 404.
- **How it was found:** /code-review, Sonnet 5.5; failing test first.
- **Fix:** existence check moved first, `ba8456b`.

### Employee row ignored the DestroyRef rule
- **Step / commit:** step 5, `e07944a`
- **Tool / model:** Claude Code (writing model not recorded)
- **What the AI produced:** `request.subscribe` in `EmployeeRow.run()` had no `takeUntilDestroyed`, although
  CLAUDE.md requires it and the other components do it. A late response updated signals and emitted on a destroyed row.
- **How it was found:** /code-review, Sonnet 5.5; failing test (destroy while request in flight).
- **Fix:** `takeUntilDestroyed(this.destroyRef)`, `5effdad`.

### Minor: UTC conversion outside `core/time.ts`, stale list handling
- **Step / commit:** step 5, `e07944a`
- **Tool / model:** Claude Code (writing model not recorded)
- **What the AI produced:** `toISOString()` called in `ApiService` (CLAUDE.md: only in `time.ts`); a new employee
  appended to the end of the list instead of reloading in server order; overlapping list loads could apply a stale response.
- **How it was found:** /code-review, Sonnet 5.5. Low impact: the output was correct for the common path.
- **Fix:** `toUtcIso` in `time.ts`, reload after add, latest-wins load, `5effdad`.

### Minor: step 2 spec/tooling assumptions were wrong
- **Step / commit:** step 2, `c8eb567`
- **Tool / model:** Claude Code (plan and specs)
- **What the AI produced:** the spec listed a KeyPerFile package (it ships with the shared framework), and omitted
  `global.json` for the Microsoft.Testing.Platform runner and the EF Core Relational pin that avoids a version conflict.
- **How it was found:** build and test failures while implementing.
- **Fix:** spec corrected in the same commit.

### Minor: dev container created `frontend/` and `node_modules` root-owned
- **Step / commit:** step 5, `e07944a`
- **Tool / model:** Claude Code
- **What the AI produced:** a `postCreateCommand` that did not chown the frontend directory and the `node_modules` volume.
- **How it was found:** `npm install` failed with permission errors.
- **Fix:** chown added to `postCreateCommand`, same commit.
