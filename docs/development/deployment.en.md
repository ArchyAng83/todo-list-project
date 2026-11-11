# Deployment

The app ships as a single container image: ASP.NET Core serves Blazor, the auth pages, and `/api/v1`.

## Image

```bash
docker build -t todolist:local .
```

The `Dockerfile` is multi-stage:

1. Restore and publish `TodoList.Web` in Release with the pinned SDK.
2. Copy the published output into the ASP.NET Core runtime image.
3. Run as the non-root `$APP_UID` user and listen on port **8080**.

`.dockerignore` keeps `bin`, `obj`, tests, docs, and local artifacts out of the build context.

## Configuration

| Setting | Required | Notes |
|---|---|---|
| `ConnectionStrings__Default` | yes | Npgsql connection string |
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` enables HSTS, the exception handler, and JSON console logs |
| `ASPNETCORE_URLS` | no | Defaults to `http://+:8080` in the image |
| `Auth__RateLimit__PermitLimit` | no | Login/registration requests per client IP per window; defaults to `10` |
| `Auth__RateLimit__WindowSeconds` | no | Fixed window length in seconds; defaults to `60` |
| `Logging__LogLevel__Default` | no | Defaults to `Information` |

The demo seeder only runs in Development, so production never creates the demo account. Provide secrets through the platform secret store; do not bake them into the image.

```bash
docker run --rm -p 8080:8080 \
  -e 'ASPNETCORE_ENVIRONMENT=Production' \
  -e 'ConnectionStrings__Default=Host=db;Port=5432;Database=todolist;Username=todolist;Password=***' \
  todolist:local
```

## Migrations

Production applies migrations as a separate deploy step; the app does not migrate on startup outside Development.

```bash
# Build a self-contained migration bundle once, then run it from the deploy pipeline.
dotnet ef migrations bundle \
  --project src/TodoList.Infrastructure \
  --startup-project src/TodoList.Web \
  --output efbundle

./efbundle --connection "Host=db;Port=5432;Database=todolist;Username=todolist;Password=***"
```

Run the bundle before shifting traffic to the new image, and keep the rollback plan in mind: a migration that drops or renames a column is not automatically reversible.

## Health probes

| Endpoint | Purpose | Expected |
|---|---|---|
| `/health/live` | liveness: the process answers | `200` |
| `/health/ready` | readiness: PostgreSQL is reachable | `200`, `503` otherwise |

Point the platform liveness probe at `/health/live` and the readiness probe at `/health/ready`. Liveness deliberately ignores the database so a slow database does not restart a healthy process.

## Authentication rate limiting

`POST /api/v1/auth/login`, `POST /account/login`, and `POST /account/register` are
throttled per client IP with a fixed window: 10 requests per 60 seconds by
default, configured through `Auth:RateLimit`. A rejected request gets
`429 Too Many Requests` with `Retry-After`; Identity lockout (5 attempts, 15
minutes) stays the per-account protection. Page loads, static assets, and the rest
of `/api/v1` are never throttled.

The partition key is `HttpContext.Connection.RemoteIpAddress`. Directly exposed,
that is the caller; behind a proxy every request carries the proxy address and the
whole deployment would share one partition. Enable forwarded headers — with the
proxy in `KnownProxies`/`KnownNetworks` — before relying on the limit, or raise it
for the aggregate traffic of the proxy.

## Reverse proxy and TLS

Terminate TLS at the platform proxy or ingress and forward plain HTTP to port 8080. `UseHttpsRedirection` cannot infer an HTTPS port behind such a proxy and logs a benign warning; configure forwarded headers before exposing the app directly to the internet.

## Blazor Server constraints

Blazor Server keeps circuit state in memory and uses Data Protection keys.

- Run a single replica for the MVP.
- To scale out, enable sticky sessions **and** share the Data Protection key ring between replicas.
- The API (`/api/v1`) is stateless and can scale independently.

## Logging

Outside Development the app writes single-line JSON to stdout, including scopes and UTC timestamps. Let the platform collect stdout; no log files are written.

## Checklist before going live

- [ ] Database reachable from the runtime network and migrations applied.
- [ ] `ConnectionStrings__Default` supplied by the secret store.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] Liveness and readiness probes configured.
- [ ] TLS terminated and the public host name configured.
- [ ] `Auth__RateLimit__*` reviewed for the expected traffic (a shared NAT or proxy counts as one client).
- [ ] A production SMTP provider selected before email reminders ship.
- [ ] Backups and a restore drill scheduled for PostgreSQL.
