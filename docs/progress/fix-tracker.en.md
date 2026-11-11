# Fix Tracker — Production Readiness (P1/P2/P3)

Branch: `main` | Created: 2026-11-11 | Status: Completed

## Summary

This document tracks the fixes identified in the pre-production code review of
`TodoListProject`. Work is done one item at a time, with a commit after each item.

## P1 — Must fix before production

### P1.1 — Validate task status enum on POST /api/v1/tasks/{id}/status

**Problem:** `TodoTask.SetStatus` accepts any `TodoTaskStatus` value (numbers bind via
`JsonStringEnumConverter`), e.g. `{"status": 99}` is saved as `"99"` and the task
disappears from Kanban columns.

**Fix:** Validation already exists in `TodoTask.SetStatus` (`Enum.IsDefined`) and
`TaskService.SetStatusAsync`. Added an integration test proving `99` → `400`.

**Status:** ✅ Done
**Commit:** `aaf258e` — `fix: validate task status enum and add ETag/If-Match for optimistic concurrency`
**Test:** `ApiEndpointTests.SetStatus_InvalidEnum_ReturnsBadRequest` (integration)

### P1.2 — Optimistic concurrency: ETag + If-Match

**Problem:** `xmin` row version configured but never exposed to the client; two
users editing the same task → unhandled `DbUpdateConcurrencyException` → 500.

**Fix:**
- `GET /api/v1/tasks/{id}` now returns `ETag: "<version>"` header (version comes from
  `TaskDetails.Version`, already populated from `xmin` shadow property).
- `PUT /api/v1/tasks/{id}` now reads `If-Match` header (takes precedence over
  `body.Version`) and returns `409 Conflict` on stale version.
- `TaskService.UpdateAsync` already catches `DbUpdateConcurrencyException` and returns
  `Result.Failure(ConcurrencyConflict())` → mapped to `409` by `ApiResults`.

**Status:** ✅ Done
**Commit:** `aaf258e` (same as P1.1)
**Tests:**
- `ApiEndpointTests.GetTask_ReturnsETagHeader`
- `ApiEndpointTests.UpdateTask_StaleIfMatch_ReturnsConflict`
- `ApiEndpointTests.UpdateTask_MatchingIfMatch_ReturnsNoContent`

### P1.3 — Atomic save for task edit dialog

**Problem:** `TaskEditDialog.SaveAsync` called `UpdateAsync`, `SetStatusAsync`,
`AddTagAsync`/`RemoveTagAsync` sequentially — each with its own `SaveChangesAsync`.
A failure at step 3 leaves steps 1–2 committed.

**Fix:** `TaskService.UpdateDetailsAsync` already performs details + status +
tags + reminder in one `SaveChangesAsync`. No code change needed — added an
integration test proving atomicity.

**Status:** ✅ Done (test only)
**Commit:** `19c97ee` — `test: cover atomic task-details update and reminder retries`
**Test:** `TaskManagementTests.TaskService_UpdateDetailsAsync_AtomicTransaction` (integration)

### P1.4 — Reminder retries after transient send failure

**Problem:** `ReminderDeliveryService` calls `MarkFailed` on exception, which puts
the reminder in terminal `Failed` after `MaxAttempts`. Any brief SMTP outage loses
reminders permanently. `ReminderDispatcher` not tested.

**Fix:** Domain logic already supports retries (`MarkFailed` returns to `Pending` with
exponential back-off until `MaxAttempts`). No code change needed — added integration
tests proving the retry path.

**Status:** ✅ Done (test only)
**Commit:** `19c97ee` (same as P1.3)
**Tests:**
- `ReminderAndRecurrenceTests.ReminderDeliveryService_FailedDelivery_ReturnsToPendingAndRetries`
- `ReminderAndRecurrenceTests.ReminderDeliveryService_MaxAttempts_BecomesTerminalFailed`

## P2 — Fix in next iterations

### P2.5 — Import validation and correct 400 instead of 500

**Status:** ✅ Done
**Commit:** `23e91ff` — `fix: validate import document up front and return 400 instead of 500`

### P2.6–7 — Localize error messages and remaining hardcoded strings

