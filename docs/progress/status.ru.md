# Статус проекта

Обновлено: 2026-11-08

- **Текущая фаза:** неделя 1 — фундамент
- **В целом:** bootstrap завершён; MVP в работе.

## Сделано

- Согласованы архитектура, scope и 8-недельный roadmap.
- Решение с Domain, Application, Infrastructure, Web и пятью тестовыми проектами.
- Central package management, зафиксированный .NET SDK, editor config, Git attributes.
- Docker Compose для PostgreSQL и Mailpit.
- CI-скелет GitHub Actions.
- Двуязычная документация и отслеживание прогресса.
- Первый коммит в ветке `main`.

## В работе

- Неделя 1: EF Core + PostgreSQL, Identity, auth-страницы, MudBlazor shell, i18n-фундамент.

## Далее

- Неделя 2: проекты, теги, задачи, подзадачи, views, quick add.

## Блокеры и риски

- Docker Desktop должен быть запущен для PostgreSQL, Mailpit, integration-тестов и E2E.
- GitHub CLI не установлен; пуш в публичный репозиторий пока не сделан.
- SMTP-провайдер для прода выберем до деплоя MVP.

## Quality gates

| Gate | Статус |
|---|---|
| Build | ✅ |
| Unit tests | ⬜ только scaffold |
| Integration tests | ⬜ только scaffold |
| Component tests | ⬜ только scaffold |
| E2E tests | ⬜ только scaffold |
| CI | ⬜ workflow добавлен, первый запуск впереди |
| Deployed | ⬜ |
