# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-11-14

### Added

- Reminder dispatch claim: `ReminderDispatchLock` serializes `ReminderDispatcher` with a PostgreSQL session advisory lock, so several app replicas cannot send the same reminder twice (ADR-0007).
- `ReminderRetentionWorker` deletes terminal (`Sent`, `Failed`, `Cancelled`) reminders after `Reminders:RetentionDays`; `Pending` rows are never removed.
- Browser hardening headers with a Blazor-compatible Content Security Policy, `AllowedHosts` guidance and a production warning when it remains `*`, and `ValidateOnStart` for `Email` and `Reminders` options.
- Container hardening: read-only root filesystem, `/tmp` tmpfs, `no-new-privileges`, `cap_drop: ALL`, PID limit, and CPU/memory limits for the app; the migration service also drops capabilities.
- `scripts/backup-postgres.ps1` backs up PostgreSQL and the Data Protection key ring; `scripts/restore-postgres.ps1` restores a dump into a scratch database and verifies it.
- `scripts/smoke-test.ps1` checks health, hardening headers, bearer login, task create/read/delete, board, calendar, and JSON export.
- `docs/operations/backup-restore.*` and `docs/operations/smoke-test.*` runbooks in English and Russian.
- `docker-compose.smoke.yml` for a disposable CI/local stack that seeds the demo account and runs the smoke test.
- CI security: vulnerable-package gate, CodeQL workflow, Trivy scans of both images, Dependabot configuration, and a smoke-test stage on the disposable compose stack.
- Integration tests for advisory-lock exclusivity, reminder retention, and security headers.

### Changed

- The production compose stack no longer requires a single app replica for reminder delivery; one replica is still recommended for Blazor circuits unless the proxy supports sticky sessions.
- Reminder and email options are validated at startup instead of failing on the first reminder.
- The deployment guide covers security headers, `AllowedHosts`, retention, container hardening, backups, and the smoke test.
- Test counts are now 54 unit, 76 integration, 32 component, and 14 E2E (176 total).

## [1.0.1] - 2026-11-12

### Added

- Production deployment hardening: configurable Data Protection key persistence, opt-in forwarded headers with an explicit proxy trust list, secure auth/antiforgery cookies outside Development, and startup warnings for an unpersisted key ring and a missing SMTP host.
- `Dockerfile.migrations` builds a self-contained EF Core migration bundle from the pinned `dotnet-ef` tool manifest; `Dockerfile` prepares `/app/keys` for the key-ring volume.
- `docker-compose.production.yml` and `.env.production.example` describe the reference PostgreSQL + migrate + app stack with health checks and a persistent key-ring volume.
- ADR 0006 records the production deployment shape; the deployment guide now covers Data Protection, proxy/TLS, email, the single-replica reminder constraint, and an expanded go-live checklist.
- `appsettings.Development.json` carries the local PostgreSQL connection string and Mailpit settings, so a fresh clone runs without extra configuration.

### Changed

- `AddInfrastructure` receives the host environment and applies `CookieSecurePolicy.Always` outside Development.
- Reminder documentation now matches the code: `System.Net.Mail.SmtpClient`, no `FOR UPDATE SKIP LOCKED` claim, and one app replica for the MVP.

### Fixed

- Per-IP throttling for the login and registration POST endpoints (`Auth:RateLimit`).
- Task status enum validation plus `ETag`/`If-Match` optimistic concurrency on the task API.
- Import validation errors answer `400` instead of `500`, and validation codes map consistently.
- Removed the committed database password and the hardcoded demo password; both come from configuration.
- Removed the `DateTimeOffset.UtcNow` default from `ApplicationUser`, localized the remaining hardcoded messages, and fixed demo-seed timezone and ordering.
- OpenAPI and Scalar are exposed only in Development.

## [Unreleased]

### Added

