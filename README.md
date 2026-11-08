# TodoList

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Interactive%20Server-512BD4)](https://learn.microsoft.com/aspnet/core/blazor/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1)](https://www.postgresql.org/)
[![Status](https://img.shields.io/badge/status-bootstrap-yellow)](docs/progress/status.en.md)

A production-minded task manager built with **.NET 10**, **Blazor**, and **PostgreSQL**. It is also a portfolio project for AI engineering roles: the core product is a real multi-user task manager, and the AI layer will be added after v1.0 without rewriting the domain.

> Русская версия — в конце документа.

## Project status

**Bootstrap (Week 0/1).** The solution, test projects, documentation, Docker Compose, and CI skeleton are in place. Product features, authentication, and the REST API are next.

See [docs/progress/status.en.md](docs/progress/status.en.md) for the current state and [docs/progress/week-01.en.md](docs/progress/week-01.en.md) for the current week.

## Stack

| Layer | Choice |
|---|---|
| Platform | .NET 10 LTS |
| UI | Blazor Web App, Interactive Server |
| Components | MudBlazor (planned) |
| Auth | ASP.NET Core Identity: cookie for Blazor, bearer for API |
| Data | PostgreSQL 17 + EF Core 10 + Npgsql |
| API | Minimal API `/api/v1`, OpenAPI + Scalar |
| Localization | `.resx` + `IStringLocalizer`, RU/EN |
| Tests | xUnit, Testcontainers, bUnit, Playwright |
| Infrastructure | Docker Compose, GitHub Actions, Serilog, OpenTelemetry |

## Architecture

A **modular monolith** with four production projects:

```text
TodoList.Web            -> Blazor UI, auth pages, minimal API
TodoList.Infrastructure -> EF Core, Identity, email, background jobs
TodoList.Application    -> use cases, DTOs, validation, abstractions
TodoList.Domain         -> entities, enums, domain rules
```

- Blazor app pages use **Interactive Server**; auth pages will be static SSR so cookie authentication works reliably.
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
| 2 — Views (weeks 5–6) | Kanban, calendar | ⬜ |
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

- PostgreSQL on `localhost:5432` (`todolist` / `todolist` / `todolist_dev`)
- Mailpit SMTP on `localhost:1025`, web UI on [http://localhost:8025](http://localhost:8025)

### Build and run

```bash
dotnet restore
dotnet build
dotnet run --project src/TodoList.Web
```

Open [https://localhost:7037](https://localhost:7037) or [http://localhost:5122](http://localhost:5122).

### Test

```bash
dotnet test
```

Test infrastructure (Testcontainers, bUnit, Playwright) will be wired in Week 1.

## Documentation

Documentation is bilingual: English is canonical, Russian is synced.

- [English index](docs/README.en.md)
- [Русский индекс](docs/README.ru.md)
- [Project status](docs/progress/status.en.md)
- [Testing strategy](docs/development/testing.en.md)
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

**TodoList** — таск-менеджер на **.NET 10 + Blazor + PostgreSQL**, который делается как реальный продукт и как портфолио для AI engineering. Сейчас завершён bootstrap: решение, тестовые проекты, документация, Docker Compose и CI-скелет. Дальше — auth, задачи, проекты, API и деплой.

- [Статус проекта](docs/progress/status.ru.md)
- [Документация на русском](docs/README.ru.md)
- [Архитектура](docs/architecture/overview.ru.md)
- [Roadmap](docs/product/roadmap.ru.md)

AI-слой добавим после v1.0: natural-language quick add, агент с tool calling, evals и трейсинг. Ядро проектируется так, чтобы это не потребовало переписывания.
