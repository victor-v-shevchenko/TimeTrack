# Step 1: Dev container, compose skeleton and secrets

## Goal
A clean clone, plus a copied `.env`, opens in the Dev Container. The workspace reaches
Postgres through the connection string mounted as a Compose secret. No credentials are tracked by git.

## Depends on
Nothing.

## Files

    .gitignore
    .gitattributes
    .env.example
    docker-compose.yml                       # db service + secrets only (api/web come in step 6)
    .devcontainer/devcontainer.json
    .devcontainer/docker-compose.devcontainer.yml
    .devcontainer/.env.example
    docs/ai-log.md                           # template
    docs/specs/                              # these specs (already present)

## Details

### `.gitignore`

    .env
    .devcontainer/.env
    bin/
    obj/
    TestResults/
    *.user
    .vs/
    node_modules/
    dist/
    .angular/
    coverage/

### `.gitattributes`

    * text=auto eol=lf
    *.png binary
    *.ico binary

Without this, Windows checkouts produce CRLF shell snippets and Dockerfiles that break inside Linux containers.

### `.env.example` (root and `.devcontainer/`, same content)

    POSTGRES_DB=timetrack
    POSTGRES_USER=timetrack
    POSTGRES_PASSWORD=change-me
    DB_CONNECTION_STRING="Host=db;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

The dev copy may keep `change-me`. The README tells users to change the root one.

### `docker-compose.yml` (skeleton)
Copy the `db` service, `secrets` and `volumes` from PLAN.md §7. Leave out `api` and `web` for now.
Every variable is referenced as `${VAR:?copy .env.example to .env}`.

### `.devcontainer/docker-compose.devcontainer.yml` and `devcontainer.json`
Exactly as in PLAN.md §8.

### `docs/ai-log.md`

    # AI log

    Bugs and wrong output produced by AI tools, how they were found, and how they were fixed.

    ## Template
    ### <short title>
    - **Step / commit:** <step N, hash>
    - **Tool / model:** Claude Code, Opus 5.5 | /code-review, Sonnet 5.5
    - **What the AI produced:** <code or behavior>
    - **How it was found:** failing test | /code-review | manual check
    - **Fix:** <what changed, commit link>

## Open question to settle in this step
Does `secrets.<name>.environment: VAR` pick up a variable defined only in `.env`?
Check it as follows: inside the container, `cat /run/secrets/ConnectionStrings__Default` prints the expanded string.

- **Works:** keep the plan.
- **Doesn't work:** switch to file secrets. Generate the gitignored `secrets/db_connection.txt`
  and `secrets/db_password.txt` from `.env` using an `initializeCommand` script, and use
  `secrets.<name>.file`. Update PLAN.md §6–§8 and this spec.

Also check that the file can be read by a non-root user (`stat -c '%a %U' /run/secrets/*`).
Step 6 runs the API as the non-root `app` user.

## Acceptance criteria
- [ ] `git status` on a clean clone shows no `.env`; `git check-ignore .env .devcontainer/.env` matches both.
- [ ] If `.devcontainer/.env` is missing, "Reopen in Container" fails with the `copy .env.example to .env` message.
- [ ] Inside the container: `dotnet --version` reports 10.x; `node --version` reports LTS; `docker ps` works.
- [ ] `cat /run/secrets/ConnectionStrings__Default` shows `Host=db;Database=timetrack;...` with the real password.
- [ ] `env | grep -i password` inside the workspace prints nothing.
- [ ] `(echo > /dev/tcp/db/5432) && echo ok` prints `ok`.
- [ ] From the host, `docker compose config` resolves (with root `.env`) and fails clearly without it.
- [ ] `git grep -nIi 'password=' -- ':!*.example' ':!docs/**' ':!PLAN.md'` returns nothing.

## Commit
`Step 1: Dev container, compose skeleton and secrets`
