# Локальный запуск

## Требования

- .NET SDK **10.0.400** — зафиксирован в `global.json`.
- Docker Desktop — PostgreSQL, Mailpit, integration-тесты и E2E.
- Git.

## Инфраструктура

```bash
docker compose up -d
```

| Сервис | Адрес |
|---|---|
| PostgreSQL | `localhost:5433`, база/пользователь `todolist`, пароль `todolist_dev` |
| Mailpit SMTP | `localhost:1025` |
| Mailpit UI | http://localhost:8025 |

## Сборка и запуск

```bash
dotnet restore
dotnet build
dotnet run --project src/TodoList.Web
```

Приложение доступно на https://localhost:7037 или http://localhost:5122.

## База данных

EF Core migrations и `AppDbContext` уже есть. Применить их можно так:

```bash
dotnet ef database update --project src/TodoList.Infrastructure --startup-project src/TodoList.Web
```

В Development миграции могут применяться при старте; в production — отдельным шагом деплоя.

## Тесты

```bash
dotnet test
```

Полная стратегия и команды по suite — в [testing](testing.ru.md).

## Troubleshooting

- **Docker daemon не запущен:** запустите Docker Desktop и повторите `docker compose up -d`.
- **Порт 5433 или 8025 занят:** остановите конфликтующий сервис или измените host-порт в `docker-compose.yml`. База публикуется на 5433, чтобы не конфликтовать с локально установленным PostgreSQL на 5432.
- **Несовпадение SDK:** выполните `dotnet --version`; версия должна удовлетворять `global.json`.
