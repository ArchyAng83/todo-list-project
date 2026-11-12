# Project Status

Last updated: 2026-11-12

- **Current phase:** Week 8 - Import/export, accessibility, and v1.0 release (code complete)
- **Overall:** Foundation, task management, settings, demo data, the bearer API, component tests, E2E, the container image, the Kanban board, the month calendar, recurring tasks, the reminder outbox, import/export, and accessibility are complete; MVP ready for production deployment.

## Done

- Architecture, scope, and 8-week roadmap agreed.
- Solution with Domain, Application, Infrastructure, Web, and five test projects.
- Central package management, pinned .NET SDK, editor config, Git attributes.
- Docker Compose for PostgreSQL and Mailpit.
- GitHub Actions CI skeleton.
- Bilingual documentation foundation and progress tracking.
- Initial repository commit on `main`.
- Public GitHub repository with a passing GitHub Actions workflow.
- Domain model: projects, tags, tasks, checklist items, task tags, reminders.
- EF Core 10 + Npgsql persistence, snake_case naming, initial migration applied.
- ASP.NET Core Identity with cookie authentication and renamed Identity tables.
- Static SSR login, register, logout, and access-denied pages.
- MudBlazor application shell, public landing page, profile settings.
- RU/EN localization with cookie and `Accept-Language` negotiation.
- Projects: create, edit, archive/unarchive, delete, open and total task counts.
- Tags: create, edit, delete, usage counts, color preview.
- Tasks: quick add, edit dialog with status, priority, project, due date/time, reminder, tags, and checklist.
- Task list: debounced search, filters by status, project, and tag, status toggle, delete.
- Task views: Inbox, Today, Upcoming, All, and Completed, plus sorting by due date, priority, creation date, and title.
- Timezone-aware "today" and overdue highlighting based on the user timezone.
- Settings page persisted through `IUserSettingsStore`: display name, language, theme, timezone, week start, and default reminder.
- Live theme switching with system-preference detection and a small `theme.js` helper.
- Deterministic demo account (`demo@todolist.local` / `DemoPass123`) with projects, tags, tasks, and checklists, controlled by `Seed:Demo`.
- Minimal REST API `/api/v1` with bearer-only authentication, custom login endpoint, and tasks, projects, tags, and settings endpoints.
- Per-IP rate limiting on the login and registration endpoints (`Auth:RateLimit`, 10 requests per minute by default) with `429` and `Retry-After`.
- OpenAPI document at `/openapi/v1.json` and Scalar UI at `/scalar` in Development only.
- Command/query Application services with FluentValidation and `Result<T>`.
- Unit tests for domain rules, recurrence, and user settings (54 passing).
- Testcontainers-based integration tests for auth, persistence, task management, board ordering, calendar ranges, recurrence, reminder delivery, auth throttling, and the REST API (72 passing).
- bUnit component tests for the task list, Kanban board, calendar grid, layout, and culture switcher (32 passing).
- Playwright E2E journeys against a real Kestrel process, Testcontainers PostgreSQL, and headless Chromium (14 passing).
- The post-review follow-up backlog is closed: E2E journeys own their users, reminder delivery tests use a fake clock, and the shared integration database is documented in the testing guide and ADR 0005.
- `/health/live` and `/health/ready` endpoints with a database readiness check.
- JSON console logging outside Development for container platforms.
- Multi-stage `Dockerfile` running as a non-root user on port 8080, plus `.dockerignore`.
- CI that runs every test suite, installs Playwright browsers, and builds the container image.
- Kanban board at `/board` with `Todo`, `InProgress`, and `Done` columns ordered by a manual sort order.
- Drag-and-drop between and inside columns, plus a card menu with move up, move down, and move to another column.
- `TaskService.MoveAsync` renumbers only the source and target columns in one `SaveChangesAsync`; `TaskQueries.BoardAsync` serves the ordered board.
- Calendar at `/calendar` with a 42-day month grid, tasks placed on their due date, done and overdue styling, and month navigation.
- The grid follows the persisted `WeekStart` setting and shows the adjacent days that complete the week rows.
- Create-on-click: selecting a day opens a dialog prefilled with that date, and editing a task never creates a new one.
- `TaskQueries.CalendarAsync` returns an inclusive due-date range in day/time/priority order, and `GET /api/v1/tasks/calendar` exposes it to API clients.
- Recurring tasks with daily, weekly, and monthly frequencies, a 1-365 interval, and an optional end date, configured in the task dialog and shown with a repeat icon.
- Completing a recurring task creates the next occurrence once, copying content, tags, reminder, and checklist titles while resetting checklist progress; a due-date change re-arms generation.
- Database-backed reminder outbox driven by `TaskReminderScheduler`: at most one pending reminder per task, rescheduled on edit, cancelled on completion.
- `ReminderDeliveryService` and `ReminderDispatcher` deliver due reminders, localize the email by the recipient's culture, and mark each message sent or failed.
- SMTP transport with a Mailpit default in Development and a logging fallback, plus `Email` and `Reminders` configuration sections.
- JSON import/export (`/data` page and `/api/v1/data`) with a versioned schema, reuse-by-name for projects and tags, and `ImportSummary`.
- CSV export with UTF-8 BOM, quoted fields, and CRLF line endings.
- `/data` page with JSON/CSV export, a JSON file picker, and an `aria-live` import result; a skip link, main landmark, localized nav labels, and `:focus-visible` styles improve accessibility.
- Production deployment assets: `DataProtection:KeysPath` key persistence on a volume, opt-in forwarded headers with a proxy trust list, secure auth/antiforgery cookies outside Development, and startup warnings for an unpersisted key ring or a missing SMTP host.
- `Dockerfile.migrations` builds a self-contained EF Core migration bundle, `docker-compose.production.yml` runs PostgreSQL + migrate + app, and `.env.production.example` documents every variable.

