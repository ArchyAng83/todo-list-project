# TodoList

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Interactive%20Server-512BD4)](https://learn.microsoft.com/aspnet/core/blazor/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1)](https://www.postgresql.org/)
[![Status](https://img.shields.io/badge/status-week%205%20complete-brightgreen)](docs/progress/status.en.md)

A production-minded task manager built with **.NET 10**, **Blazor**, and **PostgreSQL**. It is also a portfolio project for AI engineering roles: the core product is a real multi-user task manager, and the AI layer will be added after v1.0 without rewriting the domain.

> Русская версия — в конце документа.

## Project status

**Week 5 complete.** The Kanban board at `/board` groups tasks into `Todo`, `In progress`, and `Done` columns with drag-and-drop, a keyboard-friendly card menu, and persisted manual ordering. It builds on the task views and sorting, persisted settings, deterministic demo account, bearer-only `/api/v1` API, bUnit component tests, Playwright E2E journeys, health checks, structured logging, and the container image from earlier weeks. Deployment is the remaining MVP step.

See [docs/progress/status.en.md](docs/progress/status.en.md) for the current state and [docs/progress/week-05.en.md](docs/progress/week-05.en.md) for the current week.

## Stack

| Layer | Choice |
|---|---|
| Platform | .NET 10 LTS |
| UI | Blazor Web App, Interactive Server |
| Components | MudBlazor |
| Auth | ASP.NET Core Identity: cookie for Blazor, bearer for API |
| Data | PostgreSQL 17 + EF Core 10 + Npgsql |
| API | Minimal API `/api/v1`, OpenAPI + Scalar |
| Localization | `.resx` + `IStringLocalizer`, RU/EN |
| Tests | xUnit, Testcontainers, bUnit, Playwright |
| Infrastructure | Docker, GitHub Actions, health checks, JSON console logging |

## Architecture

A **modular monolith** with four production projects:

```text
TodoList.Web            -> Blazor UI, auth pages, minimal API
TodoList.Infrastructure -> EF Core, Identity, email, background jobs
TodoList.Application    -> use cases, DTOs, validation, abstractions
TodoList.Domain         -> entities, enums, domain rules
```

- Blazor app pages use **Interactive Server**; auth pages are static SSR so cookie authentication works reliably.
- The API is **bearer-only** and reuses Application services. Blazor does not call the API.
- Every aggregate root is scoped by `UserId`; cross-user access returns 404.
- Due dates are stored as local `DateOnly`/`TimeOnly`; instants use UTC `DateTimeOffset`.
- Reminders use a database-backed outbox and an idempotent background dispatcher.

Read more: [architecture overview](docs/architecture/overview.en.md) · [ADRs](docs/architecture/decisions/).

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 0 — Bootstrap | solution, docs, tests, Docker, CI | ✅ |
| 1 — MVP (weeks 1–4) | auth, tasks, projects, tags, search, API, deploy | 🚧 |
| 2 — Views (weeks 5–6) | Kanban, calendar | 🚧 |
| 3 — Productivity (weeks 7–8) | recurring tasks, email reminders, import/export | ⬜ |
| 4 — AI (post-v1.0) | NL quick add, tool-calling agent, evals | ⬜ |

Full roadmap: [docs/product/roadmap.en.md](docs/product/roadmap.en.md).

## Repository layout

```text
src/
  TodoList.Domain/
  TodoList.Application/
  TodoList.Infrastructure/
  TodoList.Web/
tests/
  TodoList.UnitTests/
  TodoList.IntegrationTests/
  TodoList.ComponentTests/
  TodoList.E2ETests/
  TodoList.Testing/
docs/
docker-compose.yml
Dockerfile
.dockerignore
Directory.Build.props
Directory.Packages.props
global.json
TodoList.sln
```

## Local development

### Prerequisites

- .NET SDK **10.0.400** (see `global.json`)
- Docker Desktop
- Git

### Start infrastructure

```bash
docker compose up -d
```

This starts:

- PostgreSQL on `localhost:5433` (`todolist` / `todolist` / `todolist_dev`)
- Mailpit SMTP on `localhost:1025`, web UI on [http://localhost:8025](http://localhost:8025)

### Build and run

```bash
dotnet restore
dotnet build
dotnet run --project src/TodoList.Web
```

Open [https://localhost:7037](https://localhost:7037) or [http://localhost:5122](http://localhost:5122).

In Development the app applies migrations and seeds a demo account:

- Email: `demo@todolist.local`
- Password: `DemoPass123`

Set `Seed:Demo=false` to disable demo data.

### API

The minimal API is bearer-only; Blazor uses cookies and calls Application services directly.

```bash
curl -X POST http://localhost:5122/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo@todolist.local","password":"DemoPass123"}'

curl http://localhost:5122/api/v1/tasks \
  -H "Authorization: Bearer <accessToken>"

# Access tokens last 7 days; refresh with the returned refreshToken.
curl -X POST http://localhost:5122/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken>"}'
```

OpenAPI is served at `/openapi/v1.json`, and the Scalar UI at [http://localhost:5122/scalar](http://localhost:5122/scalar).

The board order is available to API clients through `GET /api/v1/tasks/board` and `POST /api/v1/tasks/{id}/move`.

### Test

```bash
dotnet test
dotnet test TodoList.sln -c Release   # same as CI
```

The suite has 35 unit, 30 integration, 22 component (bUnit), and 8 E2E (Playwright) tests. Integration and E2E tests run against PostgreSQL via Testcontainers and require Docker.

Install the Playwright browser once after building:

```bash
pwsh tests/TodoList.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

E2E starts a real Kestrel process on a free port, waits for `/health/ready`, and drives headless Chromium.

### Container

```bash
docker build -t todolist .
docker run --rm -p 8080:8080 \
  -e 'ConnectionStrings__Default=Host=host.docker.internal;Port=5433;Database=todolist;Username=todolist;Password=todolist_dev' \
  todolist
```

The app listens on port 8080 as a non-root user. Health probes: `/health/live` (process) and `/health/ready` (PostgreSQL). See the [deployment guide](docs/development/deployment.en.md) for production configuration, migrations, and probes.

## Documentation

Documentation is bilingual: English is canonical, Russian is synced.

- [English index](docs/README.en.md)
- [Русский индекс](docs/README.ru.md)
- [Project status](docs/progress/status.en.md)
- [Testing strategy](docs/development/testing.en.md)
- [Deployment](docs/development/deployment.en.md)
- [Git workflow](docs/development/git-workflow.en.md)

## AI roadmap

The AI layer is intentionally deferred until the product core is stable. The planned seams:

- Application services and `/api/v1` endpoints as tools for an agent.
- `Microsoft.Extensions.AI` / Semantic Kernel with OpenAI, Azure OpenAI, or Ollama.
- Structured outputs for natural-language quick add.
- Evaluation suite with golden data, field accuracy, tool-call accuracy, latency, and cost.
- Tracing and guardrails: rate limits, cost caps, prompt-injection protection.

## License

TBD.

---

## Кратко по-русски

**TodoList** — таск-менеджер на **.NET 10 + Blazor + PostgreSQL**, который делается как реальный продукт и как портфолио для AI engineering. Неделя 5 завершена: Kanban-доска по `/board` с колонками `Todo`, `In progress` и `Done`, drag-and-drop, меню карточки для клавиатуры и сохраняемый ручной порядок. До этого — представления и сортировка задач, сохраняемые настройки, демо-аккаунт, REST API `/api/v1`, bUnit-тесты компонентов, E2E на Playwright, health checks, JSON-логи и контейнерный образ. Остался деплой MVP.

- [Статус проекта](docs/progress/status.ru.md)
- [Документация на русском](docs/README.ru.md)
- [Архитектура](docs/architecture/overview.ru.md)
- [Деплой](docs/development/deployment.ru.md)
- [Roadmap](docs/product/roadmap.ru.md)

AI-слой добавим после v1.0: natural-language quick add, агент с tool calling, evals и трейсинг. Ядро проектируется так, чтобы это не потребовало переписывания.
