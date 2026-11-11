# Fix Tracker — Production Readiness (P1/P2/P3)

Branch: `main` | Created: 2026-11-11 | Status: In Progress

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

**Problem:** FluentValidation messages and `Error.Message` are shown as-is in Russian UI.
Hardcoded English in `Error.razor`, `MainLayout.razor:93` (Reload),
`ReconnectModal.razor`, `CultureSwitcher.razor:3` (aria-label).

**Plan:**
- Map `Error.Code` / validation codes → localized resource keys in `ErrorLocalizer`
  (already partially done; extend to all codes).
- Replace literal strings in UI pages with `L[...]` lookups; add keys to both `.resx` files.

**Status:** ⬜ Not started

### P2.8 — TimeProvider instead of DateTime.UtcNow/Now

**Problem:** 5 direct calls: `Tasks.razor:155`, `AuthEndpoints.cs:75`, `Program.cs:128`,
`AuthLayout.razor:15`, `ApplicationUser.cs:19`. `ApplicationUser.CreatedAt` uses
`DateTimeOffset.UtcNow` directly (Domain/Infrastructure rule violation).

**Plan:** Inject `TimeProvider` into `ApplicationUser` (or set `CreatedAt` from caller)
and replace remaining `DateTime.UtcNow`/`DateTime.Now` calls.

**Status:** ⬜ Not started

### P2.9 — Remove committed secrets

**Problem:** `appsettings.json:3` contains `Password=todolist_dev`;
`DemoDataSeeder.Password = "DemoPass123"` hardcoded.

**Plan:**
- Move connection string password to user-secrets / environment variable.
- Move demo password to configuration (`Seed:DemoPassword`).

**Status:** ⬜ Not started

### P2.10 — Single source of truth for reminder scheduled time in seeder

**Problem:** `DemoDataSeeder` computes `ScheduledAtUtc` via `TimeSpan.Zero` (hard UTC),
while `TaskReminderScheduler` uses `UserTime.ToUtc` with user time zone. Diverges on
TZ change.

**Plan:** Use `UserTime.ToUtc` in `DemoDataSeeder.AddTask`.

**Status:** ⬜ Not started

### P2.11 — Stronger migration test

**Problem:** `DatabaseTests.Migrations_AreApplied` only checks `InitialCreate` is applied.
Should also assert `GetPendingMigrationsAsync().ShouldBeEmpty()`.

**Plan:** Add pending-migrations check to `DatabaseTests`.

**Status:** ⬜ Not started

### P2.12 — OpenAPI/Scalar only in Development

**Problem:** `app.MapOpenApi()` and `MapScalarApiReference("/scalar`) are unconditional —
extra attack surface in production.

**Plan:** Wrap both in `if (app.Environment.IsDevelopment())`.

**Status:** ⬜ Not started

## P3 — Hygiene

### P3.1 — Remove unused resource keys

**Problem:** 8 unused keys: `Tagline`, `Profile`, `Add`, `Close`, `Confirm`, `Loading`,
`Optional`, `Overdue`.

**Plan:** Remove from both `.resx` files (verify no Razor references first).

**Status:** ⬜ Not started

### P3.2 — Remove dead NuGet packages

**Problem:** `Microsoft.Extensions.TimeProvider.Testing` and `Respawn` declared in
`Directory.Packages.props` but not referenced anywhere.

**Plan:** Remove from `Directory.Packages.props`.

**Status:** ⬜ Not started

### P3.3 — Fix docs/progress/status.en.md contradiction

**Problem:** Lines 36–39 say "47 unit / 45 integration / 32 component / 12 E2E",
but the summary table (lines 76–79) says "52 / 57 / 32 / 14". Actual: 52 / 57 / 32 / 14.

**Plan:** Align the text numbers with the table (and reality). Update paired `.ru.md`.

**Status:** ⬜ Not started

## Completed commits

| Commit | Message | Items |
|--------|---------|-------|
| `aaf258e` | fix: validate task status enum and add ETag/If-Match for optimistic concurrency | P1.1, P1.2 |
| `19c97ee` | test: cover atomic task-details update and reminder retries | P1.3, P1.4 |

## Notes

- Integration tests require Docker (Testcontainers PostgreSQL on port 5433).
- Run: `dotnet test tests/TodoList.IntegrationTests/TodoList.IntegrationTests.csproj`
- Unit tests: `dotnet test tests/TodoList.UnitTests/TodoList.UnitTests.csproj`
- 5 pre-existing integration test failures unrelated to this work (error codes
  `validation` vs `target_index_invalid`/`import_invalid`, and 500s on recurrence
  / timezone endpoints).
