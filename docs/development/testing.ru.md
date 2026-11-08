# Стратегия тестирования

## Пирамида

| Уровень | Инструменты | Что покрывает | Когда |
|---|---|---|---|
| Unit | xUnit, Shouldly, NSubstitute | Доменные правила, валидация, timezone и recurrence расчёты, чистая логика Application | Каждый коммит |
| Integration | WebApplicationFactory, Testcontainers PostgreSQL, Respawn | EF Core mappings/migrations, Application-сервисы, Identity, API, изоляция данных, конкурентность | Каждый PR |
| Component | bUnit | Ключевые Blazor-компоненты, формы, фильтрация, локализация | Каждый PR |
| E2E | Playwright .NET | Auth, CRUD, фильтры, RU/EN, Kanban, календарь, recurring, напоминания через Mailpit, import/export | Smoke на PR; полный на main/nightly |

## Команды

```bash
dotnet test
dotnet test tests/TodoList.UnitTests
dotnet test tests/TodoList.IntegrationTests
dotnet test tests/TodoList.ComponentTests
dotnet test tests/TodoList.E2ETests
```

Integration и E2E тесты требуют Docker. Браузеры Playwright ставятся один раз через Playwright CLI.

## Test data и fakes

- Builders вроде `TaskBuilder` и `UserBuilder` живут в `TodoList.Testing`.
- `TimeProvider` заменяется fake'ом в time-sensitive тестах.
- Отправка email использует fake `IEmailSender` в integration-тестах; E2E проверяет письма через Mailpit API.
- Respawn очищает базу между integration-тестами.

## CI

- PR: build + unit + integration + component + E2E smoke.
- `main`: полный E2E.
- Nightly: полный E2E и coverage artifact.

## Coverage и flakiness

Coverage публикуется, но не является жёстким gate на старте. Цель — 70%+ для Domain и Application, с акцентом на критические пути. Flaky-тест чинится или уходит в quarantine с issue в течение недели.
