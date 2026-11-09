# Project Status

Last updated: 2026-11-09

- **Current phase:** Week 2 - Core domain (code complete)
- **Overall:** Foundation and core task management complete; MVP in progress.

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
- Command/query Application services with FluentValidation and `Result<T>`.
- Unit tests for domain rules (32 passing).
- Testcontainers-based integration tests for auth, persistence, and task management (13 passing).

## In progress

- First CI run (pending publication of the GitHub repository).
- Week 3: search and filter polish, settings, demo seed, `/api/v1`, OpenAPI.

## Next

- Week 3: search and filter polish, settings, demo seed, `/api/v1`, OpenAPI.

## Blockers and risks

- Docker Desktop must be running for PostgreSQL, Mailpit, integration tests, and E2E.
- GitHub CLI is not installed; pushing to the public repository is pending.
- The production SMTP provider will be chosen before the MVP deployment.

## Quality gates

| Gate | Status |
|---|---|
| Build | ✅ 0 warnings, 0 errors |
| Unit tests | ✅ 32 passing |
| Integration tests | ✅ 13 passing (Testcontainers) |
| Component tests | ⬜ scaffold only (Week 4) |
| E2E tests | ⬜ scaffold only (Week 4) |
| CI | ⬜ workflow added, first run pending |
| Deployed | ⬜ |
