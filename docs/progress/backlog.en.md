# Follow-up Backlog — Post-Review Items

Branch: `main` | Created: 2026-11-11 | Status: In Progress — 2 of 4 done

## Context

The P1/P2/P3 items from the pre-production code review are closed in
`docs/progress/fix-tracker.en.md`. This backlog holds the remaining review
observations that were deliberately left out of that pass: they are either
hardening work, test-infrastructure work, or an explicit MVP trade-off that
needs to be written down.

English is canonical; `backlog.ru.md` is the paired translation.

| ID | Title | Priority | Area | Status |
|----|-------|----------|------|--------|
| B1 | Rate limiting for authentication endpoints | P2 | Security | ✅ Done |
| B2 | Isolate E2E tests from shared demo state | P2 | Testing | ✅ Done |
| B3 | Deterministic time in reminder tests | P3 | Testing | ⬜ Open |
| B4 | Record the shared integration-database decision | P3 | Testing / docs | ⬜ Open |

## B1 — Rate limiting for authentication endpoints

**Problem.** IIS/ASP.NET Core Identity lockout is configured correctly (5 failed
attempts, 15 minutes), but it only protects a single account. Nothing limits a
distributed password-spray across many accounts, and the login/registration
forms have no request throttling at all. The review flagged
`POST /api/v1/auth/login` and the registration flow.

**Plan.**
- Add the built-in ASP.NET Core rate limiter (`builder.Services.AddRateLimiter`)
  with a fixed-window policy partitioned by client IP.
- Apply it to `POST /api/v1/auth/login`, `POST /account/login`, and
  `POST /account/register` only; page loads, assets, and the rest of the API stay
  unthrottled.
- Return `429 Too Many Requests` with `Retry-After`; keep lockout as the
  per-account protection.
- Read the client IP from `ForwardedHeaders`/`X-Forwarded-For` only when the app
  runs behind a trusted proxy, and document the proxy configuration.

**Acceptance criteria.**
- Repeated login attempts from one IP return `429` after the configured limit.
- A successful login is still possible from another IP.
- Limit values live in configuration, not in code.
- Integration tests cover the `429` path and the header.

**Status:** ✅ Done
**Commit:** `5326569` — `feat: rate limit authentication endpoints`

**Implementation.** `Auth:RateLimit` (`PermitLimit` 10, `WindowSeconds` 60 by
default) drives a fixed window over the three auth POSTs, mounted with
`app.UseWhen(IsAuthenticationRequest, branch => branch.UseRateLimiter())` above
the status-code pages so a rejection keeps its bare `429`. The partition key is
`HttpContext.Connection.RemoteIpAddress`. Tests get a higher limit through
`TodoListWebApplicationFactory` and the E2E environment, and
`AuthRateLimitTests` runs a low-limit host to assert the `429`, the
`Retry-After` header, and that a different client still logs in.

**Notes.** Keep the policy conservative enough that normal users are not
throttled by a shared NAT; verify the limits with the deployment target before
tightening them. Behind a proxy every request carries the proxy address, so
forwarded headers must be enabled (and the proxy trusted) before relying on the
limit — see `docs/development/deployment.en.md`.

## B2 — Isolate E2E tests from shared demo state

**Problem.** `UserJourneyTests` drives one seeded demo user
(`demo@todolist.local`) and shares its tasks between tests. The suite therefore
depends on execution order and cannot be parallelised safely.

**Plan.**
- Give every test its own user: register a unique account through the existing
  registration flow (or a small API/helper) with a `Guid`-based email suffix.
- Seed only the data the test needs; do not depend on demo tasks.
- Keep the demo account for manual evaluation, not for automated assertions.
- If registration through the UI is too slow, add a test-only endpoint guarded
  by `IsEnvironment("Testing")`/`IsDevelopment()`.

**Acceptance criteria.**
- Each E2E test creates and owns its user and data.
- Tests pass in any order and with xUnit parallelisation enabled per collection.
- No test asserts on data created by another test.

**Status:** ✅ Done
**Commit:** `_pending_`

**Implementation.** `E2EFixture.CreateUserAsync()` registers a unique
`e2e-{guid}@example.com` through the real registration form, keeps the browser
context signed in, and returns a `TestUser` with a bearer-authenticated
`HttpClient`. Journeys seed only the tasks they assert on through
`POST /api/v1/tasks` (due dates, recurrence) and share navigation helpers from
`Pages`. The demo account is still seeded at startup so a broken seeder fails the
suite and the container stays usable by hand, but no test asserts on its data.

## B3 — Deterministic time in reminder tests

**Problem.** Reminder integration tests use `TimeProvider.System` and compute
`scheduledAtUtc` with `DateTimeOffset.UtcNow.AddMinutes(...)`. On a slow CI
agent a reminder can become due earlier than the assertion expects, which makes
the tests timing-sensitive and occasionally flaky.

**Plan.**
- Reintroduce `Microsoft.Extensions.TimeProvider.Testing` in
  `Directory.Packages.props` and register a `FakeTimeProvider` in the
  integration host for reminder tests.
- Advance time explicitly (`provider.Advance(...)`) instead of adding real
  offsets; assert on exact `ScheduledAtUtc` values.
- Optionally expose a shared `FakeTimeProvider` helper from `TodoList.Testing`
  so unit and integration tests use the same approach.

**Acceptance criteria.**
- No reminder test depends on wall-clock delays.
- Back-off schedule is asserted exactly (1, 2, 4, 8 minutes, capped at 60).
- Tests remain green when the machine is under load.

## B4 — Record the shared integration-database decision

**Problem.** All integration tests in one collection share a single
Testcontainers PostgreSQL database. Data is not reset between tests; unique
emails keep them from colliding, but rows accumulate linearly and a few tests
need manual cleanup (for example pending reminders). The review called this
acceptable for the MVP but asked for it to be an explicit decision rather than
an accident.

**Plan.**
- Document the trade-off in `docs/development/testing.en.md` (and the Russian
  pair): one container per collection, no per-test reset, unique identities,
  targeted cleanup where ordering matters.
- Link the decision from ADR 0005 so future contributors know it is intentional.
- Record when to revisit: when the suite becomes slow or a test needs
  cross-test isolation, switch to a per-test transaction/schema or Respawn-style
  reset.

**Acceptance criteria.**
- The testing guide and ADR describe the current approach and its limits.
- The note names the trigger for revisiting the decision.

## Out of scope

- Load and performance testing under real traffic.
- Delivery behaviour against a real SMTP provider (Mailpit covers the happy
  path; production SMTP is verified during deployment).
- Coverage measurement; the review inferred gaps by reading tests.
