# Step 6: Dockerfiles, nginx, full compose stack

## Goal
`docker compose up --build` on the host starts db, api and web. The app works at
http://localhost:8080, and data (including a running timer) survives `down`/`up`.

## Depends on
Steps 1–5.

## Files

    backend/Dockerfile
    backend/.dockerignore
    frontend/Dockerfile
    frontend/.dockerignore
    frontend/nginx.conf
    docker-compose.yml                       # add api and web (PLAN.md §7)

## backend/Dockerfile (build context `./backend`)

    FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
    WORKDIR /src
    COPY TimeTrack.Api/TimeTrack.Api.csproj TimeTrack.Api/
    RUN dotnet restore TimeTrack.Api/TimeTrack.Api.csproj
    COPY TimeTrack.Api/ TimeTrack.Api/
    RUN dotnet publish TimeTrack.Api/TimeTrack.Api.csproj -c Release -o /app --no-restore

    FROM mcr.microsoft.com/dotnet/aspnet:10.0
    WORKDIR /app
    COPY --from=build /app .
    USER $APP_UID
    EXPOSE 8080
    ENTRYPOINT ["dotnet", "TimeTrack.Api.dll"]

- The test project is not copied, so the image doesn't contain tests.
- The ASP.NET image listens on 8080 by default (`ASPNETCORE_HTTP_PORTS`). The container
  runs in Production, so Swagger is off.

`backend/.dockerignore`: `**/bin`, `**/obj`, `**/TestResults`, `TimeTrack.Tests`, `**/.env`, `**/*.user`.

## frontend/Dockerfile (build context `./frontend`)

    FROM node:24-alpine AS build
    WORKDIR /src
    COPY package.json package-lock.json ./
    RUN npm ci
    COPY . .
    RUN npm run build

    FROM nginx:alpine
    COPY nginx.conf /etc/nginx/conf.d/default.conf
    COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html

Check the actual output folder in `angular.json` (`outputPath`) and adjust the path.

`frontend/.dockerignore`: `node_modules`, `dist`, `.angular`, `coverage`, `**/.env`.

## frontend/nginx.conf

    server {
      listen 80;
      root /usr/share/nginx/html;
      index index.html;

      location /api/ {
        proxy_pass http://api:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
      }

      location / {
        try_files $uri $uri/ /index.html;
      }

      location ~* \.(js|css|woff2?|svg|png|ico)$ {
        expires 7d;
        add_header Cache-Control "public, immutable";
      }
    }

`proxy_pass` has no trailing slash, so `/api/...` is forwarded unchanged.

## docker-compose.yml
Add `api` and `web` as in PLAN.md §7. `api` gets only the `db_connection` secret (target
`ConnectionStrings__Default`) and no environment variables containing credentials. Only `web` publishes a port.

## Checks
1. `cp .env.example .env`, set a password, then `docker compose up --build`.
2. Open http://localhost:8080: add two employees; start a timer for one; stop and start again for the other.
3. Deep link: reloading http://localhost:8080/report works (SPA fallback).
4. `docker compose down && docker compose up`: the employees, the closed entries and the
   **still-running** timer are all there, and the elapsed time continues.
5. `docker compose down -v && docker compose up`: data is gone. This is expected; mention it in the README.
6. `docker compose exec api sh -c 'env | grep -i -E "password|connection"'` prints nothing.
   `docker inspect` of `api` shows no password.
7. `docker image history` of the api image contains no `.env` and no connection string.
8. `docker compose exec api id` → non-root, and the API still reads the secret (it started fine).

## Pitfalls
- `localhost` in the API connection string or nginx config → it must be the service names `db` and `api`.
- nginx fails to start with "host not found in upstream" if `api` isn't on the network.
  `depends_on: [api]` covers this.
- If the secret file isn't readable by `$APP_UID`, startup fails with the "not configured"
  message. See the step 1 note about permissions.

## Acceptance criteria
- [ ] All the checks above pass on the Windows host with Docker Desktop.
- [ ] Image sizes are reasonable (api is roughly aspnet base + a few MB; web is nginx + bundle).

## Commit
`Step 6: Dockerfiles and full compose stack`