- Initial solution structure: Domain, Application, Infrastructure, Web, and test projects.
- Central package management, pinned .NET SDK, `.editorconfig`, and Git attributes.
- Docker Compose for PostgreSQL and Mailpit.
- Bilingual documentation skeleton and project progress tracking.
- GitHub Actions CI skeleton.
- Domain model for projects, tags, tasks, checklist items, task tags, and reminders.
- EF Core 10 + Npgsql persistence with snake_case naming, concurrency tokens, and the initial migration.
- ASP.NET Core Identity with cookie authentication and static SSR login, register, logout, and access-denied pages.
- MudBlazor application shell, public landing page, tasks placeholder, and profile settings page.
- RU/EN localization with a cookie-based culture switcher and `Accept-Language` fallback.
- Domain unit tests and Testcontainers-based integration tests for auth and persistence.
- Application layer for projects, tags, and tasks split into command and query services, with FluentValidation and `Result<T>`.
- Projects page with create, edit, archive/unarchive, delete, and open/total task counts.
- Tags page with create, edit, delete, color preview, and usage counts.
- Tasks page with quick add, debounced search, status/project/tag filters, status toggle, edit, and delete.
- Task edit dialog with title, notes, status, project, priority, due date/time, reminder, tags, and checklist items.
- Integration tests for project scoping, task filters, checklist and tag persistence, duplicate tag conflicts, and cross-user access.
- `CurrentUserProvider` and per-operation DI scopes for Blazor pages and dialogs.
- Task views (Inbox, Today, Upcoming, All, Completed) and sorting by due date, priority, creation date, and title.
- Timezone-aware "today" and overdue highlighting based on the user timezone.
- Persisted user settings with `IUserSettingsStore`: display name, language, theme, timezone, week start, and default reminder.
- Live theme switching with system-preference detection.
- Deterministic demo account with projects, tags, tasks, and checklists, controlled by `Seed:Demo`.
- Minimal REST API `/api/v1` with bearer-only authentication, custom login and refresh endpoints, and tasks, projects, tags, and settings endpoints.
- OpenAPI document and Scalar UI.
- Integration tests for bearer login, cookie-only rejection, API scoping, task views, priority sorting, and settings persistence.
- `TaskList` presentational component with loading, refreshing, empty, filtered-empty, error, and retry states.
- `ICurrentUserProvider` seam so components can be tested without the Identity stack.
- 14 bUnit component tests for the task list, main layout, and culture switcher.
- Playwright E2E fixture that starts PostgreSQL, a real Kestrel process, and headless Chromium, plus 6 user journeys.
- `/health/live` and `/health/ready` endpoints with a PostgreSQL readiness check.
- JSON console logging outside Development, with scopes and UTC timestamps.
- Multi-stage `Dockerfile` running as a non-root user on port 8080, plus `.dockerignore`.
- CI installs Playwright browsers and builds the container image.
- Deployment guide covering configuration, migrations, probes, and Blazor Server constraints.
- Kanban board at `/board` with `Todo`, `InProgress`, and `Done` columns, per-column counts, and an empty-column placeholder.
- `TaskService.MoveAsync` and `MoveTaskRequest` for moving a task to a status and zero-based position, renumbering only the source and target columns.
- `TaskBoard`/`TaskBoardColumn` DTOs and `TaskQueries.BoardAsync`, sharing one `ToListItem` projection with the task list.
- HTML5 drag-and-drop between and inside board columns, with a `board.js` helper that seeds the drag payload.
- Card menu with move up, move down, and move to another column, plus edit and delete actions.
- Board navigation entry and English/Russian resource keys.
- `GET /api/v1/tasks/board` returns the ordered columns, and `POST /api/v1/tasks/{id}/move` moves a card for API clients.
- 6 integration tests for board ordering, index clamping, cross-user moves, and validation; 8 bUnit tests for the board; 2 Playwright journeys for menu and drag moves.
- Month calendar at `/calendar` with a 42-day grid, tasks placed on their due date, done and overdue styling, month navigation, and a "today" shortcut.
- The calendar grid follows the persisted `WeekStart` setting and shows the adjacent days that complete the week rows.
- Create-on-click: selecting a day opens a dialog prefilled with that date, plus project, priority, due date, and due time.
- `TaskQueries.CalendarAsync` and `GET /api/v1/tasks/calendar?from=&to=` return an inclusive due-date range ordered by day and time, with an inverted range rejected as `400`.
- 3 integration tests, 9 bUnit tests, and 3 Playwright journeys for calendar placement, navigation, and creation.
- Recurring tasks with daily, weekly, and monthly frequencies, a 1-365 interval, and an optional end date, configured in the task dialog and marked with a repeat icon in the list.
- `TodoTask.SetRecurrence`, `CreateNextOccurrence`, and `CopyStructureFrom`, plus a `RecurrenceGenerated` guard so reopening and re-completing does not duplicate occurrences; a due-date change re-arms generation.
- The next occurrence copies content, project, priority, reminder, tags, and checklist titles, and resets checklist progress.
- Database-backed reminder outbox with `TaskReminderScheduler`: one pending reminder per task, rescheduled on edit, cancelled on completion, and created for each next occurrence.
- `ReminderDeliveryService` and `ReminderDispatcher` deliver due reminders, localize the email by the recipient's culture, and mark messages `Sent` or `Failed` with the error text.
- Reminder email templates in English and Russian, plus `EmailOptions`, `Email`/`Reminders` configuration, and the `AddTaskRecurrence` migration.
- SMTP transport through Mailpit in Development, with a logging sender when `Email:Host` is empty.
- `UserTime` centralizes timezone-aware "today" and local-to-UTC conversion for reminders.
- Recurrence fields on `CreateTaskRequest`/`UpdateTaskRequest`, the API bodies, and the task endpoints as optional append-only values.
- 12 unit tests for recurrence and copying, 9 integration and 3 API tests for generation, rescheduling, cancellation, and delivery, 1 bUnit test for the repeat indicator, and 1 Playwright journey for completing a recurring task.
- Per-IP rate limiting for the login and registration POST endpoints (`Auth:RateLimit`, 10 requests per 60 seconds by default) that answers `429` with `Retry-After`.

