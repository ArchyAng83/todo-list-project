# Деплой

Приложение поставляется двумя контейнерными образами: основной обслуживает Blazor, auth-страницы и `/api/v1`, а второй содержит self-contained bundle миграций EF Core, чтобы изменения схемы оставались отдельным шагом деплоя.

## Образы

```bash
docker build -t todolist:local .
docker build -f Dockerfile.migrations -t todolist-migrations:local .
```

`Dockerfile` многоступенчатый:

1. Восстановление и публикация `TodoList.Web` в Release на зафиксированном SDK.
2. Копирование результата в runtime-образ ASP.NET Core.
3. Создание `/app/keys` с владельцем `$APP_UID` и `DataProtection__KeysPath`, указывающим на этот каталог.
4. Запуск от непривилегированного пользователя `$APP_UID` с прослушиванием порта **8080**.

`Dockerfile.migrations` восстанавливает `dotnet-ef` из `.config/dotnet-tools.json`, собирает self-contained bundle под `linux-x64` и копирует его в образ `runtime-deps`. Артефакт деплоя — сам bundle; основной образ вне Development никогда не применяет миграции при старте.

`.dockerignore` не пускает в контекст сборки `bin`, `obj`, тесты, docs и локальные артефакты.

## Конфигурация

| Настройка | Обязательна | Комментарий |
|---|---|---|
| `ConnectionStrings__Default` | да | строка подключения Npgsql |
| `ASPNETCORE_ENVIRONMENT` | да | `Production` включает HSTS, обработчик исключений, secure-cookie и JSON-логи в консоль |
| `ASPNETCORE_URLS` | нет | в образе по умолчанию `http://+:8080` |
| `DataProtection__KeysPath` | да в production | каталог key ring Data Protection; в образе `/app/keys` |
| `DataProtection__ApplicationName` | нет | по умолчанию `TodoList`; не меняйте, чтобы ключи продолжали совпадать |
| `ForwardedHeaders__Enabled` | нет | по умолчанию `false`; включайте только за доверенным прокси |
| `ForwardedHeaders__ForwardLimit` | нет | сколько прокси разворачивать; по умолчанию `1` |
| `ForwardedHeaders__KnownProxies` | нет | список IP; при заполнении заменяет loopback-значения по умолчанию |
| `ForwardedHeaders__KnownNetworks` | нет | список CIDR (например `172.16.0.0/12`); при заполнении заменяет loopback-значения по умолчанию |
| `Auth__RateLimit__PermitLimit` | нет | запросов входа/регистрации на IP клиента за окно; по умолчанию `10` |
| `Auth__RateLimit__WindowSeconds` | нет | длина фиксированного окна в секундах; по умолчанию `60` |
| `Email__Host` | нет | пустое значение включает logging-sender и предупреждение при старте |
| `Email__Port` | нет | по умолчанию `25` |
| `Email__UseSsl` | нет | `true` включает STARTTLS (обычно для порта 587) |
| `Email__UserName` / `Email__Password` | нет | учётные данные SMTP |
| `Email__FromAddress` / `Email__FromName` | нет | отправитель; в production задайте реальный проверенный домен |
| `Reminders__Enabled` | нет | по умолчанию `true`; `false` останавливает dispatcher |
| `Logging__LogLevel__Default` | нет | по умолчанию `Information` |

Демо-сидер работает только в Development, поэтому в production демо-аккаунт не создаётся. Секреты передавайте через хранилище секретов платформы; не вшивайте их в образ.

```bash
docker run --rm -p 8080:8080 \
  -e 'ASPNETCORE_ENVIRONMENT=Production' \
  -e 'ConnectionStrings__Default=Host=db;Port=5432;Database=todolist;Username=todolist;Password=***' \
  -e 'DataProtection__KeysPath=/app/keys' \
  -v todolist-keys:/app/keys \
  todolist:local
```

## Compose-стек

`docker-compose.production.yml` поднимает PostgreSQL, bundle миграций и приложение. Все переменные описаны в `.env.production.example`.

```bash
cp .env.production.example .env.production   # заполните POSTGRES_PASSWORD и SMTP
docker compose --env-file .env.production -f docker-compose.production.yml up -d --build
```

- У `postgres` нет опубликованного порта; база доступна только внутри compose-сети.
- `migrate` ждёт health-check базы и выполняется один раз. Приложение стартует только после его успешного завершения.
- `app` публикует `${APP_PORT:-8080}` и хранит key ring в volume `data-protection-keys`.
- Запускайте ровно **одну** реплику приложения. Blazor Server держит circuit в памяти, а у reminder dispatcher нет межрепличного claim, поэтому две реплики обработают одни и те же due-строки дважды.

## Миграции

В production миграции применяются отдельным шагом деплоя; вне Development приложение не мигрирует базу при старте.

В compose это уже делает сервис `migrate`. Без compose соберите образ bundle один раз и запустите его против базы:

```bash
docker build -f Dockerfile.migrations -t todolist-migrations .
docker run --rm todolist-migrations \
  --connection "Host=db;Port=5432;Database=todolist;Username=todolist;Password=***"
```

