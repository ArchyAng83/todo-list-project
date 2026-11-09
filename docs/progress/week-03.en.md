# Week 3 — UX polish and REST API

Dates: 2026-11-16 — 2026-11-22

Status: Code complete; API and page smoke checks passed

## Goals

- Task views and sorting on top of search and filters.
- Persisted user settings: language, theme, timezone, week start, default reminder.
- Deterministic demo account with representative data.
- Minimal REST API `/api/v1` with bearer tokens, OpenAPI, and Scalar.
- Integration coverage for API authentication, scoping, views, sorting, and settings.

## Done

- `TaskView` (All, Inbox, Today, Upcoming, Completed) and `TaskSort` (Default, DueDate, Priority, Created, Title).
- `TaskQueries` applies views and sorting in SQL and resolves "today" in the user's timezone from `UserContext`.
- Tasks page shows view buttons, a sort selector, and timezone-aware overdue highlighting.
- Settings slice: `UserSettings`, `UpdateUserSettingsRequest`, FluentValidation rules, `IUserSettingsStore`, and `UserSettingsService`.
- `UserSettingsStore` persists settings through Identity in Infrastructure, keeping Identity out of Application.
- Settings page edits display name, language, theme, timezone (searchable), week start, and the default reminder, with loading, error, and validation states.
- `ThemePreference` keeps the MudBlazor theme in sync; the layout follows the system preference through a small `theme.js` helper.
- `DemoDataSeeder` creates `demo@todolist.local` with three projects, three tags, eight tasks, and checklists; controlled by `Seed:Demo`.
- Custom bearer login endpoint `POST /api/v1/auth/login` and token refresh endpoint `POST /api/v1/auth/refresh`.
- Bearer-only `/api/v1` group with tasks, projects, tags, and `/me/settings` endpoints.
- OpenAPI document at `/openapi/v1.json` and Scalar UI at `/scalar`.
- JSON string enums for the API; `DateOnly`/`TimeOnly` use the standard ISO representation.
- Integration tests for bearer login, cookie-only rejection, task scoping, views, priority sorting, settings round-trip, and timezone validation.
- English and Russian resources for every new string.

## Decisions

- A custom login endpoint replaces `MapIdentityApi` so the response shape, status codes, and OpenAPI metadata stay under application control.
- The API group explicitly requires `IdentityConstants.BearerScheme`; the cookie used by Blazor cannot access `/api/v1`.
- Application owns `IUserSettingsStore`; Infrastructure implements it with `UserManager`. No ASP.NET Core Identity type leaks into Application.
- `TaskListFilter` was extended append-only with `View` and `Sort`, preserving existing positional call sites.
- Priority sorting uses an inline conditional so EF Core can translate it to a `CASE` expression; a helper method is not translatable.
- The timezone list and system dark-mode detection stay at the UI edge; Application receives plain values.

## Metrics

- Build: passing with 0 warnings and 0 errors in Debug and Release.
- Tests: 35 unit tests and 24 integration tests passing.
- Smoke checks: bearer login, authenticated API calls, 401 for anonymous and cookie-only requests, `/tasks`, `/settings`, `/openapi/v1.json`, and `/scalar` respond correctly.

## Next

- Week 4: bUnit component tests, Playwright E2E, Dockerfile, health checks, structured logging, and the first deployment.
