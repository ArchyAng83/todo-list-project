# Architecture Overview

## Shape

TodoList is a **modular monolith**: one deployable ASP.NET Core process, one PostgreSQL database, clear layer boundaries, and feature-oriented Application services. Microservices are intentionally avoided for a project of this size.

```text
TodoList.Web            -> Blazor UI, auth pages, minimal API
TodoList.Infrastructure -> EF Core, Identity, email, background jobs
TodoList.Application    -> use cases, DTOs, validation, abstractions
TodoList.Domain         -> entities, enums, domain rules
```

`TodoList.Web` is the composition root. `TodoList.Domain` has no infrastructure dependencies. `ApplicationUser` lives in Infrastructure so the domain stays free of ASP.NET Core Identity.

## Domain model

| Entity | Responsibility |
|---|---|
| `ApplicationUser` | Identity user plus display name, culture, timezone, theme, week start, default reminder time |
| `Project` | Optional grouping for tasks; can be archived |
| `Tag` | User-scoped label with color |
| `TodoTask` | Title, notes, status, priority, due date/time, reminder offset, recurrence, sort order |
| `ChecklistItem` | Subtask inside a task |
| `Reminder` | Database-backed outbox row for an email reminder |
| `TaskTag` | Many-to-many join |

Every aggregate root carries `UserId`; child entities are verified through their parent. Cross-user access returns **404**, not 403, to avoid leaking existence. IDs are `Guid` v7. Concurrency uses PostgreSQL `xmin`.

## Time model

- Due dates are **floating local values**: `DateOnly` + optional `TimeOnly`.
- Instants such as created, completed, and reminder times are **UTC `DateTimeOffset`**.
- A wrapper over `TimeZoneInfo` converts local due values to UTC for reminders and handles DST edge cases.
- `TimeProvider` is injected everywhere so tests are deterministic.

## Auth and API

- Blazor uses **cookie authentication** via ASP.NET Core Identity.
- Auth pages are **static SSR** so cookies are set during an HTTP response, not inside a SignalR circuit.
- `/api/v1` uses **bearer tokens only** and reuses Application services. Blazor does not call the API, which avoids CSRF and cookie/bearer mixing.
- The API is documented with OpenAPI and exposed through Scalar.

## Background work

Reminders are written to the `Reminder` table by Application services. A hosted dispatcher claims due rows with `FOR UPDATE SKIP LOCKED`, sends email through MailKit, and marks them sent. Recurring tasks generate their next occurrence when the current one is completed.

## Testing

- Unit tests: domain and pure Application logic.
- Integration tests: `WebApplicationFactory` + Testcontainers PostgreSQL.
- Component tests: bUnit.
- E2E tests: Playwright against a real app, PostgreSQL, and Mailpit.
- Shared test utilities live in `TodoList.Testing`.

## Decisions

- [ADR-0001: Blazor Interactive Server and static SSR auth](decisions/0001-blazor-interactive-server.en.md)
- [ADR-0002: PostgreSQL, EF Core, and IAppDbContext](decisions/0002-postgresql-ef-core.en.md)
- [ADR-0003: Identity cookie and bearer-only API](decisions/0003-identity-cookie-and-bearer.en.md)
- [ADR-0004: Modular monolith without MediatR/AutoMapper](decisions/0004-modular-monolith.en.md)
- [ADR-0005: Testing strategy](decisions/0005-testing-strategy.en.md)
