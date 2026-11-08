# Статус проекта

Обновлено: 2026-11-08

- **Текущая фаза:** неделя 1 — фундамент (код готов)
- **В целом:** bootstrap и фундамент недели 1 завершены; MVP в работе.

## Сделано

- Согласованы архитектура, scope и 8-недельный roadmap.
- Решение с Domain, Application, Infrastructure, Web и пятью тестовыми проектами.
- Central package management, зафиксированный .NET SDK, editor config, Git attributes.
- Docker Compose для PostgreSQL и Mailpit.
- CI-скелет GitHub Actions.
- Двуязычная документация и отслеживание прогресса.
- Первый коммит в ветке `main`.
- Доменная модель: проекты, теги, задачи, чек-листы, связи задач с тегами, напоминания.
- Персистентность на EF Core 10 + Npgsql, snake_case, применена первая миграция.
- ASP.NET Core Identity с cookie-аутентификацией и переименованными таблицами Identity.
- Static SSR страницы входа, регистрации, выхода и отказа в доступе.
- Оболочка на MudBlazor, публичный лендинг, заглушка задач, страница профиля.
- Локализация RU/EN через cookie и `Accept-Language`.
- Unit-тесты доменных правил (32 проходят).
- Integration-тесты на Testcontainers для аутентификации и персистентности (7 проходят).

## В работе

- Первый запуск CI (после публикации репозитория на GitHub).
- Неделя 2: проекты, теги, задачи, подзадачи, views, quick add.

## Далее

- Неделя 2: проекты, теги, задачи, подзадачи, views, quick add.

## Блокеры и риски

- Docker Desktop должен быть запущен для PostgreSQL, Mailpit, integration-тестов и E2E.
- GitHub CLI не установлен; пуш в публичный репозиторий пока не сделан.
- SMTP-провайдер для прода выберем до деплоя MVP.

## Quality gates

| Gate | Статус |
|---|---|
| Build | ✅ 0 предупреждений, 0 ошибок |
| Unit tests | ✅ 32 проходят |
| Integration tests | ✅ 7 проходят (Testcontainers) |
| Component tests | ⬜ только scaffold (неделя 4) |
| E2E tests | ⬜ только scaffold (неделя 4) |
| CI | ⬜ workflow добавлен, первый запуск впереди |
| Deployed | ⬜ |
