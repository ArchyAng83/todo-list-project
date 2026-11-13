# AGENTS.md

Project guidance for coding agents working in this repository. Read it before
changing code. Deeper explanations live in `docs/`; this file is the short,
non-negotiable version.

## Project

TodoList is a multi-user task manager built as a production-minded portfolio
project. It targets .NET 10, Blazor Web App (Interactive Server), and
PostgreSQL 17. The UI and documentation are bilingual (RU/EN). v1.0 is code
complete; production deployment (hosting + SMTP provider) is the remaining
milestone. An AI layer is planned only after v1.0 and must not leak into the
domain now.

Current snapshot: `docs/progress/status.en.md`; the newest
`docs/progress/week-*.en.md` is the latest weekly log.

## Golden rules

- Keep the modular-monolith boundaries: `Domain <- Application <- Infrastructure <- Web`.
- No generic repositories, MediatR, or AutoMapper. Do not add them without an ADR.
- Scope every user-owned query by `UserId`; cross-user access returns 404.
- Never hardcode UI strings; add both English and Russian resources.
- Never edit an applied EF Core migration; add a new one.
- Add package versions to `Directory.Packages.props`, not to project files.
- Run build and the relevant tests before committing. Warnings are errors.
- Do not commit secrets. The dev database credentials in docs are local only.

## Commands

```bash
# Infrastructure (PostgreSQL on 5433, Mailpit on 1025/8025)
docker compose up -d

# Build
dotnet build TodoList.sln -c Debug

# Run the app (http://localhost:5122)
dotnet run --project src/TodoList.Web --launch-profile http

# Tests
dotnet test tests/TodoList.UnitTests/TodoList.UnitTests.csproj
dotnet test tests/TodoList.IntegrationTests/TodoList.IntegrationTests.csproj   # needs Docker
dotnet test TodoList.sln -c Release                                            # same as CI

# One suite or one test (filters are the fastest feedback loop)
dotnet test tests/TodoList.ComponentTests/TodoList.ComponentTests.csproj
dotnet test tests/TodoList.IntegrationTests/TodoList.IntegrationTests.csproj \
  --filter "FullyQualifiedName~AuthRateLimitTests"
dotnet test tests/TodoList.E2ETests/TodoList.E2ETests.csproj \
  --filter "FullyQualifiedName~UserJourneyTests"

# Install the Playwright browser once after building (needed for E2E)
pwsh tests/TodoList.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium

# EF Core migrations (dotnet-ef is pinned by .config/dotnet-tools.json)
dotnet tool restore    # once per clone; dotnet ef resolves the local tool
dotnet ef migrations add <Name> \
  --project src/TodoList.Infrastructure \
  --startup-project src/TodoList.Web \
  --output-dir Persistence/Migrations
dotnet ef database update \
  --project src/TodoList.Infrastructure \
  --startup-project src/TodoList.Web

# Operations (Docker Desktop or the production host)
pwsh ./scripts/backup-postgres.ps1 -OutputDirectory D:\backups\todolist
pwsh ./scripts/restore-postgres.ps1 -BackupFile <dump> -Force
pwsh ./scripts/smoke-test.ps1 -BaseUrl http://localhost:8080 -Email <email> -Password <password>
```

## Repository map

```text
src/
  TodoList.Domain/         entities, enums, invariants; no framework dependencies
  TodoList.Application/    use cases, DTOs, validators, IAppDbContext, Result/Error
  TodoList.Infrastructure/ EF Core, Identity, email, background jobs
  TodoList.Web/            Blazor UI, auth pages, minimal `/api/v1`, composition root
tests/
  TodoList.UnitTests/       domain and pure Application tests (xUnit + Shouldly)
  TodoList.IntegrationTests/ WebApplicationFactory + Testcontainers PostgreSQL
  TodoList.ComponentTests/  bUnit component tests
  TodoList.E2ETests/        Playwright .NET E2E tests
  TodoList.Testing/         shared fixtures, builders, fakes (not a test project)
docs/                       bilingual docs, ADRs, progress tracking
```

## Architecture invariants

