# Week 1 — Foundation

Dates: 2026-11-08 — 2026-11-14

Status: In progress

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

## Decisions

- Blazor Web App, Interactive Server for app pages; static SSR auth pages.
- PostgreSQL + EF Core + `IAppDbContext`; no generic repositories.
- Identity cookie for Blazor; bearer-only API.
- Modular monolith; no MediatR or AutoMapper.
- Testing pyramid: unit, integration, component, E2E.

## Metrics

- Build: passing.
- Tests: scaffold only.
- CI: workflow added; first run pending.

## Next

- Week 2: projects, tags, tasks, subtasks, views, quick add.
