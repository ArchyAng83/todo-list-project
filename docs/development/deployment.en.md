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
4. Run as the non-root `$APP_UID` user and listen on port **8080**. The compose service additionally runs with a read-only root filesystem, `no-new-privileges`, `cap_drop: ALL`, a `/tmp` tmpfs, and CPU/memory limits.

`Dockerfile.migrations` restores the `dotnet-ef` tool from `.config/dotnet-tools.json`, builds a `linux-x64` self-contained bundle, and copies it into a `runtime-deps` image. The bundle is the deploy artifact; the app image never migrates on startup outside Development.

`.dockerignore` keeps `bin`, `obj`, tests, docs, and local artifacts out of the build context.

## Configuration

| Setting | Required | Notes |
|---|---|---|
| `ConnectionStrings__Default` | yes | Npgsql connection string |
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` enables HSTS, the exception handler, secure cookies, and JSON console logs |
| `AllowedHosts` | yes in production | Public host names separated by `;`; the image default is `*`, which logs a startup warning |
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
| `Reminders__RetentionDays` | no | Delete terminal reminders older than this; defaults to `30` |
| `Reminders__CleanupIntervalHours` | no | Retention cleanup interval; defaults to `24` |
| `SecurityHeaders__Enabled` | no | Defaults to `true`; emits CSP, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, and `Permissions-Policy` |
| `SecurityHeaders__ContentSecurityPolicy` | no | Override the default Blazor-compatible policy |
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
- `app` runs read-only with a `/tmp` tmpfs, drops all Linux capabilities, and has CPU/memory limits. The `data-protection-keys` volume and the database volume are the only writable state.
- Run one app replica for the UI unless the proxy pins each Blazor circuit with sticky sessions. Reminder dispatch is serialized by a PostgreSQL advisory lock (ADR-0007), so extra replicas cannot send duplicates.

## Security headers and host filtering

Every response carries browser hardening headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, and a Content Security Policy. The default CSP allows the inline styles and WebSocket circuit that Blazor Server needs; override `SecurityHeaders__ContentSecurityPolicy` if a reverse proxy or a new UI dependency requires a different policy.

`AllowedHosts` is the Host-header filter. In production set it to the public names, for example `ALLOWED_HOSTS=example.com;www.example.com`. Leaving it at `*` logs a startup warning: a spoofed Host header can then reach the app (for example through password-reset links, when that feature exists).

`Email` and `Reminders` options are validated at startup (`ValidateOnStart`). An out-of-range SMTP port, an invalid sender address, or a nonsense retention value fails the host immediately instead of surfacing on the first reminder.

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
- Back up the volume or the key ring together with the database: restoring a database without its keys still forces every user to sign in again. `scripts/backup-postgres.ps1` archives both; see `docs/operations/backup-restore.en.md`.
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
- `ReminderDispatcher` takes a PostgreSQL session advisory lock before selecting due rows. Only one replica dispatches per poll; a second one skips the poll instead of sending duplicates. The lock is released when the connection closes or the process exits (ADR-0007).
- `ReminderRetentionWorker` deletes `Sent`, `Failed`, and `Cancelled` rows older than `Reminders__RetentionDays`; `Pending` rows are never removed. Cleanup is idempotent and runs on every replica.

Backups, restores, and the post-deploy smoke test are runbooks:

- `docs/operations/backup-restore.en.md` — `scripts/backup-postgres.ps1` and `scripts/restore-postgres.ps1`.
- `docs/operations/smoke-test.en.md` — `scripts/smoke-test.ps1`, also wired into the CI container job.

## Logging

Outside Development the app writes single-line JSON to stdout, including scopes and UTC timestamps. Let the platform collect stdout; no log files are written.

## Checklist before going live

- [ ] Database reachable from the runtime network and migrations applied.
- [ ] `ConnectionStrings__Default` supplied by the secret store.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `AllowedHosts` contains the public host names, not `*`.
- [ ] `DataProtection__KeysPath` points at a mounted volume, and the volume is included in backups.
- [ ] Liveness and readiness probes configured.
- [ ] TLS terminated and the public host name configured.
- [ ] `ForwardedHeaders__Enabled=true` with the proxy in the trust list, or a documented reason to keep the app directly exposed.
- [ ] `Auth__RateLimit__*` reviewed for the expected traffic (a shared NAT or proxy counts as one client).
- [ ] SMTP provider configured, or `Reminders__Enabled=false` recorded as a deliberate decision.
- [ ] `Email__FromAddress`/`FromName` use a real verified domain; SPF, DKIM, and DMARC are set up.
- [ ] Reminder retention matches the desired audit window (`Reminders__RetentionDays`).
- [ ] One app replica is enforced unless Blazor circuits are pinned with sticky sessions; reminder duplicate protection is already in place.
- [ ] `scripts/backup-postgres.ps1` runs on a schedule, and `scripts/restore-postgres.ps1` has been rehearsed against a scratch database.
- [ ] `scripts/smoke-test.ps1` passes against the public URL after deploy.
