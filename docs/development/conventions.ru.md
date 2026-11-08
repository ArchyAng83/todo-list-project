# Соглашения по коду

## C#

- Nullable reference types включены; warnings считаются ошибками.
- File-scoped namespaces, один публичный тип на файл.
- Везде `async`/`await`; суффикс `Async` у асинхронных методов.
- Принимать и пробрасывать `CancellationToken` в Application, Infrastructure и Web.
- Явные типы, когда это улучшает читаемость; `var` для очевидных присваиваний.
- Никаких `.Result` и `.Wait()`.

## Domain и Application

- Доменные методы защищают инварианты; избегаем публичных setters для изменяемого состояния.
- Application-сервисы делятся на команды (`TaskService`) и запросы (`TaskQueries`).
- `Result<T>` для ожидаемых ошибок; исключения — для неожиданных.
- `UserContext` передаётся явно; никогда не читаем текущего пользователя из static/ambient context.
- Один `SaveChangesAsync` на use case; явные транзакции только для многошаговых операций.

## EF Core

- `AsNoTracking` для read-запросов и projections в DTO.
- `IEntityTypeConfiguration<T>` классы — в Infrastructure.
- Фильтр по `UserId` в каждом user-scoped запросе; доступ к чужому → 404.
- `Guid.CreateVersion7()` для ID новых агрегатов.
- PostgreSQL `xmin` для optimistic concurrency.
- Миграции ревьюятся и не редактируются после применения.

## Blazor

- Страницы приложения — Interactive Server; auth-страницы — static SSR.
- В компонентах использовать `IDbContextFactory`, а не долгоживущий scoped `DbContext`.
- Формы используют `EditForm` и те же FluentValidation validators, что и API.
- Для каждой data-driven страницы показывать loading, empty и error states.
- Компоненты держать небольшими; переиспользуемый UI — в `Components/`.

## Тесты

- Имена тестов: `Method_State_ExpectedBehavior`.
- Builders и fakes вместо больших setup-блоков.
- Integration-тесты используют реальный PostgreSQL, не in-memory provider.
- Никаких сетевых вызовов в unit-тестах.

## Язык

- Код, идентификаторы, XML docs, commit messages и английские docs — на английском.
- UI и русские docs — двуязычные.
