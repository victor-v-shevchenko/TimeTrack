# Step 10: Fixes from the external code review

## Goal
Fix four issues found in a code review after step 9: `Location` headers that point nowhere, a race
when two stops arrive at once, an unbounded report period, and out-of-order report responses in the UI.

## Depends on
Steps 1–9.

## Changes

1. **Resolvable `Location` headers.** `POST /api/employees` returns `Location: /api/employees/{id}`
   and `POST /api/employees/{id}/timer/start` returns `/api/employees/{id}/timer`, but neither path
   had a GET. Add:
   - `GET /api/employees/{id}`: the employee with its active entry (same shape as the list); 404 unknown employee.
   - `GET /api/employees/{id}/timer`: the running entry; 404 unknown employee or nothing running.
2. **Stop race.** `StopAsync` read the running entry and saved `stopped_at` through change tracking,
   so two concurrent stops both succeeded and the second overwrote the first stop time. Close the
   entry with one conditional `UPDATE … WHERE id = @id AND stopped_at IS NULL`
   (`ExecuteUpdateAsync`); 0 rows updated → 409 "No running timer", like a stop with nothing running.
3. **Report period limit and ordering.** A report loads every overlapping row into memory, so the
   period is capped at 366 days (`ReportService.MaxPeriodDays`): longer → 422 on `to`. Rows are
   ordered by `started_at, id` in SQL instead of after loading. The employee filter is added to the
   query only when it is set, instead of `employeeId IS NULL OR …`. Durations are still aggregated
   in C# (CLAUDE.md).
4. **Report page: latest wins.** `ReportPage.show()` cancels the previous request before starting
   a new one, as `EmployeesPage.load()` already does, so a slow older response can't replace a newer report.

## Acceptance criteria
- [ ] Following `Location` after creating an employee returns that employee (200); unknown ID → 404.
- [ ] Following `Location` after starting a timer returns the running entry while it runs, 404 after stop.
- [ ] 10 parallel stops on one running timer → exactly one 200 and nine 409; `stopped_at` is the clock time (HTTP and service tests).
- [ ] A period of 366 days + 1 s → 422 with an error on `to`; exactly 366 days → 200.
- [ ] Report entries come back ordered by start time even when inserted out of order.
- [ ] Report page: of two overlapping requests, the newer response wins and the older one is unsubscribed (Vitest).
- [ ] PLAN.md §4 and §9, README API table and `docs/ai-log.md` updated.
- [ ] `dotnet test backend` and `npm test` pass (CI, step 11).

## Commit
`Step 10: Review fixes`