## In progress

## Next

- Choose a hosting target and a production SMTP provider, then deploy the prepared release and run the go-live checklist.

## Blockers and risks

- Docker Desktop must be running for PostgreSQL, Mailpit, integration tests, and E2E.
- The production SMTP provider and hosting target will be chosen before the MVP deployment.
- The reminder dispatcher has no cross-replica row claim, so production runs a single app replica until a `FOR UPDATE SKIP LOCKED` claim is implemented.

## Quality gates

| Gate | Status |
|---|---|
| Build | ✅ 0 warnings, 0 errors |
| Unit tests | ✅ 54 passing |
| Integration tests | ✅ 72 passing (Testcontainers) |
| Component tests | ✅ 32 passing (bUnit) |
| E2E tests | ✅ 14 passing (Playwright) |
| Container image | ✅ builds; migration bundle applies migrations; health probes respond |
| CI | ✅ all recent runs passed on GitHub Actions |
| Deployment assets | ✅ prepare a production stack; hosting and SMTP remain operator decisions |
| Deployed | ⬜ production deployment pending hosting/SMTP selection |

## Deployment notes

- The app runs on Kestrel in a container on port 8080 and expects a PostgreSQL database (`Default` connection string). Reminders need `Email:Host`; without it they are logged and a startup warning is emitted.
- `DataProtection:KeysPath` must point at a persistent volume; losing the key ring invalidates cookies, antiforgery tokens, and bearer tokens.
- Behind a TLS proxy set `ForwardedHeaders:Enabled=true` and trust the proxy through `KnownProxies`/`KnownNetworks`.
- Production migrations run through `Dockerfile.migrations` or the compose `migrate` service; they are applied on startup only in Development.
- Migration `AddTaskRecurrence` was hand-edited to set the `recurrence` default to the `None` enum string and must not be regenerated.
- Demo credentials (`demo@todolist.local` / `DemoPass123`) are for local evaluation only and must be removed or rotated in production.
