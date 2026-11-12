# Deployment

The app ships as a single container image: ASP.NET Core serves Blazor, the auth pages, and `/api/v1`. A second image carries a self-contained EF Core migration bundle so schema changes stay a separate deploy step.

## Images

```bash
docker build -t todolist:local .
docker build -f Dockerfile.migrations -t todolist-migrations:local .
```

The `Dockerfile` is multi-stage:

1. Restore and publish `TodoList.Web` in Release with the pinned SDK.
2. Copy the published output into the ASP.NET Core runtime image.
3. Create `/app/keys`, owned by `$APP_UID`, and point `DataProtection__KeysPath` at it.
4. Run as the non-root `$APP_UID` user and listen on port **8080**.

`Dockerfile.migrations` restores the `dotnet-ef` tool from `.config/dotnet-tools.json`, builds a `linux-x64` self-contained bundle, and copies it into a `runtime-deps` image. The bundle is the deploy artifact; the app image never migrates on startup outside Development.

`.dockerignore` keeps `bin`, `obj`, tests, docs, and local artifacts out of the build context.

## Configuration

| Setting | Required | Notes |
|---|---|---|
| `ConnectionStrings__Default` | yes | Npgsql connection string |
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` enables HSTS, the exception handler, secure cookies, and JSON console logs |
| `ASPNETCORE_URLS` | no | Defaults to `http://+:8080` in the image |
| `DataProtection__KeysPath` | yes in production | Directory for the Data Protection key ring; `/app/keys` in the image |
| `DataProtection__ApplicationName` | no | Defaults to `TodoList`; keep it stable so keys keep matching |
| `ForwardedHeaders__Enabled` | no | `false` by default; set `true` only behind a trusted proxy |
| `ForwardedHeaders__ForwardLimit` | no | Number of proxies to unwrap; defaults to `1` |
| `ForwardedHeaders__KnownProxies` | no | IP list; replaces the loopback default when set |
| `ForwardedHeaders__KnownNetworks` | no | CIDR list (for example `172.16.0.0/12`); replaces the loopback default when set |
| `Auth__RateLimit__PermitLimit` | no | Login/registration requests per client IP per window; defaults to `10` |
| `Auth__RateLimit__WindowSeconds` | no | Fixed window length in seconds; defaults to `60` |
| `Email__Host` | no | Empty falls back to the logging sender and logs a startup warning |
| `Email__Port` | no | Defaults to `25` |
| `Email__UseSsl` | no | `true` enables STARTTLS (typical for port 587) |
| `Email__UserName` / `Email__Password` | no | SMTP credentials |
| `Email__FromAddress` / `Email__FromName` | no | Sender identity; set both to a verified domain in production |
| `Reminders__Enabled` | no | Defaults to `true`; set `false` to stop the dispatcher |
| `Logging__LogLevel__Default` | no | Defaults to `Information` |

The demo seeder only runs in Development, so production never creates the demo account. Provide secrets through the platform secret store; do not bake them into the image.

```bash
docker run --rm -p 8080:8080 \
  -e 'ASPNETCORE_ENVIRONMENT=Production' \
  -e 'ConnectionStrings__Default=Host=db;Port=5432;Database=todolist;Username=todolist;Password=***' \
  -e 'DataProtection__KeysPath=/app/keys' \
  -v todolist-keys:/app/keys \
  todolist:local
```

## Compose stack

`docker-compose.production.yml` runs PostgreSQL, the migration bundle, and the app. `.env.production.example` documents every variable.

```bash
cp .env.production.example .env.production   # fill in POSTGRES_PASSWORD and SMTP
docker compose --env-file .env.production -f docker-compose.production.yml up -d --build
```

- `postgres` has no published port; only the compose network reaches it.
- `migrate` waits for the database health check and runs once. The app starts only after it exits successfully.
- `app` publishes `${APP_PORT:-8080}` and keeps its key ring in the `data-protection-keys` volume.
- Run exactly **one** app replica. Blazor Server keeps circuits in memory, and the reminder dispatcher has no cross-replica claim, so two replicas would process the same due rows twice.

## Migrations

Production applies migrations as a separate deploy step; the app does not migrate on startup outside Development.

With compose, the `migrate` service already does this. Otherwise build the bundle image once and run it against the database:

```bash
docker build -f Dockerfile.migrations -t todolist-migrations .
docker run --rm todolist-migrations \
  --connection "Host=db;Port=5432;Database=todolist;Username=todolist;Password=***"
```