- `Domain` has no references to Application, Infrastructure, or Web.
- `Application` references only `Domain` and abstractions it owns.
- `Infrastructure` implements Application abstractions (`IAppDbContext`, `IEmailSender`).
- `Web` is the composition root and may reference Application and Infrastructure.
- Application services are split into commands and queries. Return `Result<T>` for
  expected failures and throw for unexpected failures.
- Reminder dispatch is serialized with `ReminderDispatchLock` (a PostgreSQL
  session advisory lock, ADR-0007). Do not remove it or run a second dispatcher
  without a replacement claim.
- `ReminderRetentionWorker` deletes only terminal (`Sent`, `Failed`, `Cancelled`)
  reminders older than `Reminders:RetentionDays`; never delete `Pending` rows.
- Pass `UserContext` explicitly; never read the current user from a static or
  ambient context.
- Manual mapping only. Keep DTOs in Application, not Domain.
- Use one `SaveChangesAsync` per use case; explicit transactions only for
  multi-step operations.
- MVPs use hard deletes. Soft delete/trash is a later feature.

## Time, IDs, and data

- Due dates are local `DateOnly`/`TimeOnly`. Instants are UTC `DateTimeOffset`.
- Inject `TimeProvider`; never call `DateTime.Now`, `DateTime.UtcNow`, or
  `DateTimeOffset.UtcNow` directly in Domain or Application.
- New aggregate IDs use `Guid.CreateVersion7()`.
- Enums are persisted as strings (`HasConversion<string>()`).
- `TodoTask` uses PostgreSQL `xmin` as its concurrency token.
- `TodoTask`, `Project`, `Tag`, `ChecklistItem`, `TaskTag`, and `Reminder` are
  the current aggregates. Checklist items and task-tag links are owned by
  `TodoTask`.

## EF Core and the database

- PostgreSQL is published on host port **5433** because port 5432 is often taken
  by a locally installed PostgreSQL. Do not change this without updating docs.
- snake_case naming is applied globally via `EFCore.NamingConventions`; do not
  hand-write table or column names unless there is a reason.
- Identity tables are renamed to `users`, `roles`, `user_roles`, `user_claims`,
  `user_logins`, `user_tokens`, and `role_claims`.
- `IEntityTypeConfiguration<T>` classes live in
  `Infrastructure/Persistence/Configurations`.
- Use `AsNoTracking` for read queries and project into DTOs when possible.
- Development applies migrations on startup through `MigrateDatabaseAsync`.
  Production must apply them as a separate deploy step.
- Register the context through `IDbContextFactory` in components; never hold a
  long-lived scoped `DbContext` in a Blazor component.

## Blazor and UI

- App pages use Interactive Server. Auth pages use static SSR so Identity can set
  cookies during the HTTP response.
- `App.razor` selects the render mode with
  `HttpContext.AcceptsInteractiveRouting()`; auth pages opt out with
  `[ExcludeFromInteractiveRouting]`.
- Keep `public partial class Program;` in `Program.cs`; integration tests need it
  as the `WebApplicationFactory` entry point.
- Use MudBlazor components for the app shell and pages. Keep custom CSS in
  `wwwroot/app.css` or scoped `.razor.css` files.
- Components resolve Application services through `IServiceScopeFactory`.
  Blazor Server keeps one DI scope per circuit, and the layout and page load
  concurrently, so `CurrentUserProvider` resolves `UserManager` in a per-call
  scope. Never share one `DbContext` operation across concurrent components.
- Every data-driven page must show loading, empty, and error states.
- Forms use `EditForm`. `[SupplyParameterFromForm]` properties must use
  `= default!` and be initialized with `Input ??= new();` in `OnInitialized`
  to avoid BL0008.
- The app has RU and EN cultures resolved from a cookie, then `Accept-Language`.

## Auth and rate limiting

- `Auth:RateLimit` (`PermitLimit` 10, `WindowSeconds` 60 by default) is mounted
  with `app.UseWhen(IsAuthenticationRequest, ...)` above the status-code pages,
  so only `POST /api/v1/auth/login`, `POST /account/login`, and
  `POST /account/register` are throttled; page loads and the rest of the API are
  not. A rejection stays a bare `429` with `Retry-After`.
- The partition key is `HttpContext.Connection.RemoteIpAddress`. Behind a proxy,
  forwarded headers must be enabled and the proxy trusted, otherwise every
  request shares the proxy partition; see `docs/development/deployment.en.md`.
