# TimeTrack — Implementation Plan

Small web application for tracking employees' working time.
Test task requirements: add employees; start/stop a timer with a task description;
view records and total time per employee for a selected period; data survives restarts.
No authorization, fictional data only. Tests for main scenarios, including restarting
the timer and an empty report.

## 1. Stack

| Layer          | Choice |
|----------------|--------|
| Backend        | ASP.NET Core (.NET 10 LTS), Minimal APIs, EF Core + Npgsql, `TimeProvider` for an injectable clock |
| Database       | PostgreSQL 17, named Docker volume |
| Frontend       | Angular (current version, standalone components, signals, `HttpClient`) |
| Backend tests  | xUnit + `WebApplicationFactory` against real Postgres, `FakeTimeProvider` |
| Frontend tests | Angular default test runner (Vitest) |
| Configuration  | Credentials in environment variables (`.env`, not committed); connection strings as Docker Compose secrets read via the key-per-file configuration provider |
| Infrastructure | Docker Compose for running the solution; single-workspace Dev Container for development |
| AI             | Claude Code with Claude Opus 5.5 (implementation), Claude Sonnet 5.5 (review via `/code-review`) |

## 2. Repository layout

    TimeTrack/
      .devcontainer/
        devcontainer.json
        docker-compose.devcontainer.yml
        .env.example                  # dev credentials template (copied to .env, gitignored)
      backend/
        TimeTrack.Api/
          Program.cs                  # DI, config sources, endpoints, migrations on startup
          Data/AppDbContext.cs
          Data/Migrations/
          Domain/Employee.cs, TimeEntry.cs
          Services/AppException.cs, EmployeeService.cs, TimerService.cs, ReportService.cs
          Endpoints/EmployeeEndpoints.cs, TimerEndpoints.cs, ReportEndpoints.cs, AppExceptionHandler.cs
          Contracts/                  # request/response DTOs
        Dockerfile
        TimeTrack.Tests/
          ApiFactory.cs               # WebApplicationFactory + per-class test DB
          *ServiceTests.cs            # service-level tests (step 3)
          EmployeeTests.cs, TimerTests.cs, ReportTests.cs, PersistenceTests.cs   # HTTP tests
        TimeTrack.slnx
      frontend/
        src/app/
          employees/                  # list, add form, start/stop
          report/                     # period picker, entries, totals
          core/api.service.ts, models.ts
        proxy.conf.json               # /api -> localhost:5000 (dev)
        nginx.conf                    # /api -> api container (prod)
        Dockerfile
      seed/                           # fictional data
      docs/ai-log.md                  # AI prompts, bugs found, fixes
      docs/specs/01-…09-*.md          # detailed spec per implementation step
      .env.example                    # credentials template (copied to .env, gitignored)
      .gitignore                      # includes .env, .devcontainer/.env
      .gitattributes                  # LF line endings
      CLAUDE.md                       # instructions for Claude Code
      docker-compose.yml
      PLAN.md
      README.md

## 3. Data model

    employees:     id (identity), name (varchar 100, unique, not empty), created_at timestamptz
    time_entries:  id, employee_id FK, description (varchar 500, required),
                   started_at timestamptz, stopped_at timestamptz NULL

- **One running timer per employee**, enforced twice:
  - a service-layer check;
  - a partial unique index `(employee_id) WHERE stopped_at IS NULL`.
  A unique violation (`PostgresException` 23505) maps to 409, so concurrent starts cannot both succeed.
- All times are `DateTimeOffset` in UTC; conversion to local time happens only in the UI.
- Snake_case naming via `EFCore.NamingConventions`.
- Migrations are committed and applied on startup (`Database.Migrate()`).

## 4. API

| Method | Path | Behavior |
|--------|------|----------|
| POST | `/api/employees` | Create; 422 empty name, 409 duplicate |
| GET  | `/api/employees` | List with active entry (or `null`) |
| POST | `/api/employees/{id}/timer/start` `{description}` | 201; 409 already running; 404 unknown employee |
| POST | `/api/employees/{id}/timer/stop` | 200 with closed entry; 409 nothing running |
| GET  | `/api/reports?from=&to=&employeeId=` | Entries + total seconds per employee |

Errors are returned as RFC 7807 `ProblemDetails`. Swagger UI is enabled in Development.

### Report rules
- An entry is included if it overlaps `[from, to)`; its duration is clipped to the period.
- A running entry counts up to `TimeProvider.GetUtcNow()`, also clipped.
- The UI sends `to` as exclusive: the day after the selected end date at 00:00 local time, converted to UTC.
- Empty period → 200 with `entries: []` and `totalSeconds: 0` for every employee (or the filtered one).
- `from >= to` → 422.

## 5. Frontend (Angular)

- **Employees page**: add form with validation; one row per employee with task input,
  Start/Stop button and a live elapsed timer.
- **Report page**: from/to dates (default: current week), employee filter, entries table,
  totals table, "No records for this period" empty state.
