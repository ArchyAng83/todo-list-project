# Testing Strategy

## Pyramid

| Level | Tools | What it covers | When |
|---|---|---|---|
| Unit | xUnit, Shouldly, NSubstitute | Domain rules, validation, timezone and recurrence calculations, pure Application logic | Every commit |
| Integration | WebApplicationFactory, Testcontainers PostgreSQL, Respawn | EF Core mappings/migrations, Application services, Identity, API, data isolation, concurrency | Every PR |
| Component | bUnit | Key Blazor components, forms, filtering, localization | Every PR |
| E2E | Playwright .NET | Auth, CRUD, filters, RU/EN, Kanban, calendar, recurring tasks, reminders via Mailpit, import/export | Smoke on PR; full on main/nightly |

## Commands

```bash
dotnet test
dotnet test tests/TodoList.UnitTests
dotnet test tests/TodoList.IntegrationTests
dotnet test tests/TodoList.ComponentTests
dotnet test tests/TodoList.E2ETests
```

Integration and E2E tests require Docker. Playwright browsers are installed once with the Playwright CLI.

## Test data and fakes

- Builders such as `TaskBuilder` and `UserBuilder` live in `TodoList.Testing`.
- `TimeProvider` is replaced with a fake in time-sensitive tests.
- Email sending uses a fake `IEmailSender` in integration tests; E2E asserts messages through the Mailpit API.
- Respawn resets the database between integration tests.

## CI

- PR: build + unit + integration + component + E2E smoke.
- `main`: full E2E.
- Nightly: full E2E and coverage artifact.

## Coverage and flakiness

Coverage is reported, not enforced as a hard gate initially. The target is 70%+ for Domain and Application, with emphasis on critical paths. A flaky test is fixed or quarantined with an issue within one week.