Запускайте bundle до переключения трафика на новый образ и держите в голове план отката: миграция, удаляющая или переименовывающая колонку, автоматически не откатывается. Сначала сделайте бэкап.

## Health-пробы

| Endpoint | Назначение | Ожидание |
|---|---|---|
| `/health/live` | liveness: процесс отвечает | `200` |
| `/health/ready` | readiness: PostgreSQL доступен | `200`, иначе `503` |

Liveness-пробу платформы направьте на `/health/live`, readiness — на `/health/ready`. Liveness намеренно не проверяет базу, чтобы медленная база не перезапускала здоровый процесс. Health-check в compose проверяет только слушающий порт, потому что в runtime-образе нет HTTP-клиента.

## Data Protection

Blazor-circuit, auth-cookie, antiforgery-токены и bearer/refresh-токены защищены key ring Data Protection.

- Задайте `DataProtection__KeysPath` с постоянным каталогом и смонтируйте туда volume (в образе это `/app/keys`).
- Пересоздание контейнера без постоянных ключей инвалидирует все cookie и токены — пользователей разлогинит.
- При старте в production пишется предупреждение, если `DataProtection__KeysPath` не задан.
- Делайте бэкап volume или key ring вместе с базой: восстановление базы без ключей всё равно заставит всех войти заново.
- Файлы ключей по умолчанию не шифруются на диске; защитите volume правами хоста и шифрованием диска.
- При нескольких репликах все они должны делить один key ring и `DataProtection__ApplicationName`.

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

Завершайте TLS на прокси или ingress платформы и передавайте обычный HTTP на порт 8080. Затем:

1. Включите `ForwardedHeaders__Enabled=true`.
2. Доверяйте только прокси: `ForwardedHeaders__KnownProxies` (IP) или
   `ForwardedHeaders__KnownNetworks` (CIDR-диапазон). Заполнение любого списка
   заменяет loopback-значения по умолчанию.
3. Оставляйте `ForwardedHeaders__ForwardLimit=1`, если цепочка из нескольких прокси не нужна.

Для compose добавьте небольшой override, не правя отслеживаемый файл:

```yaml
services:
  app:
    environment:
      ForwardedHeaders__Enabled: "true"
      ForwardedHeaders__KnownNetworks__0: 172.16.0.0/12
```

С доверенными заголовками приложение видит `https` и реальный IP клиента. Без них оно видит `http` и IP прокси: auth-cookie теряют `Secure`, HSTS не применяется, а rate limiter сваливает всех пользователей в одну партицию. Если приложение открыто напрямую, выключенная функция ещё и не даёт подделать IP через заголовки. Пока TLS завершается выше, `UseHttpsRedirection` не может определить HTTPS-порт и пишет безобидное предупреждение.

## Напоминания и email

Задания напоминаний пишутся в базу и доставляются hosted dispatcher.

- Настройте `Email__Host`/`Port`/`UseSsl`/`UserName`/`Password` и отправителя на проверенном домене.
- При пустом `Email__Host` DI регистрирует `LoggingEmailSender`: напоминания обрабатываются, но только пишутся в лог. В production в этом случае выводится предупреждение при старте.
- `Reminders__Enabled=false` полностью останавливает dispatcher (например, пока SMTP-провайдер не выбран).
- Доставка идёт через `System.Net.Mail.SmtpClient`; `Email__UseSsl=true` включает STARTTLS, как ожидают провайдеры на порту 587.
- Dispatcher выбирает due-строки без row-level locking. Держите одну реплику приложения; для масштабирования сначала нужен claim (`FOR UPDATE SKIP LOCKED`).

## Логирование

Вне Development приложение пишет однострочный JSON в stdout, включая scopes и UTC-таймстампы. Сбор логов оставьте платформе; файлы не пишутся.

## Чек-лист перед запуском

- [ ] База доступна из сети runtime, миграции применены.
- [ ] `ConnectionStrings__Default` передаётся из хранилища секретов.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `DataProtection__KeysPath` указывает на смонтированный volume, и этот volume входит в бэкапы.
- [ ] Настроены liveness- и readiness-пробы.
- [ ] TLS завершается на прокси, публичное имя хоста настроено.
- [ ] `ForwardedHeaders__Enabled=true` и прокси в списке доверия, либо зафиксирована причина оставить приложение открытым напрямую.
- [ ] `Auth__RateLimit__*` проверены под ожидаемый трафик (общий NAT или прокси считается одним клиентом).
- [ ] SMTP-провайдер настроен, либо `Reminders__Enabled=false` зафиксирован как осознанное решение.
- [ ] `Email__FromAddress`/`FromName` используют реальный проверенный домен; настроены SPF, DKIM и DMARC.
- [ ] Одна реплика приложения обеспечена; вторая реплика запрещена, пока нет claim для напоминаний.
- [ ] Настроены бэкапы и проверено восстановление PostgreSQL **и** volume Data Protection.
- [ ] После деплоя пройден smoke-тест: регистрация, вход, создание задачи, доска и календарь, экспорт данных, `/health/ready`.
