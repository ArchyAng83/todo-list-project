# Стратегия тестирования

## Пирамида

| Уровень | Инструменты | Что покрывает | Когда |
|---|---|---|---|
| Unit | xUnit, Shouldly, NSubstitute | Доменные правила, валидация, расчёты часовых поясов, чистая логика Application | Каждый коммит |
| Integration | WebApplicationFactory, Testcontainers PostgreSQL | EF Core mappings/migrations, Application-сервисы, Identity, API, изоляция данных, конкурентность | Каждый PR |
| Component | bUnit, NSubstitute | Презентационные Blazor-компоненты: загрузка, пустота, ошибка, колбэки, локализация, layout | Каждый PR |
| E2E | Playwright .NET | Static SSR вход, SignalR-circuit, быстрый ввод, поиск, представления, drag-and-drop на доске, RU/EN, реальные cookie и статические файлы | Каждый PR |

## Команды

```bash
dotnet test                                       # все наборы
dotnet test TodoList.sln -c Release               # как в CI
dotnet test tests/TodoList.UnitTests
dotnet test tests/TodoList.IntegrationTests
dotnet test tests/TodoList.ComponentTests
dotnet test tests/TodoList.E2ETests
```

Integration- и E2E-тестам нужен Docker. Браузер Playwright ставится один раз после сборки:

```bash
pwsh tests/TodoList.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

В Linux CI добавьте `--with-deps`, чтобы вместе с Chromium установились системные библиотеки.

## Как запускаются наборы

- Unit-тесты не требуют инфраструктуры.
- Integration-тесты поднимают один контейнер PostgreSQL на коллекцию и создают изолированную базу на каждую фабрику.
- Component-тесты рендерят компоненты изолированно с сервисами локализации и MudBlazor; база данных им не нужна.
- E2E поднимает контейнер PostgreSQL, запускает реальное веб-приложение на свободном порту, ждёт `/health/ready` и управляет headless Chromium.
- E2E ждёт, пока Blazor навесит обработчики событий, прежде чем кликать, чтобы клик не уходил в нативную отправку формы. Карточки доски, отрисованные после enhanced navigation, маркер `_bl_*` не несут, поэтому тесты доски ждут видимости карточки.
- E2E-контексты используют viewport 1280x800: при стандартном viewport колонки Kanban складываются в столбец, и drag-and-drop между колонками становится нестабильным.

## Test data и fakes

- Общие фикстуры живут в `TodoList.Testing`; типы `[CollectionDefinition]` — в тестовых сборках, которые их используют.
- Unit-тесты Application используют небольшие ручные fake'и, например in-memory `IUserSettingsStore`.
- `TimeProvider` внедряется везде, чтобы time-sensitive функции можно было тестировать детерминированно.
- Отправка email будет использовать fake `IEmailSender` в integration-тестах, когда появятся напоминания.

## CI

- Каждый push и pull request запускает build, unit-, integration-, component- и E2E-тесты.
- CI ставит браузер Chromium для Playwright перед запуском набора.
- Вторая job собирает контейнерный образ из `Dockerfile`.
- Nightly-запуски и артефакты coverage появятся после публикации репозитория.

## Coverage и flakiness

Coverage публикуется, но не является жёстким gate на старте. Цель — 70%+ для Domain и Application, с акцентом на критические пути. Flaky-тест чинится или уходит в quarantine с issue в течение недели.
