# ADR-0006: Production deployment shape

- **Status:** Accepted
- **Date:** 2026-11-12

## Context

The app is a single ASP.NET Core process serving Blazor Server, Identity pages, and `/api/v1`. Production needs a repeatable deploy without choosing a hosting vendor or an SMTP provider yet. Several runtime details have security consequences: encryption keys, cookie flags, client IP resolution behind TLS proxies, and email delivery.

## Decision

Ship and operate the app with these defaults:

- **One app container, one replica.** Blazor Server keeps circuits in memory, and the reminder dispatcher has no cross-replica row claim. The API could scale independently later; the UI stays single-replica for the MVP.
- **Two images.** `Dockerfile` produces the runtime image. `Dockerfile.migrations` produces a self-contained EF Core migration bundle, applied as a separate deploy step before traffic shifts.
- **Data Protection keys on a volume.** `DataProtection:KeysPath` persists the key ring; the image defaults to `/app/keys`. Cookies, antiforgery tokens, and bearer/refresh tokens survive container recreation only when the volume is backed up and kept stable.
- **Secure cookies outside Development.** Auth and antiforgery cookies use `CookieSecurePolicy.Always` in non-Development environments. Development keeps `SameAsRequest` so plain-HTTP local runs still work.
- **Forwarded headers are opt-in.** `ForwardedHeaders:Enabled` is off by default. When enabled, only `X-Forwarded-For` and `X-Forwarded-Proto` are honoured, with `ForwardLimit` and an explicit `KnownProxies`/`KnownNetworks` trust list. This keeps the auth rate limiter keyed on the real client IP and lets HSTS and secure cookies work behind TLS termination.
- **SMTP is optional at startup.** An empty `Email:Host` selects the logging sender and logs a startup warning; operators either configure SMTP or set `Reminders:Enabled=false` deliberately.
- **Compose is the reference topology.** `docker-compose.production.yml` runs PostgreSQL, the migration bundle, and the app, with `.env.production.example` documenting every variable. Hosting providers can translate the same container contract.

## Consequences

- Deploys are mechanical once a host and SMTP account exist; neither is required to build or test the release.
- Losing the Data Protection volume logs every user out and invalidates tokens, so it belongs in backups.
- Without forwarded headers behind a proxy, the app sees HTTP and the proxy IP; the deployment guide documents both the symptom and the override.
- Scaling past one replica requires implementing a reminder claim and sticky Blazor sessions first.
- The migration bundle must run before the app starts; compose enforces this with `service_completed_successfully`.
