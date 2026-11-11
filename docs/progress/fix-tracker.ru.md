# Трекер исправлений — готовность к продакшену (P1/P2/P3)

Ветка: `main` | Создан: 2026-11-11 | Статус: Завершено

## Сводка

Этот документ отслеживает исправления, выявленные при предпроизводственном ревью кода
`TodoListProject`. Работа ведётся по одному пункту за раз, с коммитом после каждого пункта.

## P1 — kritisch, исправить до продакшена

### P1.1 — Валидация enum статуса задачи на POST /api/v1/tasks/{id}/status

**Проблема:** `TodoTask.SetStatus` принимает любое значение `TodoTaskStatus` (числа биндятся через
`JsonStringEnumConverter`), например `{"status": 99}` сохраняется как `"99"` и задача
исчезает с колонок Kanban.

**Исправление:** Валидация уже существует в `TodoTask.SetStatus` (`Enum.IsDefined`) и
`TaskService.SetStatusAsync`. Добавлен интеграционный тест, подтверждающий, что `99` → `400`.

**Статус:** ✅ Готово
**Коммит:** `aaf258e` — `fix: validate task status enum and add ETag/If-Match for optimistic concurrency`
**Тест:** `ApiEndpointTests.SetStatus_InvalidEnum_ReturnsBadRequest` (интеграционный)

### P1.2 — Оптимистичная блокировка: ETag + If-Match

**Проблема:** `xmin` настроен как версия строки, но не отдаётся клиенту; двое пользователей
редактируют одну задачу → необработанное `DbUpdateConcurrencyException` → 500.

**Исправление:**
- `GET /api/v1/tasks/{id}` теперь отдаёт заголовок `ETag: "<version>"` (версия из
  `TaskDetails.Version`, уже заполняется из теневого свойства `xmin`).
- `PUT /api/v1/tasks/{id}` теперь читает заголовок `If-Match` (приоритет над `body.Version`)
  и возвращает `409 Conflict` при устаревшей версии.
- `TaskService.UpdateAsync` уже перехватывает `DbUpdateConcurrencyException` и возвращает
  `Result.Failure(ConcurrencyConflict())` → отображается как `409` через `ApiResults`.

**Статус:** ✅ Готово
**Коммит:** `aaf258e` (тот же, что и P1.1)
**Тесты:**
- `ApiEndpointTests.GetTask_ReturnsETagHeader`
- `ApiEndpointTests.UpdateTask_StaleIfMatch_ReturnsConflict`
- `ApiEndpointTests.UpdateTask_MatchingIfMatch_ReturnsNoContent`

### P1.3 — Атомарное сохранение диалога редактирования задачи

**Проблема:** `TaskEditDialog.SaveAsync` вызывал `UpdateAsync`, `SetStatusAsync`,
`AddTagAsync`/`RemoveTagAsync` последовательно — каждый со своим `SaveChangesAsync`.
Сбой на шаге 3 оставляет шаги 1–2 закоммиченными.

**Исправление:** `TaskService.UpdateDetailsAsync` уже выполняет детали + переход статуса +
теги + напоминание в одном `SaveChangesAsync`. Изменений в коде не требуется — добавлен
интеграционный тест атомарности.

**Статус:** ✅ Готово (только тест)
**Коммит:** `19c97ee` — `test: cover atomic task-details update and reminder retries`
**Тест:** `TaskManagementTests.TaskService_UpdateDetailsAsync_AtomicTransaction` (интеграционный)

### P1.4 — Ретраи напоминаний после временной ошибки отправки

**Проблема:** `ReminderDeliveryService` вызывает `MarkFailed` при исключении, что переводит
напоминание в терминальный `Failed` после `MaxAttempts`. Любой кратковременный сбой SMTP
постоянно теряет напоминания. `ReminderDispatcher` не тестируется.

**Исправление:** Логика домена уже поддерживает ретраи (`MarkFailed` возвращает в `Pending` с
экспоненциальным бэкоффом до `MaxAttempts`). Изменений в коде не требуется — добавлены
интеграционные тесты пути повторной доставки.

