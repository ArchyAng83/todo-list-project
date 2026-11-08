# Project Status

Last updated: 2026-11-08

- **Current phase:** Week 1 — Foundation
- **Overall:** Bootstrap complete; MVP in progress.

## Done

- Architecture, scope, and 8-week roadmap agreed.
- Solution with Domain, Application, Infrastructure, Web, and five test projects.
- Central package management, pinned .NET SDK, editor config, Git attributes.
- Docker Compose for PostgreSQL and Mailpit.
- GitHub Actions CI skeleton.
- Bilingual documentation foundation and progress tracking.
- Initial repository commit on `main`.

## In progress

- Week 1: EF Core + PostgreSQL, Identity, auth pages, MudBlazor shell, i18n foundation.

## Next

- Week 2: projects, tags, tasks, subtasks, views, quick add.

## Blockers and risks

- Docker Desktop must be running for PostgreSQL, Mailpit, integration tests, and E2E.
- GitHub CLI is not installed; pushing to the public repository is pending.
- The production SMTP provider will be chosen before the MVP deployment.

## Quality gates

| Gate | Status |
|---|---|
| Build | ✅ |
| Unit tests | ⬜ scaffold only |
| Integration tests | ⬜ scaffold only |
| Component tests | ⬜ scaffold only |
| E2E tests | ⬜ scaffold only |
| CI | ⬜ workflow added, first run pending |
| Deployed | ⬜ |