Run it before shifting traffic to the new image, and keep the rollback plan in mind: a migration that drops or renames a column is not automatically reversible. Take a backup first.

## Health probes

| Endpoint | Purpose | Expected |
|---|---|---|
| `/health/live` | liveness: the process answers | `200` |
| `/health/ready` | readiness: PostgreSQL is reachable | `200`, `503` otherwise |

Point the platform liveness probe at `/health/live` and the readiness probe at `/health/ready`. Liveness deliberately ignores the database so a slow database does not restart a healthy process. The compose health check only probes the listener because the runtime image ships no HTTP client.

## Data Protection

Blazor circuits, auth cookies, antiforgery tokens, and bearer/refresh tokens are all protected by the ASP.NET Core Data Protection key ring.

- Set `DataProtection__KeysPath` to a persistent directory and mount a volume there (the image defaults to `/app/keys`).
- Recreating a container without persistent keys invalidates every cookie and token, logging all users out.
- The startup log warns when production runs without `DataProtection__KeysPath`.
- Back up the volume or the key ring together with the database: restoring a database without its keys still forces every user to sign in again.
- The key files are not encrypted at rest by default; protect the volume with host permissions and disk encryption.
- When running more than one replica, all replicas must share the same key ring and `DataProtection__ApplicationName`.

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

Terminate TLS at the platform proxy or ingress and forward plain HTTP to port 8080. Then:

1. Set `ForwardedHeaders__Enabled=true`.
2. Trust only the proxy: `ForwardedHeaders__KnownProxies` (an IP) or
   `ForwardedHeaders__KnownNetworks` (a CIDR range). Setting either list replaces
   the loopback-only defaults.
3. Keep `ForwardedHeaders__ForwardLimit=1` unless several proxies chain.

For compose, add a small override instead of editing the tracked file:

```yaml
services:
  app:
    environment:
      ForwardedHeaders__Enabled: "true"
      ForwardedHeaders__KnownNetworks__0: 172.16.0.0/12
```

With trusted headers the app sees `https` and the real client IP. Without them it sees `http` and the proxy IP: auth cookies lose `Secure`, HSTS does not apply, and the rate limiter lumps every user into one partition. Leaving the feature off when the app is directly exposed also prevents callers from spoofing their IP through headers. While TLS is terminated upstream, `UseHttpsRedirection` cannot infer the HTTPS port and logs a benign warning.

## Reminders and email

Reminder jobs are written to the database and delivered by a hosted dispatcher.

- Configure `Email__Host`/`Port`/`UseSsl`/`UserName`/`Password` and a sender on a verified domain.
- With an empty `Email__Host`, DI registers `LoggingEmailSender`; reminders are still processed but only written to the log. Production logs a startup warning in that case.
- Set `Reminders__Enabled=false` to stop the dispatcher entirely (for example before an SMTP provider is chosen).
- Delivery uses `System.Net.Mail.SmtpClient`; `Email__UseSsl=true` performs STARTTLS, which is what port 587 providers expect.
- The dispatcher selects due rows without row-level locking. Keep a single app replica; scaling out requires implementing a claim (`FOR UPDATE SKIP LOCKED`) first.

## Logging

Outside Development the app writes single-line JSON to stdout, including scopes and UTC timestamps. Let the platform collect stdout; no log files are written.

## Checklist before going live

- [ ] Database reachable from the runtime network and migrations applied.
- [ ] `ConnectionStrings__Default` supplied by the secret store.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `DataProtection__KeysPath` points at a mounted volume, and the volume is included in backups.
- [ ] Liveness and readiness probes configured.
- [ ] TLS terminated and the public host name configured.
- [ ] `ForwardedHeaders__Enabled=true` with the proxy in the trust list, or a documented reason to keep the app directly exposed.
- [ ] `Auth__RateLimit__*` reviewed for the expected traffic (a shared NAT or proxy counts as one client).
- [ ] SMTP provider configured, or `Reminders__Enabled=false` recorded as a deliberate decision.
- [ ] `Email__FromAddress`/`FromName` use a real verified domain; SPF, DKIM, and DMARC are set up.
- [ ] A single app replica is enforced; a second replica is blocked until reminder claiming exists.
- [ ] Backups and a restore drill scheduled for PostgreSQL **and** the Data Protection volume.
- [ ] Smoke test after deploy: register, sign in, create a task, load the board and calendar, export data, check `/health/ready`.