**Статус:** ✅ Готово (только тесты)
**Коммит:** `19c97ee` (тот же, что и P1.3)
**Тесты:**
- `ReminderAndRecurrenceTests.ReminderDeliveryService_FailedDelivery_ReturnsToPendingAndRetries`
- `ReminderAndRecurrenceTests.ReminderDeliveryService_MaxAttempts_BecomesTerminalFailed`

## P2 — исправить в следующих итерациях

### P2.5 — Валидация импорта и корректный 400 вместо 500

**Проблема:** `DataTransferService.ImportAsync` перехватывает только
`ArgumentException`/`InvalidOperationException`/`DbUpdateException`; `Project.SetColor`
принимает любую строку (колонка ограничена 7 символами) → `DbUpdateException` не перехватывается → 500.

**План:**
- Добавить валидацию импортируемого документа (FluentValidation или явная проверка полей).
- Расширить catch в `ImportAsync` (сопоставить с `Error.Validation(ImportInvalid)` → 400).
- Добавить проверку hex в `Project.SetColor` (как в `Tag.SetColor`).

**Статус:** ✅ Готово
**Коммит:** `23e91ff` — `fix: validate import document up front and return 400 instead of 500`

### P2.6–7 — Локализация сообщений об ошибках и оставшихся хардкод-строк

**Проблема:** Сообщения FluentValidation и `Error.Message` выводятся как есть в русском UI.
Хардкод английского в `Error.razor`, `MainLayout.razor:93` (Reload),
`ReconnectModal.razor`, `CultureSwitcher.razor:3` (aria-label).

**План:**
- Сопоставить `Error.Code` / коды валидации с ключами локализованных ресурсов в `ErrorLocalizer`
  (частично уже сделано; расширить на все коды).
- Заменить буквальные строки в страницах на `L[...]`; добавить ключи в оба `.resx` файла.

**Статус:** ✅ Готово
**Коммит:** `70d5195` — `fix: localize error messages and remove hardcoded English strings`

### P2.8 — TimeProvider вместо DateTime.UtcNow/Now

**Проблема:** 5 прямых вызовов: `Tasks.razor:155`, `AuthEndpoints.cs:75`, `Program.cs:128`,
`AuthLayout.razor:15`, `ApplicationUser.cs:19`. `ApplicationUser.CreatedAt` использует
`DateTimeOffset.UtcNow` напрямую (нарушение правила для Domain/Infrastructure).

**План:** Инжектировать `TimeProvider` в `ApplicationUser` (или задавать `CreatedAt` из вызывающего кода)
и заменить оставшиеся вызовы `DateTime.UtcNow`/`DateTime.Now`.

**Статус:** ✅ Готово
**Коммит:** `0101dac` — `fix: remove DateTimeOffset.UtcNow default from ApplicationUser`

### P2.9 — Удалить закоммиченные секреты

**Проблема:** `appsettings.json:3` содержит `Password=todolist_dev`;
`DemoDataSeeder.Password = "DemoPass123"` задан жёстко.

**План:**
- Перенести пароль connection string в user-secrets / переменную окружения.
- Перенести пароль демо в конфигурацию (`Seed:DemoPassword`).

**Статус:** ✅ Готово
**Коммит:** `bc70119` — `fix: remove committed secrets`

### P2.10 — Единый источник правды для времени напоминания в сидере

**Проблема:** `DemoDataSeeder` вычисляет `ScheduledAtUtc` через `TimeSpan.Zero` (жёсткий UTC),
тогда как `TaskReminderScheduler` использует `UserTime.ToUtc` с таймзоной пользователя.
Расходятся при смене таймзоны демо-пользователя.

**План:** Использовать `UserTime.ToUtc` в `DemoDataSeeder.AddTask`.

**Статус:** ✅ Готово — все вызовы `AddTask` передают `user.TimeZoneId`, а сидер
теперь назначает уникальный `SortOrder`, чтобы порядок демо-доски был детерминированным.
**Коммит:** `fbea7e4` — `fix: use user time zone and deterministic order in demo seeder`

### P2.11 — Более строгий тест миграций

**Проблема:** `DatabaseTests.Migrations_AreApplied` проверяет только, что `InitialCreate` применён.
Нужно также утверждать `GetPendingMigrationsAsync().ShouldBeEmpty()`.

**План:** Добавить проверку ожидающих миграций в `DatabaseTests`.