- `ApiService` wraps `HttpClient`; API errors (e.g. 409) are shown inline.
- Dev: `ng serve --host 0.0.0.0 --proxy-config proxy.conf.json`.
- Prod: static build served by nginx, which proxies `/api` to the API (no CORS needed).

## 6. Configuration and secrets

No credential or connection string is committed: not in `docker-compose.yml`,
`appsettings*.json`, `devcontainer.json`, or test code.

**Credentials → environment variables.** Compose reads them from a gitignored `.env`
next to the compose file. A committed `.env.example` holds placeholders:

    POSTGRES_DB=timetrack
    POSTGRES_USER=timetrack
    POSTGRES_PASSWORD=change-me
    DB_CONNECTION_STRING="Host=db;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

- The connection string is built in `.env` by interpolation, so the password is defined once.
- Compose files reference variables as `${POSTGRES_PASSWORD:?copy .env.example to .env}`,
  so a missing `.env` fails at once with a clear message, not with a confusing auth error.

**Connection strings and passwords → Compose secrets.** Secrets are created from the
variables above (`secrets.<name>.environment`) and mounted as files under `/run/secrets/`.
They don't appear as container env vars, so `docker inspect` and process listings don't show them.

- Postgres reads its password through `POSTGRES_PASSWORD_FILE=/run/secrets/db_password`.
- The API reads the connection string from the secret file `ConnectionStrings__Default`:

      builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

  (package `Microsoft.Extensions.Configuration.KeyPerFile`; `__` in the file name maps to `:`,
  so the file becomes `ConnectionStrings:Default`). It is added after the default sources,
  so the secret takes precedence. The plain env var `ConnectionStrings__Default` still works
  as a fallback (e.g. CI).
- Startup fails fast if `ConnectionStrings:Default` is missing. The connection string is never
  logged; EF `EnableSensitiveDataLogging` stays off outside Development.
- Without Swarm, Compose secrets are bind-mounted plain files, not encrypted. They keep
  credentials out of the repo, image layers and environment, which is the goal here. A real
  deployment would use a secret store (Vault, Azure Key Vault, etc.) behind the same
  configuration key.

## 7. Docker Compose (running the solution)

    services:
      db:
        image: postgres:17
        environment:
          POSTGRES_DB: ${POSTGRES_DB:?copy .env.example to .env}
          POSTGRES_USER: ${POSTGRES_USER:?copy .env.example to .env}
          POSTGRES_PASSWORD_FILE: /run/secrets/db_password
        secrets: [db_password]
        volumes: [pgdata:/var/lib/postgresql/data]
        healthcheck: { test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER}"], interval: 5s, retries: 10 }
      api:
        build: ./backend
        secrets:
          - source: db_connection
            target: ConnectionStrings__Default
        depends_on: { db: { condition: service_healthy } }
      web:
        build: ./frontend
        ports: ["8080:80"]
        depends_on: [api]
    secrets:
      db_password:   { environment: POSTGRES_PASSWORD }
      db_connection: { environment: DB_CONNECTION_STRING }
    volumes: { pgdata: {} }

- First run: `cp .env.example .env` (edit the password), then `docker compose up --build` → http://localhost:8080
- Persistence: `docker compose down` + `up` keeps data; only `down -v` wipes it.
- Postgres applies the password only when it initializes the volume. Changing it later
  requires `down -v` or `ALTER USER`.
- Multi-stage Dockerfiles: backend `sdk` → `aspnet`; frontend `node` → `nginx:alpine`.
  `.dockerignore` excludes `.env` so it never ends up in the build context.

## 8. Dev Container (single workspace)

One container holds both toolchains (.NET + Node) plus a Postgres service.
Credentials come from `.devcontainer/.env` (gitignored, copied from `.devcontainer/.env.example`),
which Compose picks up because it sits next to the dev container compose file.

`.devcontainer/docker-compose.devcontainer.yml`:

    services:
      workspace:
        image: mcr.microsoft.com/devcontainers/dotnet:10.0
        volumes:
          - ..:/workspace:cached
          - node_modules:/workspace/frontend/node_modules
        command: sleep infinity
        environment:
          ASPNETCORE_URLS: http://0.0.0.0:5000
        secrets:
          - source: db_connection
            target: ConnectionStrings__Default
        depends_on: [db]
      db:
        image: postgres:17
        environment:
          POSTGRES_DB: ${POSTGRES_DB:?copy .env.example to .env}
          POSTGRES_USER: ${POSTGRES_USER:?copy .env.example to .env}
          POSTGRES_PASSWORD_FILE: /run/secrets/db_password
        secrets: [db_password]
        volumes: [pgdata-dev:/var/lib/postgresql/data]
    secrets:
      db_password:   { environment: POSTGRES_PASSWORD }
      db_connection: { environment: DB_CONNECTION_STRING }
    volumes: { pgdata-dev: {}, node_modules: {} }

