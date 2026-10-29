# Step 4: HTTP endpoints, ProblemDetails, API tests

## Goal
The API from PLAN.md §4 is exposed over HTTP, returns RFC 7807 errors and is covered by
HTTP-level tests for every scenario in PLAN.md §9.

## Depends on
Step 3.

## Files

    backend/TimeTrack.Api/Endpoints/EmployeeEndpoints.cs
    backend/TimeTrack.Api/Endpoints/TimerEndpoints.cs
    backend/TimeTrack.Api/Endpoints/ReportEndpoints.cs
    backend/TimeTrack.Api/Endpoints/AppExceptionHandler.cs
    backend/TimeTrack.Api/Contracts/Requests.cs
    backend/TimeTrack.Api/Program.cs                       # wiring
    backend/TimeTrack.Tests/EmployeeTests.cs
    backend/TimeTrack.Tests/TimerTests.cs
    backend/TimeTrack.Tests/ReportTests.cs
    backend/TimeTrack.Tests/HttpAssert.cs                  # helpers for ProblemDetails

## Endpoints
Each file has `public static RouteGroupBuilder MapXxxEndpoints(this RouteGroupBuilder api)`.
`Program` does `var api = app.MapGroup("/api");`.

| Method | Route | Request | Success |
|--------|-------|---------|---------|
| POST | `/employees` | `CreateEmployeeRequest(string? Name)` | 201 `EmployeeDto`, `Location: /api/employees/{id}` |
| GET  | `/employees` | — | 200 `EmployeeDto[]` |
| POST | `/employees/{id:int}/timer/start` | `StartTimerRequest(string? Description)` | 201 `TimeEntryDto` |
| POST | `/employees/{id:int}/timer/stop` | — | 200 `TimeEntryDto` |
| GET  | `/reports` | query `from`, `to` (required, ISO 8601 with offset), `employeeId?` | 200 `ReportDto` |

Use `TypedResults` and `Results<...>` return types so that OpenAPI shows the responses.
Endpoints contain no logic beyond calling the service.

## Error handling
- `builder.Services.AddProblemDetails()`; `builder.Services.AddExceptionHandler<AppExceptionHandler>()`;
  `app.UseExceptionHandler()`; `app.UseStatusCodePages()`.
- `AppExceptionHandler : IExceptionHandler` handles only `AppException`:
  - `ValidationException` → `HttpValidationProblemDetails` with status 422 and
    `errors = { [field]: [message] }`;
  - others → `ProblemDetails { Status, Title, Detail }`;
  - write through `IProblemDetailsService` so `traceId` is added.
- Any other exception → default 500 ProblemDetails, without stack traces outside Development.
- Malformed JSON or unparsable query values → the framework's 400 ProblemDetails. Leave these as they are.

## OpenAPI / Swagger UI (Development only)
`builder.Services.AddOpenApi()`. In Development: `app.MapOpenApi()` and
`app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"))`
(package `Swashbuckle.AspNetCore.SwaggerUI`). The UI is at `/swagger`.

## Tests (HttpClient from `ApiFactory`)
`HttpAssert.ProblemAsync(response, status)` checks the status code, the
`application/problem+json` content type, and returns the parsed body.

**EmployeeTests**
- POST valid → 201, Location header, body has trimmed name; GET contains it with `activeEntry: null`.
- POST `{ "name": "  " }` → 422 problem with `errors.name`.
- POST duplicate → 409 problem.

**TimerTests**
- Start → `Clock.Advance(30 min)` → stop → 200, `stoppedAt - startedAt` = 1800 s; GET shows `activeEntry: null`.
- While running, GET employees shows `activeEntry` with the description.
- **Restart timer:** start/stop twice → report has two entries, the first unchanged, total = sum.
- Start twice → second is 409; DB has exactly one running entry.
- **Parallel race:** 10 concurrent POST start → exactly one 201, nine 409.
- Stop without running → 409.
- Unknown employee → 404 for start and stop.
- Blank description → 422 with `errors.description`.

**ReportTests**
- **Empty report:** fresh DB with two employees, no entries → 200, `entries: []`, both totals 0.
- **Empty report, filtered:** `employeeId` of an employee without entries → one total of 0.
- Boundary clipping and running entry (one test each, at HTTP level, smaller than in step 3).
- `from >= to` → 422.
- Missing `from` → 400.
- Unknown `employeeId` → 404.
- Query uses `+02:00` offsets (URL-encoded `%2B`) → treated correctly.

## Pitfalls
- A `+` in a query string decodes to a space. Tests and the frontend must URL-encode
  offsets, or send `Z` times.
- `DateTimeOffset` binding from the query accepts ISO 8601. A date-only value would mean local
  server midnight, so the frontend always sends a full UTC timestamp.

## Acceptance criteria
- [ ] Every row of the PLAN.md §9 backend table has a passing test.
- [ ] `/swagger` works in Development and returns 404 in Production.
- [ ] Every non-2xx response in the tests is `application/problem+json`.
- [ ] `dotnet test backend` is green.

## Commit
`Step 4: HTTP endpoints and API tests`
