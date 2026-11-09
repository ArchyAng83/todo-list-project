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
- Minimal REST API `/api/v1` with bearer-only authentication, custom login endpoint, and tasks, projects, tags, and settings endpoints.
- OpenAPI document and Scalar UI.
- Integration tests for bearer login, cookie-only rejection, API scoping, task views, priority sorting, and settings persistence.

### Changed

- EF Core now uses `QuerySplittingBehavior.SplitQuery` to keep task collection queries efficient.
- Checklist items and task-tag links are added to the context explicitly after being created through the `TodoTask` aggregate.
- Priority sorting uses a translatable `CASE` expression instead of a CLR helper method.