**Статус:** ✅ Готово — `GetPendingMigrationsAsync().ShouldBeEmpty()`.
**Коммит:** `20198b8` — `test: assert no pending migrations are applied`

### P2.12 — OpenAPI/Scalar только в Development

**Проблема:** `app.MapOpenApi()` и `MapScalarApiReference("/scalar`) безусловны —
лишняя поверхность атаки в продакшене.

**План:** Обернуть оба вызова в `if (app.Environment.IsDevelopment())`.

**Статус:** ✅ Готово — и регистрация `AddOpenApi()`, и маппинг endpoint-ов
доступны только в Development.
**Коммит:** `cebeb86` — `fix: expose OpenAPI and Scalar only in Development`

## P3 — гигиена

### P3.1 — Удалить неиспользуемые ключи ресурсов

**Проблема:** 8 неиспользуемых ключей: `Tagline`, `Profile`, `Add`, `Close`, `Confirm`, `Loading`,
`Optional`, `Overdue`.

**План:** Удалить из обоих `.resx` файлов (проверить отсутствие ссылок в Razor сначала).

**Статус:** ✅ Готово
**Коммит:** `4543344` — `chore: remove unused resource keys and dead packages`

### P3.2 — Удалить мёртвые пакеты NuGet

**Проблема:** `Microsoft.Extensions.TimeProvider.Testing` и `Respawn` объявлены в
`Directory.Packages.props`, но нигде не подключены.

**План:** Удалить из `Directory.Packages.props`.

**Статус:** ✅ Готово — также убрано устаревшее упоминание `Respawn` из ADR 0005.
**Коммит:** `4543344` — `chore: remove unused resource keys and dead packages`

### P3.3 — Исправить противоречие в docs/progress/status.en.md

**Проблема:** Строки 36–39 говорят "47 unit / 45 integration / 32 component / 12 E2E",
а итоговая таблица (строки 76–79) — "52 / 57 / 32 / 14". Фактически: 52 / 57 / 32 / 14.

**План:** Привести числа в тексте к таблице (и к реальности). Обновить парный `.ru.md`.

**Статус:** ✅ Готово — оба файла теперь показывают 54 unit / 70 integration /
32 component / 14 E2E, как в фактическом прогоне после добавления тестов P1.
**Коммит:** `docs: align test counts and complete fix tracker`

## Завершённые коммиты

| Коммит | Сообщение | Пункты |
|--------|-----------|--------|
| `aaf258e` | fix: validate task status enum and add ETag/If-Match for optimistic concurrency | P1.1, P1.2 |
| `19c97ee` | test: cover atomic task-details update and reminder retries | P1.3, P1.4 |
| `23e91ff` | fix: validate import document up front and return 400 instead of 500 | P2.5 |
| `70d5195` | fix: localize error messages and remove hardcoded English strings | P2.6–7 |
| `0101dac` | fix: remove DateTimeOffset.UtcNow default from ApplicationUser | P2.8 |
| `bc70119` | fix: remove committed secrets | P2.9 |
| `fbea7e4` | fix: use user time zone and deterministic order in demo seeder | P2.10 |
| `20198b8` | test: assert no pending migrations are applied | P2.11 |
| `cebeb86` | fix: expose OpenAPI and Scalar only in Development | P2.12 |
| `f6353c1` | fix: map every validation code to 400 and stabilize reminder tests | follow-up |
| `4543344` | chore: remove unused resource keys and dead packages | P3.1, P3.2 |
| _этот коммит_ | docs: align test counts and complete fix tracker | P3.3 |

## Примечания

- Интеграционные тесты требуют Docker (Testcontainers PostgreSQL на порту 5433).
- Запуск: `dotnet test tests/TodoList.IntegrationTests/TodoList.IntegrationTests.csproj`
- Юнит-тесты: `dotnet test tests/TodoList.UnitTests/TodoList.UnitTests.csproj`
- Прежние сбои, когда специфичные коды валидации давали `500`, исправлены:
  `ApiResults` теперь отдаёт `400` для любого кода валидатора, а ошибки документа
  импорта используют `import_invalid`. Текущий прогон: 54 unit / 70 integration /
  32 component проходят.
