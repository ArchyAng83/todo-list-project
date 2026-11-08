# Неделя 1 — фундамент

Даты: 2026-11-08 — 2026-11-14

Статус: код готов; первый запуск CI впереди

## Цели

- EF Core + PostgreSQL + первая миграция.
- ASP.NET Core Identity и auth-страницы.
- MudBlazor app shell, light/dark тема.
- Фундамент локализации RU/EN.
- Тестовая инфраструктура: Testcontainers, bUnit, Playwright.
- Первый запуск CI.

## Сделано

- Bootstrap-коммит: решение, тестовые проекты, docs, Docker Compose, CI-скелет.
- Архитектурные решения зафиксированы в ADR-0001…0005.
- Доменная модель: проекты, теги, задачи, чек-листы, связи задач с тегами, напоминания.
- `AppDbContext`, EF Core-конфигурации, snake_case и миграция `InitialCreate`, применённая к PostgreSQL 17.
- ASP.NET Core Identity с cookie-аутентификацией, переименованными таблицами и политиками пароля/блокировки.
- Static SSR auth-страницы (`login`, `register`, `logout`, `access-denied`) с antiforgery-защитой.
- Оболочка MudBlazor с навигацией по состоянию аутентификации, публичным лендингом, заглушкой задач и профилем.
- Локализация RU/EN через cookie культуры, fallback на `Accept-Language` и переключатель языка.
- Unit-тесты доменных инвариантов.
- Integration-тесты на Testcontainers: регистрация, редиректы входа, миграции, схема и EF round-trip.
- Ручная проверка в браузере: регистрация, выход, вход, защищённые редиректы, return URL и переключение RU/EN.

## Решения

- Blazor Web App, Interactive Server для страниц приложения; auth-страницы — static SSR.
- PostgreSQL + EF Core + `IAppDbContext`; без generic-репозиториев.
- Identity cookie для Blazor; bearer-only API.
- Модульный монолит; без MediatR и AutoMapper.
- Пирамида тестов: unit, integration, component, E2E.
- PostgreSQL публикуется на host-порту 5433, чтобы не конфликтовать с локально установленным PostgreSQL на 5432.
- bUnit и Playwright подключаются на неделе 4 вместе с интерактивными представлениями задач.

## Метрики

- Build: проходит, 0 предупреждений и 0 ошибок.
- Тесты: 32 unit-теста и 7 integration-тестов проходят.
- CI: workflow добавлен; первый запуск впереди после публикации репозитория.

## Далее

- Неделя 2: проекты, теги, задачи, подзадачи, views, quick add.
