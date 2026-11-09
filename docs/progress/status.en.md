# Project Status

Last updated: 2026-11-09

- **Current phase:** Week 6 - Calendar month view (code complete)
- **Overall:** Foundation, task management, settings, demo data, the bearer API, component tests, E2E, the container image, the Kanban board, and the month calendar are complete; MVP in progress.

## Done

- Architecture, scope, and 8-week roadmap agreed.
- Solution with Domain, Application, Infrastructure, Web, and five test projects.
- Central package management, pinned .NET SDK, editor config, Git attributes.
- Docker Compose for PostgreSQL and Mailpit.
- GitHub Actions CI skeleton.
- Bilingual documentation foundation and progress tracking.
- Initial repository commit on `main`.
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
- OpenAPI document at `/openapi/v1.json` and Scalar UI at `/scalar`.
- Command/query Application services with FluentValidation and `Result<T>`.
- Unit tests for domain rules and user settings (35 passing).
- Testcontainers-based integration tests for auth, persistence, task management, board ordering, calendar ranges, and the REST API (33 passing).
- bUnit component tests for the task list, Kanban board, calendar grid, layout, and culture switcher (31 passing).
- Playwright E2E journeys against a real Kestrel process, Testcontainers PostgreSQL, and headless Chromium (11 passing).
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

## In progress

- First CI run (pending publication of the GitHub repository).
- Deployment target selection for the MVP.

## Next

- Week 7: recurring tasks and email reminders with a database-backed outbox and dispatcher.

## Blockers and risks

- Docker Desktop must be running for PostgreSQL, Mailpit, integration tests, and E2E.
- GitHub CLI is not installed; pushing to the public repository is pending.
- The production SMTP provider and hosting target will be chosen before the MVP deployment.

## Quality gates

| Gate | Status |
|---|---|
| Build | ✅ 0 warnings, 0 errors |
| Unit tests | ✅ 35 passing |
| Integration tests | ✅ 33 passing (Testcontainers) |
| Component tests | ✅ 31 passing (bUnit) |
| E2E tests | ✅ 11 passing (Playwright) |
| Container image | ✅ builds; health probes respond |
| CI | ⬜ workflow added, first run pending |
| Deployed | ⬜ |
