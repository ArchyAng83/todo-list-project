# Обзор архитектуры

## Форма

TodoList — **модульный монолит**: один deployable ASP.NET Core процесс, одна база PostgreSQL, чёткие границы слоёв и feature-oriented Application-сервисы. Микросервисы сознательно не используются для проекта такого размера.

```text
TodoList.Web            -> Blazor UI, auth-страницы, minimal API
TodoList.Infrastructure -> EF Core, Identity, email, background jobs
TodoList.Application    -> use cases, DTO, валидация, абстракции
TodoList.Domain         -> сущности, enum'ы, доменные правила
```

`TodoList.Web` — composition root. `TodoList.Domain` не зависит от инфраструктуры. `ApplicationUser` живёт в Infrastructure, чтобы домен не знал про ASP.NET Core Identity.

## Доменная модель

| Сущность | Ответственность |
|---|---|
| `ApplicationUser` | Identity-пользователь + display name, culture, timezone, theme, week start, default reminder time |
| `Project` | Необязательная группировка задач; можно архивировать |
| `Tag` | Пользовательский тег с цветом |
| `TodoTask` | Title, notes, status, priority, due date/time, reminder offset, recurrence, sort order |
| `ChecklistItem` | Подзадача внутри задачи |
| `Reminder` | Database-backed outbox для email-напоминания |
| `TaskTag` | Many-to-many join |

У каждого aggregate root есть `UserId`; дочерние сущности проверяются через родителя. Доступ к чужим данным возвращает **404**, а не 403, чтобы не раскрывать существование. ID — `Guid` v7. Конкурентность — PostgreSQL `xmin`.

## Модель времени

- Дедлайны — **floating local values**: `DateOnly` + опциональный `TimeOnly`.
- Моменты времени (created, completed, reminder) — **UTC `DateTimeOffset`**.
- Обёртка над `TimeZoneInfo` конвертирует локальные дедлайны в UTC для напоминаний и обрабатывает DST.
- `TimeProvider` внедряется везде, чтобы тесты были детерминированными.

## Auth и API

- Blazor использует **cookie-аутентификацию** через ASP.NET Core Identity.
- Auth-страницы — **static SSR**, чтобы cookie ставилась в HTTP-ответе, а не внутри SignalR-circuit.
- `/api/v1` использует **только bearer-токены** и переиспользует Application-сервисы. Blazor не вызывает API — это убирает CSRF и смешивание cookie/bearer.
- `POST /api/v1/auth/login` выдаёт access- и refresh-токены, а `POST /api/v1/auth/refresh` их обновляет; API покрывает задачи, проекты, теги и `/me/settings`.
- API документируется через OpenAPI по `/openapi/v1.json` и Scalar по `/scalar`.
- Настройки пользователя (язык, тема, часовой пояс, начало недели, напоминание по умолчанию) хранятся в `ApplicationUser` и доступны через `IUserSettingsStore`, поэтому Identity не попадает в Application.

## Фоновая работа

Напоминания пишутся в таблицу `Reminder` Application-сервисами. Hosted dispatcher забирает due-строки через `FOR UPDATE SKIP LOCKED`, отправляет email через MailKit и помечает отправленными. Повторяющиеся задачи создают следующее вхождение при завершении текущей.

## Тестирование

- Unit-тесты: домен и чистая логика Application.
- Integration-тесты: `WebApplicationFactory` + Testcontainers PostgreSQL.
- Component-тесты: bUnit.
- E2E-тесты: Playwright против реального приложения, PostgreSQL и Mailpit.
- Общие test utilities — в `TodoList.Testing`.

## Решения

- [ADR-0001: Blazor Interactive Server и static SSR auth](decisions/0001-blazor-interactive-server.ru.md)
- [ADR-0002: PostgreSQL, EF Core и IAppDbContext](decisions/0002-postgresql-ef-core.ru.md)
- [ADR-0003: Identity cookie и bearer-only API](decisions/0003-identity-cookie-and-bearer.ru.md)
- [ADR-0004: Модульный монолит без MediatR/AutoMapper](decisions/0004-modular-monolith.ru.md)
- [ADR-0005: Стратегия тестирования](decisions/0005-testing-strategy.ru.md)