- Identity lockout (5 attempts, 15 minutes) remains the per-account protection;
  the limiter covers distributed password spraying.
- Test hosts raise the limit (`TodoListWebApplicationFactory` and the E2E
  process set `Auth:RateLimit:PermitLimit=1000`). A throttling test stands up a
  host with a low permit plus a startup filter that assigns `RemoteIpAddress`
  (a request header works), because `TestServer` leaves it unset and all
  in-process clients would share one partition.

## Production runtime config

- `DataProtection:KeysPath` persists the key ring; the image defaults it to
  `/app/keys`. Losing it invalidates auth cookies, antiforgery tokens, and
  bearer/refresh tokens. Compose mounts `data-protection-keys` there.
- `AllowedHosts` must list the public host names in production; the app logs a
  warning when it is empty or `*`.
- `SecurityHeaders` adds CSP, `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, and `Permissions-Policy`. Keep the default CSP compatible
  with Blazor Server (`'unsafe-inline'` for styles/scripts) or test the UI after
  changing it.
- `Email` and `Reminders` options use `ValidateOnStart`; out-of-range values fail
  the host at startup.
- Auth and antiforgery cookies are `Secure` (`Always`) outside Development.
  Development uses `SameAsRequest` so plain HTTP on port 5122 still works.
- `ForwardedHeaders:Enabled` is off by default. When on, only
  `X-Forwarded-For`/`X-Forwarded-Proto` are honoured, with `ForwardLimit` and an
  explicit `KnownProxies`/`KnownNetworks` list; setting either list replaces the
  loopback defaults, and an invalid IP/CIDR fails startup.
- Empty `Email:Host` registers `LoggingEmailSender` and logs a warning while
  reminders stay enabled. The dispatcher has no cross-replica row claim, so
  production runs one app replica.
- Production schema changes run through `Dockerfile.migrations` or the compose
  `migrate` service, never through app startup. `.config/dotnet-tools.json` pins
  `dotnet-ef`; compose must be invoked with `--env-file .env.production`.

## Localization

- UI strings live in `src/TodoList.Web/Resources/SharedResource.resx` (English)
  and `SharedResource.ru.resx` (Russian).
- Add or change keys in both files in the same commit.
- Use `IStringLocalizer<SharedResource>`, never literal user-facing text.
- Code, identifiers, XML docs, commit messages, and English docs are English.
  UI text and Russian docs are translated.

## Testing

- Domain rule or pure Application logic -> unit test (xUnit + Shouldly).
- Auth, persistence, API, or Identity change -> integration test.
- Integration tests run against real PostgreSQL via Testcontainers and require
  Docker. Do not use the EF in-memory provider.
- One `TodoListAppFixture` per `IntegrationCollection` starts a single container,
  applies migrations once, and never resets the database between tests. New
  tests create their own users (unique `Guid` emails), assert only on rows they
  created, and explicitly clear global rows they depend on. Read the
  "Integration test database" section of `docs/development/testing.en.md` before
  adding cleanup code or a second fixture.
- `TodoListAppFixture.ConnectionString` lets a test start an extra host with
  different configuration (for example a low rate limit) without migrating a
  second database.
- Shared fixtures live in `TodoList.Testing`; test files live in the test
  projects. `[CollectionDefinition]` must be in the same assembly as the tests
  that use it (xUnit1041).
- Testcontainers 4.16 uses `new PostgreSqlBuilder("postgres:17-alpine")`; the
  parameterless builder is obsolete.
- Name tests `Method_State_ExpectedBehavior`.
- No network calls in unit tests. Replace `TimeProvider` and `IEmailSender`
  with fakes where time or email matters. Reminder delivery tests pass a
  `FakeTimeProvider` and advance it; never sleep or rewrite `ScheduledAtUtc` in
  SQL. Lock tests acquire `ReminderDispatchLock` from two scopes and assert only
  one succeeds; retention tests use a fixed clock.
- CI also runs a vulnerable-package gate, CodeQL, Trivy image scans, Dependabot,
  and `scripts/smoke-test.ps1` on a disposable compose stack.
- Component tests (bUnit) target presentational components through
  `ComponentTestContext`, which pins the culture to English; E2E tests
  (Playwright) start the real app process on a free port.
- Integration and E2E hosts set `Reminders:Enabled=false`; reminder delivery is
  driven by `FakeTimeProvider` in tests, not by the background dispatcher.
- E2E journeys call `E2EFixture.CreateUserAsync()`: each registers a unique user
  through the real form and seeds its data through `/api/v1`. Never assert on the
  demo account; `Seed:Demo` stays on only so a broken seeder fails startup.

## Conventions

- Nullable reference types and implicit usings are enabled; warnings are errors.
- File-scoped namespaces, one public type per file, `sealed` by default.
- `async`/`await` everywhere; append `Async` to asynchronous methods; forward
  `CancellationToken`. No `.Result` or `.Wait()`.
- Four-space indentation. `.editorconfig` is the source of truth.
- Keep diffs focused. Do not perform drive-by refactors or reformat unrelated
  files.

## Documentation and git

- English docs are canonical; update the paired `.ru.md` file in the same change.
- When a milestone closes, update `docs/progress/status.*`,
  `docs/progress/week-*.md`, `CHANGELOG.md`, and the index in `docs/README.*`.
- Keep the test counts in `README.md` and `docs/progress/status.*` equal to the
  last full `-c Release` run; they have drifted before.
- Significant architectural decisions get an ADR in
  `docs/architecture/decisions/`.
- Commit directly to `main` in this repository. `docs/development/git-workflow.en.md`
  describes the PR/squash model to follow once development moves to branches.
  Use Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `chore:`).
- `README.md` holds the public overview; do not let it drift from the docs.

## Known gotchas

- The app locks its DLLs on Windows. Stop the process on port 5122 before
  rebuilding if the build fails with file-in-use errors.
- Docker Desktop must be running for PostgreSQL, Mailpit, and integration/E2E.
- The SDK is pinned in `global.json` (10.0.400). A mismatch fails the build.
- `dotnet-ef` may print a version warning; it is harmless as long as migrations
  are generated correctly.
- The `AddTaskRecurrence` migration was hand-edited so the new `recurrence`
  column defaults to the `None` enum string. Do not regenerate it; a fresh
  scaffold produces an invalid default for existing rows.
- `.playwright-mcp/` and `TestResults/` are ignored artifacts; do not commit them.
- E2E waits for the `_bl_*` handler marker before clicking; otherwise a click can
  submit a form natively before the Blazor circuit starts.
- The reminder advisory lock is session-scoped: it is held on the open
  `AppDbContext` connection and released by scope disposal or connection close.
  Do not re-register `ReminderDispatchLock` as singleton or share it across
  scopes.
- `backup-postgres.ps1`/`restore-postgres.ps1` default to the production compose
  file and `.env.production`; for the dev stack pass `-ComposeFile
  docker-compose.yml -EnvFile ""` (and `-SkipKeys` for backups, because the dev
  stack has no app service).
- `smoke-test.ps1` needs an existing account: CI uses the demo account from the
  Development smoke stack, while a real deploy uses a provisioned smoke user.
- The migration bundle is a self-contained single-file executable, so the
  read-only `migrate` service needs `DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/.net`
  (already set in compose); without it the bundle exits 159.
- Razor treats a bare attribute value on a `string` component parameter as
  literal text: write `Error="@_error"`, not `Error="_error"`.
- If a build fails only in `TodoList.Testing`, make sure it stays
  `<IsTestProject>false</IsTestProject>`.
- On this Windows host, edit UTF-8/Cyrillic files with the file tools, not with
  PowerShell text round-trips, which mangle the encoding.
- `dotnet test ... | Select-String` often prints nothing; tee the output to a
  temp file and search the file instead.

## Read more

- `docs/development/setup.en.md` — local setup and infrastructure.
- `docs/development/conventions.en.md` — coding conventions.
- `docs/development/testing.en.md` — test pyramid, commands, and the shared
  integration database.
- `docs/development/deployment.en.md` — configuration, rate limits, migrations,
  and proxy caveats.
- `docs/architecture/overview.en.md` — architecture and flows.
- `docs/architecture/decisions/` — ADRs.
- `docs/progress/backlog.en.md` — open follow-ups; currently all review items
  are closed.
- `docs/product/roadmap.en.md` — phased roadmap.
