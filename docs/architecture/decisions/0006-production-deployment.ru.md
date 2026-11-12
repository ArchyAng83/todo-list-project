# ADR-0006: Форма production-деплоя

- **Статус:** принято
- **Дата:** 2026-11-12

## Контекст

Приложение — один процесс ASP.NET Core, который обслуживает Blazor Server, страницы Identity и `/api/v1`. Для production нужен повторяемый деплой ещё до выбора площадки хостинга и SMTP-провайдера. Несколько runtime-деталей имеют последствия для безопасности: ключи шифрования, флаги cookie, определение IP клиента за TLS-прокси и отправка email.

## Решение

Поставляем и эксплуатируем приложение со следующими умолчаниями:

- **Один контейнер приложения, одна реплика.** Blazor Server держит circuit в памяти, а reminder dispatcher не использует межрепличный claim. API позже сможет масштабироваться отдельно; UI для MVP остаётся в одной реплике.
- **Два образа.** `Dockerfile` собирает runtime-образ. `Dockerfile.migrations` собирает self-contained bundle миграций EF Core, который применяется отдельным шагом до переключения трафика.
- **Ключи Data Protection на volume.** `DataProtection:KeysPath` сохраняет key ring; в образе по умолчанию `/app/keys`. Cookie, antiforgery-токены и bearer/refresh-токены переживают пересоздание контейнера только при стабильном и включённом в бэкап volume.
- **Secure-cookie вне Development.** Auth- и antiforgery-cookie используют `CookieSecurePolicy.Always` вне Development. В Development остаётся `SameAsRequest`, чтобы локальный запуск по обычному HTTP работал.
- **Forwarded headers включаются явно.** `ForwardedHeaders:Enabled` по умолчанию выключен. При включении учитываются только `X-Forwarded-For` и `X-Forwarded-Proto`, с `ForwardLimit` и явным списком доверия `KnownProxies`/`KnownNetworks`. Так rate limiter аутентификации ключуется по реальному IP клиента, а HSTS и secure-cookie работают за TLS-терминацией.
- **SMTP не обязателен при старте.** Пустой `Email:Host` выбирает logging-sender и пишет предупреждение при старте; оператор либо настраивает SMTP, либо осознанно ставит `Reminders:Enabled=false`.
- **Compose — эталонная топология.** `docker-compose.production.yml` поднимает PostgreSQL, bundle миграций и приложение, а `.env.production.example` описывает все переменные. Площадки хостинга могут перенести тот же контракт контейнеров.

## Последствия

- Деплой становится механическим, как только появятся хост и SMTP-аккаунт; для сборки и тестов релиза ни то, ни другое не нужно.
- Потеря volume Data Protection разлогинивает всех пользователей и инвалидирует токены, поэтому volume входит в бэкапы.
- Без forwarded headers за прокси приложение видит HTTP и IP прокси; гайд по деплою описывает и симптом, и override.
- Масштабирование больше одной реплики сначала требует claim для напоминаний и sticky-сессий Blazor.
- Bundle миграций должен выполняться до старта приложения; compose обеспечивает это через `service_completed_successfully`.
