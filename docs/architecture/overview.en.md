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
- `POST /api/v1/auth/login` issues access and refresh tokens, and `POST /api/v1/auth/refresh` rotates them; the API covers tasks (including board ordering), projects, tags, and `/me/settings`.
- The API is documented with OpenAPI at `/openapi/v1.json` and exposed through Scalar at `/scalar`.
- Login and registration POSTs are throttled per client IP with a fixed window (`Auth:RateLimit`, 10 requests per 60 seconds by default) and return `429` with `Retry-After`; Identity lockout stays the per-account protection.
- User settings (language, theme, timezone, week start, default reminder) live on `ApplicationUser` and are accessed through `IUserSettingsStore`, keeping Identity out of Application.

## Task ordering and Kanban

- `TodoTask.SortOrder` is a per-column manual order, not a global one. `TaskQueries.BoardAsync` orders by `SortOrder` then `CreatedAt`, so ties and legacy rows still render deterministically.
- `TaskService.MoveAsync` loads the source and target columns, renumbers only those two, and saves once; the other columns are not touched.
- The board groups tasks into `Todo`, `InProgress`, and `Done` columns and deliberately ignores the list filters, because manual ordering only makes sense over the full column.
- `TaskListItem` is shared by the list and the board, so `/api/v1` and the task page keep one contract.

## Blazor and DI scopes

- Blazor Server keeps one service scope per circuit, and the layout and page components load concurrently.
- Components resolve Application services through `IServiceScopeFactory`, and `CurrentUserProvider` resolves `UserManager` in a per-call scope, so two components never issue overlapping operations on one `DbContext`.

## Operations

- `/health/live` checks the process; `/health/ready` checks PostgreSQL. Liveness deliberately ignores the database.
- Outside Development the app writes single-line JSON to stdout with scopes and UTC timestamps.
- The container image publishes in Release, runs as a non-root user, and listens on port 8080.
- Production applies migrations as a separate deploy step; the demo seeder only runs in Development.

## Background work

Reminders are written to the `Reminder` table by Application services. A hosted dispatcher claims due rows with `FOR UPDATE SKIP LOCKED`, sends email through MailKit, and marks them sent. Recurring tasks generate their next occurrence when the current one is completed.

## Testing

- Unit tests: domain and pure Application logic.
- Integration tests: `WebApplicationFactory` + Testcontainers PostgreSQL.
- Component tests: bUnit against presentational components with explicit `EventCallback` parameters.
- E2E tests: Playwright against the real app process, Testcontainers PostgreSQL, and headless Chromium.
- Shared test utilities live in `TodoList.Testing`.

## Decisions

- [ADR-0001: Blazor Interactive Server and static SSR auth](decisions/0001-blazor-interactive-server.en.md)
- [ADR-0002: PostgreSQL, EF Core, and IAppDbContext](decisions/0002-postgresql-ef-core.en.md)
- [ADR-0003: Identity cookie and bearer-only API](decisions/0003-identity-cookie-and-bearer.en.md)
- [ADR-0004: Modular monolith without MediatR/AutoMapper](decisions/0004-modular-monolith.en.md)
- [ADR-0005: Testing strategy](decisions/0005-testing-strategy.en.md)
