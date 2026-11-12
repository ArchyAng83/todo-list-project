# Local Setup

## Prerequisites

- .NET SDK **10.0.400** — pinned in `global.json`.
- Docker Desktop — PostgreSQL, Mailpit, integration tests, and E2E.
- Git.

## Infrastructure

```bash
docker compose up -d
```

| Service | Address |
|---|---|
| PostgreSQL | `localhost:5433`, database/user `todolist`, password `todolist_dev` |
| Mailpit SMTP | `localhost:1025` |
| Mailpit UI | http://localhost:8025 |

## Build and run

```bash
dotnet restore
dotnet build
dotnet run --project src/TodoList.Web
```

The app is available at https://localhost:7037 or http://localhost:5122.

`appsettings.Development.json` already points at the compose PostgreSQL (`todolist_dev`) and Mailpit on `localhost:1025`, so a fresh clone runs without extra configuration.

Health probes:

| Endpoint | Checks |
|---|---|
| `/health/live` | the process responds |
| `/health/ready` | PostgreSQL is reachable |

## Container

```bash
docker build -t todolist .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Host=host.docker.internal;Port=5433;Database=todolist;Username=todolist;Password=todolist_dev" \
  todolist
```

The image publishes in Release, runs as a non-root user, and listens on port 8080. Mount a volume at `/app/keys` to keep the Data Protection key ring, or use the production compose stack. See [deployment](deployment.en.md) for the production checklist.

## Database

EF Core migrations and the `AppDbContext` are in place. `dotnet-ef` is pinned by `.config/dotnet-tools.json`, so restore the local tool once and then apply migrations:

```bash
dotnet tool restore
dotnet ef database update --project src/TodoList.Infrastructure --startup-project src/TodoList.Web
```

Development may apply migrations on startup; production applies them as a separate deploy step.

## Tests

```bash
dotnet test
```

See [testing](testing.en.md) for the full strategy and per-suite commands.

## Troubleshooting

- **Docker daemon is not running:** start Docker Desktop and retry `docker compose up -d`.
- **Port 5433 or 8025 is busy:** stop the conflicting service or change the host port in `docker-compose.yml`. The database is published on 5433 so it does not clash with a locally installed PostgreSQL on 5432.
- **SDK mismatch:** run `dotnet --version`; it must satisfy `global.json`.