**Status:** ✅ Done
**Commit:** `70d5195` — `fix: localize error messages and remove hardcoded English strings`

### P2.8 — TimeProvider instead of DateTime.UtcNow/Now

**Status:** ✅ Done
**Commit:** `0101dac` — `fix: remove DateTimeOffset.UtcNow default from ApplicationUser`

### P2.9 — Remove committed secrets

**Status:** ✅ Done
**Commit:** `bc70119` — `fix: remove committed secrets`

### P2.10 — Single source of truth for reminder scheduled time in seeder

**Problem:** `DemoDataSeeder` computes `ScheduledAtUtc` via `TimeSpan.Zero` (hard UTC),
while `TaskReminderScheduler` uses `UserTime.ToUtc` with user time zone. Diverges on
TZ change.

**Plan:** Use `UserTime.ToUtc` in `DemoDataSeeder.AddTask`.

**Status:** ✅ Done — every `AddTask` call passes `user.TimeZoneId`, and the seeder
now assigns a distinct `SortOrder` so the demo board order is deterministic.
**Commit:** `fbea7e4` — `fix: use user time zone and deterministic order in demo seeder`

### P2.11 — Stronger migration test

**Problem:** `DatabaseTests.Migrations_AreApplied` only checks `InitialCreate` is applied.
Should also assert `GetPendingMigrationsAsync().ShouldBeEmpty()`.

**Plan:** Add pending-migrations check to `DatabaseTests`.

**Status:** ✅ Done — `GetPendingMigrationsAsync().ShouldBeEmpty()`.
**Commit:** `20198b8` — `test: assert no pending migrations are applied`

### P2.12 — OpenAPI/Scalar only in Development

**Problem:** `app.MapOpenApi()` and `MapScalarApiReference("/scalar`) are unconditional —
extra attack surface in production.

**Plan:** Wrap both in `if (app.Environment.IsDevelopment())`.

**Status:** ✅ Done — both `AddOpenApi()` registration and endpoint mapping are
Development-only.
**Commit:** `cebeb86` — `fix: expose OpenAPI and Scalar only in Development`

## P3 — Hygiene

### P3.1 — Remove unused resource keys

**Problem:** 8 unused keys: `Tagline`, `Profile`, `Add`, `Close`, `Confirm`, `Loading`,
`Optional`, `Overdue`.

**Plan:** Remove from both `.resx` files (verify no Razor references first).

**Status:** ✅ Done
**Commit:** `4543344` — `chore: remove unused resource keys and dead packages`

### P3.2 — Remove dead NuGet packages

**Problem:** `Microsoft.Extensions.TimeProvider.Testing` and `Respawn` declared in
`Directory.Packages.props` but not referenced anywhere.

**Plan:** Remove from `Directory.Packages.props`.

**Status:** ✅ Done — also removed the stale `Respawn` mention from ADR 0005.
**Commit:** `4543344` — `chore: remove unused resource keys and dead packages`

### P3.3 — Fix docs/progress/status.en.md contradiction

**Problem:** Lines 36–39 say "47 unit / 45 integration / 32 component / 12 E2E",
but the summary table (lines 76–79) says "52 / 57 / 32 / 14".

**Plan:** Align the text numbers with the table (and reality). Update paired `.ru.md`.

**Status:** ✅ Done — both files now report 54 unit / 70 integration / 32 component /
14 E2E, matching the actual run after the P1 test additions.
**Commit:** `docs: align test counts and complete fix tracker`

## Completed commits

| Commit | Message | Items |
|--------|---------|-------|
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
| _this commit_ | docs: align test counts and complete fix tracker | P3.3 |

## Notes

- Follow-up items outside this pass live in `docs/progress/backlog.en.md`.
- Integration tests require Docker (Testcontainers PostgreSQL on port 5433).
- Run: `dotnet test tests/TodoList.IntegrationTests/TodoList.IntegrationTests.csproj`
- Unit tests: `dotnet test tests/TodoList.UnitTests/TodoList.UnitTests.csproj`
- The earlier failures where specific validation codes answered `500` are fixed:
  `ApiResults` now maps every validator code to `400`, and import document errors
  use `import_invalid`. Current run: 54 unit / 70 integration / 32 component pass.
