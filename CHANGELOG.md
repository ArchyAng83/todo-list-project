# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

### Fixed

- The task list no longer passes the literal string `_error` as its error message; Razor treats bare attribute text on `string` parameters as literal text, so the page always rendered its error state.
- Concurrent layout and page loads no longer share a `DbContext` operation and terminate the Blazor circuit.
