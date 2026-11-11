# ADR-0005: Testing strategy

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The project needs confidence in domain rules, EF Core mappings, Identity flows, Blazor components, and end-to-end behavior. A single test level would either be too slow or too shallow.

## Decision

Use a four-level test pyramid:

- **Unit** (xUnit + Shouldly + NSubstitute): domain rules, validation, timezone/recurrence calculations.
- **Integration** (WebApplicationFactory + Testcontainers PostgreSQL): EF Core, Application services, Identity, API, data isolation, concurrency.
- **Component** (bUnit): key Blazor components, forms, filtering, localization.
- **E2E** (Playwright .NET): real browser against the app, PostgreSQL, and Mailpit.

Shared fixtures and helpers live in `TodoList.Testing`. Reminder dispatch uses `TimeProvider` so tests are deterministic. CI runs unit/integration/component on every PR, E2E smoke on PR, and full E2E on `main` and nightly.

## Consequences

- High confidence across the stack.
- Integration and E2E tests require Docker and add CI time.
- Coverage is reported but not used as a hard gate initially; critical paths are the priority.
- Flaky tests are fixed or quarantined within one week.
