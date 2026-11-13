# Резервное копирование и восстановление

PostgreSQL — единственный источник истины для задач, проектов, тегов и пользователей. Кольцо ключей Data Protection — второй объект с состоянием: восстановление базы без ключей всё равно разлогинит всех пользователей и сделает недействительными выданные bearer- и refresh-токены.

Копируйте и базу, и ключи, храните копии вне хоста и регулярно тренируйте восстановление.

## Что копировать

| Ресурс | Где находится | Зачем |
|---|---|---|
| PostgreSQL | том `postgres-data` compose-стека | Все данные приложения |
| Ключи Data Protection | том `data-protection-keys`, `/app/keys` в контейнере приложения | Cookie, antiforgery-токены, bearer/refresh-токены |
| Секреты конфигурации | Хранилище секретов платформы или `.env.production` | Пароль базы и SMTP-креды не лежат в базе |

Скрипты в `scripts/` копируют базу и кольцо ключей. Держите `.env.production` и архив ключей в том же защищённом месте: они одинаково чувствительны.

## Создать резервную копию

Запустите скрипт из корня репозитория на хосте, где работает compose-стек:

```powershell
pwsh ./scripts/backup-postgres.ps1 `
  -ComposeFile docker-compose.production.yml `
  -EnvFile .env.production `
  -OutputDirectory D:\backups\todolist `
  -RetentionDays 14
```

Скрипт:

1. Выполняет `pg_dump -Fc` внутри сервиса `postgres`.
2. Копирует дамп в `OutputDirectory` как `todolist-<timestamp>.dump`.
3. Копирует `/app/keys` из сервиса `app` и упаковывает в `keys-<timestamp>.zip`.
4. Удаляет дампы и архивы ключей старше `RetentionDays`.

Используйте `-SkipKeys`, только если платформа уже копирует том ключей.

Для локального dev-стека передайте `-ComposeFile docker-compose.yml -EnvFile "" -SkipKeys`: в нём нет сервиса `app`, откуда можно забрать ключи.

### Расписание

Запускайте ежедневное копирование через cron или планировщик платформы. Пример для Linux-хоста:

```cron
15 2 * * * cd /srv/todolist && pwsh ./scripts/backup-postgres.ps1 -ComposeFile docker-compose.production.yml -EnvFile .env.production -OutputDirectory /srv/backups/todolist -RetentionDays 14 >> /var/log/todolist-backup.log 2>&1
```

Скопируйте каталог с бэкапами в объектное хранилище или на другой хост. Бэкап только на том же хосте, где работает база, — не бэкап.

## Тренировка восстановления

Никогда не восстанавливайте в живую базу при работающем приложении. Восстановите в черновую базу, проверьте её и только потом решайте, нужен ли live-restore.

```powershell
pwsh ./scripts/restore-postgres.ps1 `
  -BackupFile D:\backups\todolist\todolist-20261112-020015.dump `
  -ComposeFile docker-compose.production.yml `
  -EnvFile .env.production `
  -TargetDatabase todolist_restore `
  -Force
```

Скрипт пересоздаёт целевую базу, выполняет `pg_restore --no-owner --no-acl` и печатает число строк в `users`. Если команда падает до удаления данных, живая база не затронута.

Затем проверьте:

1. Сравните количество строк в `users`, `tasks`, `projects` и `tags` с источником.
2. Запустите временный экземпляр приложения против черновой базы или переключите существующий через `ConnectionStrings__Default`.
3. Войдите и запустите `scripts/smoke-test.ps1` против этого экземпляра.
4. Удалите черновую базу после тренировки.

```bash
docker compose --env-file .env.production -f docker-compose.production.yml \
  exec -T postgres sh -c 'dropdb -U "$POSTGRES_USER" --if-exists --force todolist_restore'
```

## Восстановление кольца ключей

Остановите приложение перед заменой ключей. Имя тома зависит от имени compose-проекта: `todolist-production_data-protection-keys`.

```bash
docker run --rm \
  -v todolist-production_data-protection-keys:/keys \
  -v /srv/backups/todolist:/backup:ro \
  alpine sh -c 'rm -rf /keys/* && cd /keys && unzip /backup/keys-20261112-020015.zip'
```

Запустите приложение и проверьте, что существующая cookie-сессия продолжает работать. Если ключи потеряны, всех пользователей разлогинит; это восстановимо, но копируйте ключи вместе с базой, чтобы не совмещать это с восстановлением.

## Восстановление на живую

При реальном инциденте:

1. Остановите приложение (`docker compose ... stop app`), чтобы записи и отправка напоминаний не гонялись с восстановлением.
2. Сделайте страховочную копию текущей базы, если она ещё доступна.
3. Восстановите выбранный дамп в живую базу. `pg_restore --clean --if-exists` удаляет и пересоздаёт объекты; используйте его только после страховочной копии.
4. Восстановите соответствующее кольцо ключей, если хост пересобирали.
5. Запустите приложение, проверьте `/health/ready` и прогоните smoke-тест.
6. Убедитесь, что в таблице reminders нет зависших `Pending`-строк старше окна простоя. Dispatcher повторит их после старта.

## Чек-лист проверки

- [ ] Дамп не пустой, и `pg_restore --list` читает его.
- [ ] Черновое восстановление показывает ожидаемое количество строк.
- [ ] Архив ключей лежит в том же наборе бэкапа, что и дамп.
- [ ] Сценарий восстановления прогнан за последний квартал.
- [ ] Бэкапы зашифрованы и скопированы с хоста.
- [ ] Окно хранения соответствует согласованной RPO.
