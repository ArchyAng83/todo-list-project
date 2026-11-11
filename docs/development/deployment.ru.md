# Деплой

Приложение поставляется одним контейнерным образом: ASP.NET Core обслуживает Blazor, auth-страницы и `/api/v1`.

## Образ

```bash
docker build -t todolist:local .
```

`Dockerfile` многоступенчатый:

1. Восстановление и публикация `TodoList.Web` в Release на зафиксированном SDK.
2. Копирование результата в runtime-образ ASP.NET Core.
3. Запуск от непривилегированного пользователя `$APP_UID` с прослушиванием порта **8080**.

`.dockerignore` не пускает в контекст сборки `bin`, `obj`, тесты, docs и локальные артефакты.

## Конфигурация

| Настройка | Обязательна | Комментарий |
|---|---|---|
| `ConnectionStrings__Default` | да | строка подключения Npgsql |
| `ASPNETCORE_ENVIRONMENT` | да | `Production` включает HSTS, обработчик исключений и JSON-логи в консоль |
| `ASPNETCORE_URLS` | нет | в образе по умолчанию `http://+:8080` |
| `Auth__RateLimit__PermitLimit` | нет | запросов входа/регистрации на IP клиента за окно; по умолчанию `10` |
| `Auth__RateLimit__WindowSeconds` | нет | длина фиксированного окна в секундах; по умолчанию `60` |
| `Logging__LogLevel__Default` | нет | по умолчанию `Information` |

Демо-сидер работает только в Development, поэтому в production демо-аккаунт не создаётся. Секреты передавайте через хранилище секретов платформы; не вшивайте их в образ.

```bash
docker run --rm -p 8080:8080 \
  -e 'ASPNETCORE_ENVIRONMENT=Production' \
  -e 'ConnectionStrings__Default=Host=db;Port=5432;Database=todolist;Username=todolist;Password=***' \
  todolist:local
```

## Миграции

В production миграции применяются отдельным шагом деплоя; вне Development приложение не мигрирует базу при старте.

```bash
# Соберите self-contained bundle один раз и запускайте его из пайплайна деплоя.
dotnet ef migrations bundle \
  --project src/TodoList.Infrastructure \
  --startup-project src/TodoList.Web \
  --output efbundle

./efbundle --connection "Host=db;Port=5432;Database=todolist;Username=todolist;Password=***"
```

Запускайте bundle до переключения трафика на новый образ и держите в голове план отката: миграция, удаляющая или переименовывающая колонку, автоматически не откатывается.

## Health-пробы

| Endpoint | Назначение | Ожидание |
|---|---|---|
| `/health/live` | liveness: процесс отвечает | `200` |
| `/health/ready` | readiness: PostgreSQL доступен | `200`, иначе `503` |

Liveness-пробу платформы направьте на `/health/live`, readiness — на `/health/ready`. Liveness намеренно не проверяет базу, чтобы медленная база не перезапускала здоровый процесс.

## Ограничение частоты аутентификации

`POST /api/v1/auth/login`, `POST /account/login` и `POST /account/register`
ограничены по IP клиента фиксированным окном: по умолчанию 10 запросов за 60
секунд, настраивается через `Auth:RateLimit`. Отказ возвращает
`429 Too Many Requests` с `Retry-After`; lockout Identity (5 попыток, 15 минут)
остаётся защитой на уровне аккаунта. Загрузка страниц, статика и остальной
`/api/v1` не ограничиваются.

Ключ партиции — `HttpContext.Connection.RemoteIpAddress`. При прямом доступе это
адрес вызывающего; за прокси все запросы приходят с адреса прокси, и весь деплой
попадёт в одну партицию. Включите forwarded headers — с прокси в
`KnownProxies`/`KnownNetworks` — прежде чем полагаться на лимит, либо поднимите
его под суммарный трафик прокси.

## Reverse proxy и TLS

Завершайте TLS на прокси или ingress платформы и передавайте обычный HTTP на порт 8080. `UseHttpsRedirection` не может определить HTTPS-порт за таким прокси и пишет безобидное предупреждение; настройте forwarded headers, прежде чем открывать приложение напрямую в интернет.

## Ограничения Blazor Server

Blazor Server держит состояние circuit в памяти и использует ключи Data Protection.

- Для MVP запускайте один экземпляр.
- Для масштабирования включите sticky sessions **и** общий key ring Data Protection между экземплярами.
- API (`/api/v1`) не хранит состояние и масштабируется независимо.

## Логирование

Вне Development приложение пишет однострочный JSON в stdout, включая scopes и UTC-таймстампы. Сбор логов оставьте платформе; файлы не пишутся.

## Чек-лист перед запуском

- [ ] База доступна из сети runtime, миграции применены.
- [ ] `ConnectionStrings__Default` передаётся из хранилища секретов.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] Настроены liveness- и readiness-пробы.
- [ ] TLS завершается на прокси, публичное имя хоста настроено.
- [ ] `Auth__RateLimit__*` проверены под ожидаемый трафик (общий NAT или прокси считается одним клиентом).
- [ ] SMTP-провайдер для прода выбран до запуска email-напоминаний.
- [ ] Настроены бэкапы и проверено восстановление PostgreSQL.
