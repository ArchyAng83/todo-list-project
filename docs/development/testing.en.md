# Testing Strategy

## Pyramid

| Level | Tools | What it covers | When |
|---|---|---|---|
| Unit | xUnit, Shouldly, NSubstitute | Domain rules, validation, timezone calculations, pure Application logic | Every commit |
| Integration | WebApplicationFactory, Testcontainers PostgreSQL | EF Core mappings/migrations, Application services, Identity, API, data isolation, concurrency | Every PR |
| Component | bUnit, NSubstitute | Presentational Blazor components: loading, empty, error, callbacks, localization, layout | Every PR |
| E2E | Playwright .NET | Static SSR auth, the SignalR circuit, quick add, search, views, board drag-and-drop, RU/EN, real cookies and assets | Every PR |

## Commands

```bash
dotnet test                                       # every suite
dotnet test TodoList.sln -c Release               # same as CI
dotnet test tests/TodoList.UnitTests
dotnet test tests/TodoList.IntegrationTests
dotnet test tests/TodoList.ComponentTests
dotnet test tests/TodoList.E2ETests
```

Integration and E2E tests require Docker. Install the Playwright browser once after building:

```bash
pwsh tests/TodoList.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

On Linux CI add `--with-deps` so the Chromium system libraries are installed too.

## How the suites run

- Unit tests need no infrastructure.
- Integration tests start one PostgreSQL container per collection, and every test in the collection uses that one database.
- Component tests render components in isolation with localization and MudBlazor services; they do not touch a database.
- E2E starts a PostgreSQL container, runs the real web app on a free port, waits for `/health/ready`, and drives headless Chromium.
- Each E2E journey registers its own user and seeds the tasks it asserts on through `/api/v1`, so journeys never share state and do not depend on execution order.
- E2E waits until Blazor has attached event handlers before clicking, so interactions do not fall through to native form submits.
- Board cards rendered after enhanced navigation do not carry the `_bl_*` marker, so board tests wait for the card to be visible instead.
- E2E contexts use a 1280x800 viewport: the default viewport stacks the Kanban columns, which makes cross-column drag-and-drop unreliable.

## Integration test database

`IntegrationCollection` owns a single `TodoListAppFixture`, which starts one PostgreSQL container, applies migrations once, and hands the same connection string to every test in the collection. The database is not reset between tests, and tests run sequentially inside the collection, so the suite pays for schema creation once instead of once per test.

Isolation comes from the data, not from the schema:

- Every test creates its own user, and assertions read only rows it created. Emails use a `Guid` suffix, so a name collision cannot happen.
- Cross-test order dependence is treated as a bug. A test that needs a globally empty table clears it explicitly — reminder delivery tests cancel leftover pending reminders before dispatching.
- Container startup dominates the runtime, so keeping one database is what makes the suite finish in well under a minute.

Startup cost is fine at the current size. Revisit this decision when any of the following becomes true:

- One test needs a database-wide invariant, such as an exact row count or a sequence value.
- A test has to clean up data it does not own, or starts depending on execution order.
- Integration tests run in parallel, or the collection grows enough that one database becomes a bottleneck.
- A failure leaves the shared database in a state that breaks later tests.

The remedies, in increasing cost, are a transaction rollback per test, a schema per test, or a Respawn-style reset. Any of them should land with an ADR update rather than ad hoc cleanup code.

## Test data and fakes

- Shared fixtures live in `TodoList.Testing`; `[CollectionDefinition]` types live in the test assemblies that use them.
- Application unit tests use small hand-written fakes, such as an in-memory `IUserSettingsStore`.
- `TimeProvider` is injected everywhere, and reminder delivery tests pass a `FakeTimeProvider` so back-off timing is exact instead of dependent on wall time.
- Email sending uses fake `IEmailSender` implementations in integration tests.

## CI

- Every push and pull request runs build, unit, integration, component, and E2E tests.
- CI installs the Playwright Chromium browser before running the suite.
- A second job builds the container image from the `Dockerfile`.
- Test results are uploaded as a TRX artifact; nightly runs are not configured yet.

## Coverage and flakiness

Coverage is collected locally with `coverlet.collector` (`dotnet test --collect:"XPlat Code Coverage"`); CI does not run it and it is not a gate. The target is 70%+ for Domain and Application, with emphasis on critical paths. A flaky test is fixed or quarantined with an issue within one week.
