# Project Status

Last updated: 2026-11-09

- **Current phase:** Week 3 - UX polish and REST API (code complete)
- **Overall:** Foundation, task management, settings, demo data, and the bearer API are complete; MVP in progress.

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
- Testcontainers-based integration tests for auth, persistence, task management, and the REST API (22 passing).

## In progress

- First CI run (pending publication of the GitHub repository).

## Next

- Week 4: bUnit component tests, Playwright E2E, Dockerfile, observability, and the first deployment.

## Blockers and risks

- Docker Desktop must be running for PostgreSQL, Mailpit, integration tests, and E2E.
- GitHub CLI is not installed; pushing to the public repository is pending.
- The production SMTP provider will be chosen before the MVP deployment.

## Quality gates

| Gate | Status |
|---|---|
| Build | ✅ 0 warnings, 0 errors |
| Unit tests | ✅ 35 passing |
| Integration tests | ✅ 22 passing (Testcontainers) |
| Component tests | ⬜ scaffold only (Week 4) |
| E2E tests | ⬜ scaffold only (Week 4) |
| CI | ⬜ workflow added, first run pending |
| Deployed | ⬜ |
