# CLAUDE.md

TimeTrack: a small web app for tracking employees' working time (test task).
ASP.NET Core (.NET 10) Minimal API + EF Core/Npgsql + PostgreSQL 17, Angular frontend,
Docker Compose for running, a single Dev Container for development.

- Overall design: [PLAN.md](PLAN.md)
- Per-step specifications: [docs/specs/](docs/specs/) — implement one step at a time, in order
- AI bug log: [docs/ai-log.md](docs/ai-log.md)

## Workflow

- Implement exactly one step from `docs/specs/` per commit. Read the step's spec first and
  check every item of its "Acceptance criteria" before committing.
- Commit message: `Step N: <spec title>`, then a short body.
- If the plan or a spec turns out wrong, update the spec (and PLAN.md if it's a design
  change) in the same commit and say why in the body.
- When AI-generated code had a bug that was caught (test, review, manual check), add an
  entry to `docs/ai-log.md` in the commit that fixes it.

## Commands

All commands run inside the Dev Container (`/workspace`).

    dotnet build backend
    dotnet test backend                                   # needs the db service
    dotnet watch --project backend/TimeTrack.Api          # API on :5000
    dotnet ef migrations add <Name> --project backend/TimeTrack.Api --output-dir Data/Migrations
    cd frontend && npm start                              # ng serve on :4200, proxies /api
    cd frontend && npm test                               # Vitest

Full stack from the host: `docker compose up --build` → http://localhost:8080.

## Rules

**Secrets**
- Never commit credentials or connection strings: not in `appsettings*.json`, compose files,
  `devcontainer.json`, tests or docs examples. Only `.env.example` files, with placeholders.
- Credentials come from env vars (`.env`, `.devcontainer/.env`). Connection strings come from
  Compose secrets mounted at `/run/secrets/ConnectionStrings__Default`, read through
  `AddKeyPerFile("/run/secrets", optional: true)`.
- Never log the connection string. `EnableSensitiveDataLogging` only in Development.

**Time**
- Never use `DateTime.Now`/`UtcNow`/`DateTimeOffset.Now` in backend code. Inject `TimeProvider`
  and use `GetUtcNow()`. Tests use `FakeTimeProvider`.
- All stored and transported times are `DateTimeOffset` in UTC (`timestamptz`). Only the UI
  converts to local time.
- Report periods are half-open `[from, to)`.

**Backend**
- Minimal APIs grouped under `/api`; endpoint files only map HTTP to services.
- Business rules live in `Services/`; they throw `AppException` subclasses, which one
  `IExceptionHandler` turns into RFC 7807 `ProblemDetails`.
- "One running timer per employee" relies on the partial unique index
  `ux_time_entries_one_running`. Keep both the service check and the 23505 → 409 mapping.
- Snake_case DB names (`UseSnakeCaseNamingConvention`). Schema changes only through
  committed migrations; never `EnsureCreated`.
- Aggregate durations in C# after loading the overlapping rows, not in SQL.
- Tests: xUnit against real Postgres, one database per test class through `ApiFactory`.
  No mocks of `DbContext`. Plain `Assert`; no FluentAssertions.

**Frontend**
- Standalone components, signals, `inject()`, new control flow (`@if`, `@for`).
- All HTTP goes through `core/api.service.ts`. Types live in `core/models.ts`.
- Date ↔ UTC conversion only in `core/time.ts` (unit-tested). `to` is exclusive: the
  selected end date + 1 day at local midnight.
- Intervals and subscriptions are cleaned up with `DestroyRef`/`takeUntilDestroyed`.

**General**
- Fictional data only. No authentication.
- Keep it small: no extra layers, repositories, MediatR, AutoMapper.
- Line endings are LF (`.gitattributes`). The host is Windows; the container is Linux.
