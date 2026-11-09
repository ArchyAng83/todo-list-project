# Week 4 — Testing, containers, and observability

Dates: 2026-11-23 — 2026-11-29

Status: Code complete; container image built and E2E suite green

## Goals

- Component tests with bUnit for the task list, layout, and culture switcher.
- Playwright E2E journeys against a real Kestrel process and PostgreSQL.
- Reproducible container image for the web app.
- Health checks and structured logging suitable for a container platform.
- CI that runs unit, integration, component, and E2E tests plus the image build.

## Done

- `TaskList` presentational component extracted from the tasks page with loading, empty, error, retry, and callback states.
- 8 bUnit tests for `TaskList` and 3 each for `MainLayout` and `CultureSwitcher` (14 total).
- `ICurrentUserProvider` seam so components resolve `UserContext` without the Identity stack in tests.
- Playwright E2E project with a fixture that starts a PostgreSQL container, the web app on a free port, and headless Chromium.
- 6 E2E journeys: anonymous login entry, demo sign-in, quick add, search, Today view, and RU culture switch.
- `/health/live` (process) and `/health/ready` (database) endpoints.
- JSON console logging outside Development, including scopes and UTC timestamps.
- Multi-stage `Dockerfile` that publishes in Release, runs as a non-root user, and listens on port 8080; `.dockerignore` keeps build output and tests out of the image.
- CI runs component and E2E tests and builds the container image.

## Fixed after E2E

- Razor treats `string` component parameters written as `Error="_error"` as literal text; the task list was permanently in its error state. It now uses `Error="@_error"`.
- Blazor Server keeps one service scope per circuit, so the layout and page could query PostgreSQL through the same `DbContext` at the same time. `CurrentUserProvider` now resolves `UserManager` in a per-call scope, and pages already resolve Application services through `IServiceScopeFactory`.
- E2E interactions wait until Blazor has attached event handlers (the `_bl_*` marker) before clicking, so a click cannot fall through to a native form submit.

## Decisions

- Component tests target presentational components with explicit `EventCallback` parameters instead of pages that need a database or a circuit.
- E2E runs a real app process instead of `WebApplicationFactory`: it exercises static SSR, the SignalR circuit, cookies, and static assets the way a browser sees them.
- The readiness probe checks PostgreSQL only; liveness stays database-independent so a slow database does not restart a healthy process.
- The container image uses the built-in JSON console logger instead of adding a logging package; a collector can consume stdout.
- Migrations stay a separate deploy step in production; the image does not apply them at startup.

## Metrics

- Build: passing with 0 warnings and 0 errors in Debug and Release.
- Tests: 35 unit, 24 integration, 14 component, and 6 E2E tests passing (79 total).
- Container: image builds locally from the multi-stage `Dockerfile`.
- Smoke checks: `/health/live` and `/health/ready` return 200, the login flow works, and the demo data renders.

## Next

- Week 5: Kanban board with status columns, drag-and-drop ordering, and tests.
- Publish the repository and confirm the first GitHub Actions run.
- Pick a hosting target and a production SMTP provider before the MVP deployment.
