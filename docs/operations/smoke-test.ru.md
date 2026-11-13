# Smoke-тест после деплоя

`scripts/smoke-test.ps1` проверяет публичную поверхность запущенного экземпляра. Он намеренно небольшой: ловит сломанную конфигурацию, недоступную через приложение базу и нарушенный путь чтения/записи после деплоя.

## Что проверяется

1. `/health/live` и `/health/ready` отвечают `200`.
2. Лендинг возвращает hardening-заголовки (`X-Content-Type-Options` и Content Security Policy).
3. Вход по bearer-токену с существующим аккаунтом.
4. Создание, чтение задачи, запросы board и calendar.
5. JSON-экспорт возвращает непустой документ.
6. Созданная задача удаляется (если не передан `-SkipDelete`).

## Запуск

```powershell
pwsh ./scripts/smoke-test.ps1 `
  -BaseUrl https://todo.example.com `
  -Email smoke@example.com `
  -Password 'change-me'
```

Аккаунт должен уже существовать. Создайте отдельный smoke-аккаунт при провижининге или используйте демо-аккаунт на одноразовом dev-хосте.

Скрипт завершается с ненулевым кодом на первом неудачном шаге и печатает HTTP-статус. Письма он не отправляет, поэтому работает и до настройки SMTP-провайдера.

## Локальный запуск и CI

Против одноразового compose-стека:

```bash
docker compose --env-file .env.smoke \
  -f docker-compose.production.yml \
  -f docker-compose.smoke.yml up -d

pwsh ./scripts/smoke-test.ps1 \
  -BaseUrl http://localhost:8080 \
  -Email demo@todolist.local \
  -Password DemoPass123
```

`docker-compose.smoke.yml` запускает production-образ в Development, чтобы демо-сидер создал этот аккаунт, и отключает dispatcher напоминаний. CI прогоняет ту же последовательность в job `container` после сборки обоих образов, поэтому сломанный образ или миграция ловятся до релиза.

## После реального деплоя

Запустите скрипт с машины, которая видит публичный URL, затем пройдите остальной чек-лист в `docs/development/deployment.ru.md`. Smoke-тест не заменяет браузерную проверку интерактивности Blazor; её в CI покрывают Playwright-сценарии.
