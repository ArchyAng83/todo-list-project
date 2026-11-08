# Scope

## MVP — Phase 1 (weeks 1–4)

- ASP.NET Core Identity: register, login/logout, email confirmation, password reset/change, lockout, rate limiting.
- Demo account with seeded projects, tags, tasks, and checklist items.
- Projects, tags, tasks, subtasks, priorities, due dates.
- Views: Inbox, Today, Upcoming, All, Completed, Project, Tag.
- Search, filters, sorting, quick add.
- Settings: language, theme, timezone, week start.
- Responsive MudBlazor UI, light/dark, RU/EN.
- Minimal REST API `/api/v1` with bearer tokens, OpenAPI, and Scalar.
- Unit, integration, component, and E2E smoke tests.
- CI, Dockerfile, health checks, structured logging, deployment.
- README, bilingual docs, demo credentials.

## Phase 2 — Views (weeks 5–6)

- Kanban board with drag-and-drop and ordering.
- Month calendar with tasks by due date and create-on-day-click.

## Phase 3 — Productivity (weeks 7–8)

- Recurring tasks: daily/weekly/monthly with interval and end date.
- Email reminders with a database-backed outbox and background dispatcher.
- JSON import/export with a versioned schema, CSV export.

## Phase 4 — AI (post-v1.0)

- Natural-language quick add with structured outputs.
- Tool-calling assistant that reads and mutates tasks.
- Evaluation suite, tracing, cost/latency guardrails.

## Out of scope for v1.0

- Teams, sharing, and collaboration.
- Attachments and file uploads.
- Native mobile apps.
- Full RFC 5545 RRULE support; the MVP uses a simplified recurrence model.
- 2FA and external identity providers (candidates for later).
- PWA/offline mode.
