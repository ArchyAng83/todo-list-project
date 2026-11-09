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
- Integration tests start one PostgreSQL container per collection and create an isolated database per factory.
- Component tests render components in isolation with localization and MudBlazor services; they do not touch a database.
- E2E starts a PostgreSQL container, runs the real web app on a free port, waits for `/health/ready`, and drives headless Chromium.
- E2E waits until Blazor has attached event handlers before clicking, so interactions do not fall through to native form submits.
- Board cards rendered after enhanced navigation do not carry the `_bl_*` marker, so board tests wait for the card to be visible instead.
- E2E contexts use a 1280x800 viewport: the default viewport stacks the Kanban columns, which makes cross-column drag-and-drop unreliable.

## Test data and fakes

- Shared fixtures live in `TodoList.Testing`; `[CollectionDefinition]` types live in the test assemblies that use them.
- Application unit tests use small hand-written fakes, such as an in-memory `IUserSettingsStore`.
- `TimeProvider` is injected everywhere so time-sensitive features can be tested deterministically.
- Email sending will use a fake `IEmailSender` in integration tests once reminders ship.

## CI

- Every push and pull request runs build, unit, integration, component, and E2E tests.
- CI installs the Playwright Chromium browser before running the suite.
- A second job builds the container image from the `Dockerfile`.
- Nightly runs and coverage artifacts are added when the repository is published.

## Coverage and flakiness

Coverage is reported, not enforced as a hard gate initially. The target is 70%+ for Domain and Application, with emphasis on critical paths. A flaky test is fixed or quarantined with an issue within one week.
