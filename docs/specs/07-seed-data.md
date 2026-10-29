# Step 7: Seed data

## Goal
A single idempotent script fills an empty database with fictional employees and two weeks of
entries, so the UI and report have something to show.

## Depends on
Step 6 (run against the compose stack) or step 2 (dev DB).

## Files

    seed/seed.sql
    seed/README.md                          # how to run it (short)

## Why SQL and not the API
The API can only start entries "now". Historical entries need explicit past timestamps.
The script runs after migrations, so the schema already exists. Don't put it in
`/docker-entrypoint-initdb.d`: that runs before the API creates the tables.

## seed/seed.sql
- Wrap everything in `BEGIN; … COMMIT;`.
- Employees: 6 clearly fictional names (e.g. "Alice Example", "Bob Sample", "Carol Testova",
  "Dmytro Demo", "Erin Placeholder", "Frank Fictional"):

      INSERT INTO employees (name, created_at)
      VALUES ('Alice Example', now() - interval '30 days'), …
      ON CONFLICT (name) DO NOTHING;

- Entries are inserted only for seeded employees that have no entries yet (so a re-run adds nothing):
  - For each of the last 10 weekdays, 2–3 entries per employee with realistic task descriptions
    ("Code review: report endpoint", "Sprint planning", "Fix timezone bug in report", …).
  - Times are relative to `date_trunc('day', now())` so the current week always has data. Use
    `generate_series` for the days and a small `VALUES` list of (offset, duration, description) templates.
  - Make one entry cross midnight (22:30–00:45) to show clipping in the report.
  - Leave one running entry (`stopped_at NULL`) for "Alice Example", started 25 minutes ago.
  - Leave one employee with no entries (shows a 0 total).
- No `id` values are hard-coded; identity columns generate them.
- All `timestamptz` values come from `now()` arithmetic, never from string literals without an offset.

## Running it (document in `seed/README.md`)
Use the db container's own variables, so no credentials are typed or committed.

bash (host or Dev Container with docker-outside-of-docker):

    docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < seed/seed.sql

PowerShell (Windows host):

    Get-Content seed/seed.sql -Raw | docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'

For the dev database, use the same command with `-f .devcontainer/docker-compose.devcontainer.yml`.
The compose project name may differ; use `docker ps` to find the container if needed.

## Acceptance criteria
- [ ] On an empty stack: run the script, then the Employees page shows 6 people and Alice's timer is running.
- [ ] The report for the current week shows entries and non-zero totals, and one employee with 0.
- [ ] Running the script a second time changes nothing (row counts identical).
- [ ] Unit/API tests are unaffected (they use their own databases).

## Commit
`Step 7: Seed data`
