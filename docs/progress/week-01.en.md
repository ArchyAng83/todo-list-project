# Week 1 — Foundation

Dates: 2026-11-08 — 2026-11-14

Status: Code complete; first CI run pending

## Goals

- EF Core + PostgreSQL + initial migration.
- ASP.NET Core Identity and auth pages.
- MudBlazor app shell, light/dark theme.
- RU/EN localization foundation.
- Test infrastructure: Testcontainers, bUnit, Playwright.
- First CI run.

## Done

- Bootstrap commit: solution, test projects, docs, Docker Compose, CI skeleton.
- Architecture decisions recorded in ADR-0001…0005.
- Domain model with projects, tags, tasks, checklist items, task tags, and reminders.
- `AppDbContext`, EF Core configurations, snake_case naming, and the `InitialCreate` migration applied to PostgreSQL 17.
- ASP.NET Core Identity with cookie authentication, renamed Identity tables, and password/lockout policies.
- Static SSR auth pages (`login`, `register`, `logout`, `access-denied`) with antiforgery protection.
- MudBlazor shell with auth-aware navigation, public landing page, tasks placeholder, and profile settings.
- RU/EN localization with a culture cookie, `Accept-Language` fallback, and a language switcher.
- Unit tests for domain invariants.
- Testcontainers-based integration tests covering registration, login redirects, migrations, schema, and EF round-trips.
- Manual browser verification of register, logout, login, protected redirects, return URLs, and RU/EN switching.

## Decisions

- Blazor Web App, Interactive Server for app pages; static SSR auth pages.
- PostgreSQL + EF Core + `IAppDbContext`; no generic repositories.
- Identity cookie for Blazor; bearer-only API.
- Modular monolith; no MediatR or AutoMapper.
- Testing pyramid: unit, integration, component, E2E.
- PostgreSQL is published on host port 5433 to avoid the locally installed PostgreSQL on 5432.
- bUnit and Playwright are wired in during Week 4 with the interactive task views.

## Metrics

- Build: passing with 0 warnings and 0 errors.
- Tests: 32 unit tests and 7 integration tests passing.
- CI: workflow added; first run pending repository publication.

## Next

- Week 2: projects, tags, tasks, subtasks, views, quick add.
