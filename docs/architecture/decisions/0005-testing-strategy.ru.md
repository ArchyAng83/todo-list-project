# ADR-0005: Стратегия тестирования

- **Статус:** принято
- **Дата:** 2026-11-08

## Контекст

Проекту нужна уверенность в доменных правилах, EF Core mappings, Identity-флоу, Blazor-компонентах и end-to-end поведении. Один уровень тестов был бы либо слишком медленным, либо слишком поверхностным.

## Решение

Используем четырёхуровневую пирамиду тестов:

- **Unit** (xUnit + Shouldly + NSubstitute): доменные правила, валидация, timezone/recurrence расчёты.
- **Integration** (WebApplicationFactory + Testcontainers PostgreSQL): EF Core, Application-сервисы, Identity, API, изоляция данных, конкурентность.
- **Component** (bUnit): ключевые Blazor-компоненты, формы, фильтрация, локализация.
- **E2E** (Playwright .NET): реальный браузер против приложения, PostgreSQL и Mailpit.

Общие fixtures и helpers живут в `TodoList.Testing`. Reminder dispatch использует `TimeProvider`, чтобы тесты были детерминированными. CI запускает unit/integration/component на каждом PR, E2E smoke на PR и полный E2E на `main` и nightly.

## Последствия

- Высокая уверенность по всему стеку.
- Integration и E2E тесты требуют Docker и увеличивают время CI.
- Coverage собирается, но не используется как жёсткий gate на старте; приоритет — критические пути.
- Flaky-тесты чинятся или уходят в quarantine в течение недели.