### Changed

- EF Core now uses `QuerySplittingBehavior.SplitQuery` to keep task collection queries efficient.
- Checklist items and task-tag links are added to the context explicitly after being created through the `TodoTask` aggregate.
- Priority sorting uses a translatable `CASE` expression instead of a CLR helper method.
- Pages and dialogs inject `ICurrentUserProvider` instead of the concrete `CurrentUserProvider`.
- `CurrentUserProvider` resolves `UserManager` in a per-call scope because Blazor Server shares one scope per circuit.
- `TodoTask.SetSortOrder` ignores a call that would write the current value, so board renumbering does not mark every card as modified.
- E2E browser contexts use a 1280x800 viewport so the Kanban columns stay side by side during drag-and-drop.
- `CreateTaskRequest` and `CreateTaskBody` accept optional priority, due date, and due time at the end, so existing callers and API clients stay valid.
- `ListAsync`, `BoardAsync`, and `CalendarAsync` share one `ToListItem` projection, so the task shape cannot drift between views.
- Component test contexts pin both `CurrentCulture` and `CurrentUICulture` to English, because the calendar formats month and weekday names from `CurrentCulture`.
- Reminder email templates are read through a `ResourceManager` over the embedded `.resx`, because `IStringLocalizer` did not resolve the Infrastructure assembly's resources.
- Reminder times convert the user's local due date/time to UTC and default date-only tasks to 09:00 local.
- Task edits reconcile the reminder outbox in the same `SaveChangesAsync`, and the demo seed persists reminders and a recurring "Weekly planning" task.
- Integration and E2E tests set `Reminders:Enabled=false` so background delivery stays deterministic.
- Each Playwright journey registers its own user and seeds data through `/api/v1` instead of sharing the demo account, so journeys no longer depend on execution order.
- Reminder integration tests drive `ReminderDeliveryService` with a `FakeTimeProvider` instead of wall-clock offsets, so back-off windows are asserted exactly.
- The testing guide and ADR 0005 record the shared per-collection integration database, its isolation rules, and the conditions that trigger a per-test reset.

### Fixed

- The task list no longer passes the literal string `_error` as its error message; Razor treats bare attribute text on `string` parameters as literal text, so the page always rendered its error state.
- Concurrent layout and page loads no longer share a `DbContext` operation and terminate the Blazor circuit.
