# Scope

## MVP — фаза 1 (недели 1–4)

- ASP.NET Core Identity: регистрация, вход/выход, подтверждение email, сброс/смена пароля, lockout, rate limiting.
- Demo-аккаунт с seed-данными: проекты, теги, задачи, checklist.
- Проекты, теги, задачи, подзадачи, приоритеты, дедлайны.
- Представления: Inbox, Today, Upcoming, All, Completed, Project, Tag.
- Поиск, фильтры, сортировка, quick add.
- Настройки: язык, тема, timezone, начало недели.
- Адаптивный MudBlazor UI, light/dark, RU/EN.
- Minimal REST API `/api/v1` с bearer-токенами, OpenAPI и Scalar.
- Unit, integration, component и E2E smoke тесты.
- CI, Dockerfile, health checks, structured logging, деплой.
- README, двуязычная документация, demo credentials.

## Фаза 2 — Views (недели 5–6)

- Kanban с drag-and-drop и сортировкой.
- Календарь на месяц с задачами по дедлайнам и созданием по клику на день.

## Фаза 3 — Productivity (недели 7–8)

- Повторяющиеся задачи: daily/weekly/monthly с интервалом и датой окончания.
- Email-напоминания через database-backed outbox и фоновый dispatcher.
- JSON import/export с версионированной схемой, CSV export.

## Фаза 4 — AI (после v1.0)

- Natural-language quick add со structured outputs.
- Агент с tool calling, который читает и меняет задачи.
- Eval-suite, трейсинг, guardrails по стоимости и latency.

## Вне scope для v1.0

- Команды, шаринг и совместная работа.
- Вложения и загрузка файлов.
- Нативные мобильные приложения.
- Полная поддержка RFC 5545 RRULE; в MVP упрощённая модель recurrence.
- 2FA и внешние identity-провайдеры (кандидаты на later).
- PWA/offline.
