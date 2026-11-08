# Project Status

Last updated: 2026-11-08

- **Current phase:** Week 1 — Foundation (code complete)
- **Overall:** Bootstrap and Week 1 foundation complete; MVP in progress.

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
- MudBlazor application shell, public landing page, tasks placeholder, profile settings.
- RU/EN localization with cookie and `Accept-Language` negotiation.
- Unit tests for domain rules (32 passing).
- Testcontainers-based integration tests for auth and persistence (7 passing).

## In progress

- First CI run (pending publication of the GitHub repository).
- Week 2: projects, tags, tasks, subtasks, views, quick add.

## Next

- Week 2: projects, tags, tasks, subtasks, views, quick add.

## Blockers and risks

- Docker Desktop must be running for PostgreSQL, Mailpit, integration tests, and E2E.
- GitHub CLI is not installed; pushing to the public repository is pending.
- The production SMTP provider will be chosen before the MVP deployment.

## Quality gates

| Gate | Status |
|---|---|
| Build | ✅ 0 warnings, 0 errors |
| Unit tests | ✅ 32 passing |
| Integration tests | ✅ 7 passing (Testcontainers) |
| Component tests | ⬜ scaffold only (Week 4) |
| E2E tests | ⬜ scaffold only (Week 4) |
| CI | ⬜ workflow added, first run pending |
| Deployed | ⬜ |
