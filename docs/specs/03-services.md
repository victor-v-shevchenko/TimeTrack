# Step 3: Employee, timer and report services

## Goal
All business rules live in three services, tested against real Postgres with a fake clock.
There is no HTTP yet.

## Depends on
Step 2.

## Files

    backend/TimeTrack.Api/Services/AppException.cs
    backend/TimeTrack.Api/Services/EmployeeService.cs
    backend/TimeTrack.Api/Services/TimerService.cs
    backend/TimeTrack.Api/Services/ReportService.cs
    backend/TimeTrack.Api/Contracts/*.cs                  # records returned by services
    backend/TimeTrack.Tests/TimerServiceTests.cs
    backend/TimeTrack.Tests/ReportServiceTests.cs
    backend/TimeTrack.Tests/EmployeeServiceTests.cs

PLAN.md lists only `TimerService` and `ReportService`. `EmployeeService` is added so that
employee validation is handled the same way as the rest.

## Errors

    public abstract class AppException(int status, string title, string detail) : Exception(detail)
    {
        public int Status { get; } = status;
        public string Title { get; } = title;
    }
    public sealed class NotFoundException(string detail)   : AppException(404, "Not found", detail);
    public sealed class ConflictException(string detail)   : AppException(409, "Conflict", detail);
    public sealed class ValidationException(string field, string message)
        : AppException(422, "Validation failed", message) { public string Field { get; } = field; }

Step 4 maps these to `ProblemDetails`. Services never return HTTP types.

## Contracts (records, camelCase in JSON)

    EmployeeDto(int Id, string Name, DateTimeOffset CreatedAt, ActiveEntryDto? ActiveEntry)
    ActiveEntryDto(long Id, string Description, DateTimeOffset StartedAt)
    TimeEntryDto(long Id, int EmployeeId, string Description, DateTimeOffset StartedAt, DateTimeOffset? StoppedAt)
    ReportDto(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<ReportEntryDto> Entries, IReadOnlyList<ReportTotalDto> Totals)
    ReportEntryDto(long Id, int EmployeeId, string EmployeeName, string Description,
                   DateTimeOffset StartedAt, DateTimeOffset? StoppedAt, long DurationSeconds)
    ReportTotalDto(int EmployeeId, string EmployeeName, long TotalSeconds)

## EmployeeService
- `CreateAsync(string? name)`: trim. Empty → `ValidationException("name", ...)`; longer than 100 →
  `ValidationException`. An existing name (exact match after trim) → `ConflictException`. Also
  catch unique violation 23505 on `ux_employees_name` → `ConflictException` (race).
  `CreatedAt = clock.GetUtcNow()`.
- `ListAsync()`: all employees ordered by name, each with its running entry or `null`. Use one
  query (projection with a subquery), not N+1.

## TimerService
- `StartAsync(int employeeId, string? description)`:
  1. Trim the description. Empty or longer than 500 → `ValidationException("description", ...)`.
  2. Unknown employee → `NotFoundException`.
  3. A running entry already exists → `ConflictException("Timer is already running for this employee.")`.
  4. Insert with `StartedAt = clock.GetUtcNow()`.
  5. If `SaveChangesAsync` throws `DbUpdateException` with `PostgresException { SqlState: "23505",
     ConstraintName: AppDbContext.OneRunningIndex }` → `ConflictException` (same message).
  6. Return `TimeEntryDto`.
- `StopAsync(int employeeId)`:
  1. Unknown employee → `NotFoundException`.
  2. No running entry → `ConflictException("No running timer for this employee.")`.
  3. `StoppedAt = clock.GetUtcNow()`; save; return `TimeEntryDto`.

## ReportService
`GetAsync(DateTimeOffset from, DateTimeOffset to, int? employeeId)`:
1. Convert both bounds to UTC (`ToUniversalTime()`). If `from >= to` → `ValidationException("to", "'to' must be after 'from'.")`.
2. If `employeeId` is set and unknown → `NotFoundException`.
3. `now = clock.GetUtcNow()`.
4. Load overlapping entries with employee names (`AsNoTracking`):
   `StartedAt < to && (StoppedAt == null || StoppedAt > from)`, plus the optional employee filter.
5. For each entry, in memory:
   `start = max(StartedAt, from)`, `end = min(StoppedAt ?? now, to)`,
   `DurationSeconds = max(0, (long)Math.Floor((end - start).TotalSeconds))`.
6. `Entries` ordered by `StartedAt`, then `Id`. `StoppedAt` is returned unclipped (`null` for running).
7. `Totals`: one row for **every** employee (or only the filtered one), ordered by name.
   `TotalSeconds` is the sum of that employee's `DurationSeconds`; employees without entries get 0.
   Summing the floored per-entry values keeps the two UI tables consistent.

Doing the aggregation in memory is intentional. It avoids SQL translation problems with
`DateTimeOffset` arithmetic, and the data set is small.

## Registration
Register the three services as scoped in `Program.cs`.

## Tests
All tests use `ApiFactory` and resolve services from a scope. Move time with `factory.Clock.Advance(...)`.
Each test class gets its own database, and each test creates employees with unique names.

**EmployeeServiceTests**
- Create trims the name; list returns it with `ActiveEntry == null`.
- Empty or whitespace name → `ValidationException`; 101 characters → `ValidationException`.
- Duplicate → `ConflictException`.

**TimerServiceTests**
- Start → advance 30 min → stop: one entry, `StoppedAt - StartedAt == 30 min`.
- **Restart:** start, advance 10 min, stop, advance 5 min, start, advance 20 min, stop → two
  entries. The first is unchanged after the second start/stop. The report for the day gives 1800 s total.
- Double start → `ConflictException`; exactly one running entry in the DB.
- **Race:** 10 parallel `StartAsync` calls, each in its own scope → exactly one succeeds, 9
  `ConflictException`, one running entry.
- Stop with nothing running → `ConflictException`.
- Start or stop for an unknown employee → `NotFoundException`.
- Blank description → `ValidationException`.

**ReportServiceTests**
- **Empty:** no entries in the period → `Entries` empty; `Totals` has every employee with 0.
- **Empty with filter:** employee without entries → one total of 0.
- Clipping at the start: entry 08:00–10:00, `from` 09:00 → 3600 s.
- Clipping at the end: entry 23:00–01:00 next day, period = first day → 3600 s.
- Entry fully outside (ends exactly at `from`, or starts exactly at `to`) → excluded.
- Running entry counts up to `now`, and is clipped to `to` if `now > to`.
- Totals equal the sum of entry durations per employee.
- `from == to` and `from > to` → `ValidationException`.
- Unknown `employeeId` → `NotFoundException`.
- Non-UTC input offsets (e.g. +02:00) give the same result as the equivalent UTC values.

## Acceptance criteria
- [x] No `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.UtcNow` in `TimeTrack.Api` (`git grep`).
- [x] `dotnet test backend` is green; the race test passes 5 runs in a row.
- [x] Services contain no `Microsoft.AspNetCore.Http` references.

## Commit
`Step 3: Employee, timer and report services`
