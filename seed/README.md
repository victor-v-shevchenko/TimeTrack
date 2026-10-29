# Seed data

`seed.sql` fills the database with 6 fictional employees and about two weeks of time entries.
It is idempotent: employees are inserted with `ON CONFLICT DO NOTHING`, and entries only for
seeded employees that have none yet.

- Alice Example has a running timer (started 25 minutes ago).
- Bob Sample has one entry that crosses midnight (22:30–00:45).
- Frank Fictional has no entries.

Run it after the API has applied migrations (the API does this on startup). It uses the
db container's own variables, so no credentials are typed or committed.

bash:

    docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < seed/seed.sql

PowerShell:

    Get-Content seed/seed.sql -Raw | docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'

For the dev database add `-f .devcontainer/docker-compose.devcontainer.yml` after `docker compose`.
If the compose project name differs, find the container with `docker ps`.

Times are relative to the database's `now()`, so the current week always has data.
