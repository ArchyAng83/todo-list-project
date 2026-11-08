# Неделя 1 — фундамент

Даты: 2026-11-08 — 2026-11-14

Статус: в работе

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

## Решения

- Blazor Web App, Interactive Server для страниц приложения; auth-страницы — static SSR.
- PostgreSQL + EF Core + `IAppDbContext`; без generic-репозиториев.
- Identity cookie для Blazor; bearer-only API.
- Модульный монолит; без MediatR и AutoMapper.
- Пирамида тестов: unit, integration, component, E2E.

## Метрики

- Build: проходит.
- Тесты: только scaffold.
- CI: workflow добавлен; первый запуск впереди.

## Далее

- Неделя 2: проекты, теги, задачи, подзадачи, views, quick add.