`.devcontainer/devcontainer.json`:

    {
      "name": "TimeTrack",
      "dockerComposeFile": ["docker-compose.devcontainer.yml"],
      "service": "workspace",
      "workspaceFolder": "/workspace",
      "features": {
        "ghcr.io/devcontainers/features/node:1": { "version": "lts" },
        "ghcr.io/devcontainers/features/docker-outside-of-docker:1": {}
      },
      "forwardPorts": [4200, 5000, 5432],
      "postCreateCommand": "dotnet restore backend && dotnet tool install -g dotnet-ef && cd frontend && npm ci",
      "customizations": { "vscode": { "extensions": [
        "ms-dotnettools.csdevkit", "angular.ng-template",
        "dbaeumer.vscode-eslint", "ckolkman.vscode-postgres"
      ]}}
    }

- `dotnet watch`, `dotnet ef` and `dotnet test` all read the connection string from
  `/run/secrets/ConnectionStrings__Default`, the same path as in production.
- `node_modules` lives in a named volume, which avoids slow Windows bind mounts and
  platform-mismatched native binaries.
- Dev data (`pgdata-dev`) is separate from the compose stack (`pgdata`).

**Dev loop** (after `cp .devcontainer/.env.example .devcontainer/.env` and "Reopen in Container"):
1. `dotnet watch --project backend/TimeTrack.Api`
2. `cd frontend && ng serve --host 0.0.0.0`
3. `dotnet test backend` / `ng test`

## 9. Tests

### Backend (xUnit, real Postgres)
`ApiFactory` reads `ConnectionStrings:Default` from the same sources as the app
(key-per-file `/run/secrets`, then environment variables), replaces the database name
with a unique one per test class (`timetrack_test_<guid>`) via `NpgsqlConnectionStringBuilder`,
creates it, applies migrations, swaps in `FakeTimeProvider`, and drops the database afterwards.
No separate test connection string and no credentials in test code.

| Scenario | File |
|----------|------|
| Add employee; reject empty and duplicate names | EmployeeTests |
| Start → advance 30 min → stop gives one closed entry of 1800 s | TimerTests |
| **Restart timer**: start → stop → start → stop gives two entries, first unchanged, total is the sum | TimerTests |
| Double start → 409, only one running entry (including a parallel race) | TimerTests |
| Stop with nothing running → 409 | TimerTests |
| **Empty report**: no entries / employee without entries → 200, `[]`, total 0 | ReportTests |
| Entry crossing the period boundary is clipped | ReportTests |
| Running entry counted up to now | ReportTests |
| `from >= to` → 422 | ReportTests |
| **Persistence**: write data, dispose factory, new factory on same DB, data (incl. running timer) still there | PersistenceTests |
| Missing connection string → startup fails with a clear error | PersistenceTests |

### Frontend
- `ApiService` with `HttpTestingController`
- Report component renders the empty state
- Start/Stop button toggles state

## 10. AI-generated bugs to watch for

Recorded in `docs/ai-log.md` with: what the AI produced, how it was found
(failing test, `/code-review`, manual check), and the fix (with commit link).

- `DateTime` with `Kind=Unspecified/Local` written to `timestamptz` (Npgsql throws)
- `DateTime.Now` instead of the injected `TimeProvider`
- Report filters by `started_at` instead of overlap; no clipping
- Running entries skipped, or a null `stopped_at` crashes the sum
- Missing unique index, so a concurrent double start succeeds
- Duration aggregation that EF cannot translate to SQL
- Angular: off-by-one day in date → UTC conversion, inclusive `to`, live-timer subscription leak
- Compose: API starts before Postgres is ready, or `localhost` used instead of `db`
- Secrets: hardcoded passwords or connection strings in `appsettings.json`, compose files or tests;
  `.env` committed or copied into an image; connection string written to logs
- Tests sharing one database and leaking state

## 11. README outline

1. Overview and screenshot
2. How to run: `cp .env.example .env`, `docker compose up --build`; Dev Container path
   (`.devcontainer/.env`); running tests
3. Design notes: one active timer, UTC, report clipping, migrations on startup,
   credentials in env vars and connection strings in secrets
4. AI tools and models, and what each was used for
5. Example prompts (3–5 real ones)
6. Errors found and fixed (from `docs/ai-log.md`)
7. Possible improvements: editing entries, CSV export, auth, pagination, timezone
   settings, splitting the dev container per frontend/backend, external secret store

## 12. Implementation steps (one commit each)

1. Dev container (workspace + db) and compose skeleton with `.env.example`, `.gitignore`
   and Compose secrets; Postgres reachable from workspace using the secret connection string
2. Backend: domain, DbContext, key-per-file config, first migration, persistence test
3. Timer and report services with tests using `FakeTimeProvider`
4. Endpoints, `ProblemDetails`, API tests
5. Angular app: employees page, report page, component tests
6. Dockerfiles, `.dockerignore` and nginx; `docker compose up` works end to end and data survives a restart
7. Seed data (fictional names)
8. AI review pass; fix issues; check that no secrets are tracked (`git grep -i password`); update `docs/ai-log.md`
9. README; check from a clean clone
