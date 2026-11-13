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
- `POST /api/v1/auth/login` выдаёт access- и refresh-токены, а `POST /api/v1/auth/refresh` их обновляет; API покрывает задачи (включая порядок на доске), проекты, теги и `/me/settings`.
- API документируется через OpenAPI по `/openapi/v1.json` и Scalar по `/scalar`.
- POST-запросы входа и регистрации ограничены по IP клиента фиксированным окном (`Auth:RateLimit`, по умолчанию 10 запросов за 60 секунд) и возвращают `429` с `Retry-After`; lockout Identity остаётся защитой на уровне аккаунта.
- Настройки пользователя (язык, тема, часовой пояс, начало недели, напоминание по умолчанию) хранятся в `ApplicationUser` и доступны через `IUserSettingsStore`, поэтому Identity не попадает в Application.

## Порядок задач и Kanban

- `TodoTask.SortOrder` — ручной порядок внутри колонки, а не глобальный. `TaskQueries.BoardAsync` сортирует по `SortOrder`, затем по `CreatedAt`, поэтому равные значения и старые строки всё равно отрисовываются детерминированно.
- `TaskService.MoveAsync` загружает исходную и целевую колонки, перенумеровывает только их и сохраняет один раз; остальные колонки не затрагиваются.
- Доска группирует задачи в колонки `Todo`, `InProgress` и `Done` и осознанно игнорирует фильтры списка: ручная сортировка имеет смысл только на всей колонке.
- `TaskListItem` общий для списка и доски, поэтому `/api/v1` и страница задач сохраняют один контракт.

## Blazor и DI-scope

- Blazor Server держит один DI-scope на circuit, а layout и страница загружаются одновременно.
- Компоненты получают сервисы Application через `IServiceScopeFactory`, а `CurrentUserProvider` резолвит `UserManager` в отдельном scope на вызов, поэтому два компонента никогда не выполняют пересекающиеся операции на одном `DbContext`.

## Эксплуатация

- `/health/live` проверяет процесс; `/health/ready` проверяет PostgreSQL. Liveness намеренно не зависит от базы.
- Вне Development приложение пишет однострочный JSON в stdout, включая scopes и UTC-таймстампы.
- Контейнерный образ собирается в Release, работает от непривилегированного пользователя и слушает порт 8080.
- В production миграции применяются отдельным шагом деплоя; демо-сидер работает только в Development.

## Фоновая работа

Напоминания пишутся в таблицу `Reminder` Application-сервисами. Hosted dispatcher опрашивает due-строки, отправляет email через `System.Net.Mail.SmtpClient` (или logging-sender, если `Email:Host` пуст) и помечает их отправленными. MVP работает в одной реплике, потому что доставка не использует межрепличный claim; повторяющиеся задачи создают следующее вхождение при завершении текущей.

## Тестирование

- Unit-тесты: домен и чистая логика Application.
- Integration-тесты: `WebApplicationFactory` + Testcontainers PostgreSQL.
- Component-тесты: bUnit против презентационных компонентов с явными `EventCallback`.
- E2E-тесты: Playwright против реального процесса приложения, PostgreSQL в Testcontainers и headless Chromium.
- Общие test utilities — в `TodoList.Testing`.

## Решения

- [ADR-0001: Blazor Interactive Server и static SSR auth](decisions/0001-blazor-interactive-server.ru.md)
- [ADR-0002: PostgreSQL, EF Core и IAppDbContext](decisions/0002-postgresql-ef-core.ru.md)
- [ADR-0003: Identity cookie и bearer-only API](decisions/0003-identity-cookie-and-bearer.ru.md)
- [ADR-0004: Модульный монолит без MediatR/AutoMapper](decisions/0004-modular-monolith.ru.md)
- [ADR-0005: Стратегия тестирования](decisions/0005-testing-strategy.ru.md)
- [ADR-0006: Форма production-деплоя](decisions/0006-production-deployment.ru.md)
- [ADR-0007: Claim для отправки напоминаний](decisions/0007-reminder-dispatch-claim.ru.md)
