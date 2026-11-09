# Week 7 — Recurring tasks and email reminders

Dates: 2026-12-14 - 2026-12-20

Status: Code complete; all suites green

## Goals

- Recurring tasks with daily, weekly, and monthly frequencies, an interval, and an optional end date.
- Email reminders delivered from a database-backed outbox by a background dispatcher.
- Localized reminder email in the recipient's language.
- Unit, integration, component, E2E, and manual coverage for recurrence and delivery.

## Done

- `RecurrenceFrequency` (`None`, `Daily`, `Weekly`, `Monthly`) and four `TodoTask` fields: `Recurrence`, `RecurrenceInterval`, `RecurrenceEndDate`, and `RecurrenceGenerated`.
- `TodoTask.SetRecurrence` validates that recurrence needs a due date, clamps the interval to 1-365, and rejects an end date before the due date. Clearing the due date clears the recurrence.
- `TodoTask.CreateNextOccurrence(now)` computes the next due date from the previous one (days for daily/weekly, calendar months for monthly), skips past the end date, copies schedule and reminder settings, and guards against duplicate generation with `RecurrenceGenerated`.
- `TodoTask.CopyStructureFrom` copies tag links and checklist item titles into the next occurrence; checklist items start incomplete so the new occurrence is actionable.
- Creating, editing, completing, and moving a recurring task to `Done` all generate the next occurrence in `TaskService`; a reopen and re-complete does not duplicate it, and changing the due date resets the generation guard.
- `TaskReminderScheduler` keeps the outbox in sync: at most one `Pending` reminder per task, edits replace it, completing the task cancels it, and the next occurrence gets its own reminder.
- Reminder time is the local due date/time converted to UTC minus the offset; date-only tasks default to 09:00 local so a reminder never fires at midnight.
- `UserTime` centralizes timezone-aware "today" and local-to-UTC conversion; `TaskQueries.LocalToday` and `UserClock` now share it.
- `TaskService` reconciles reminders in the same `SaveChangesAsync` as the task change, so a task edit cannot leave a stale pending reminder.
- `ReminderDeliveryService` loads due pending reminders, skips or cancels reminders for completed tasks and missing recipients, sends through `IEmailSender`, and marks each message `Sent` or `Failed` with the error text.
- `ReminderDispatcher` is a `BackgroundService` that polls every `Reminders:PollIntervalSeconds` (30 by default, 10 in Development) and delivers a bounded batch.
- `EmailOptions` and `SmtpEmailSender` send HTML and plain-text mail through SMTP; Development points at Mailpit, and the app falls back to `LoggingEmailSender` when `Email:Host` is empty.
- Reminder templates live in `Infrastructure/Resources/Email/ReminderEmailResources.resx` and `.ru.resx`, and the recipient's `Culture` selects the language.
- Task edit dialog gains a repeat select, interval, and end date, enabled only when a due date is set; the task list shows a repeat icon with a localized tooltip.
- `CreateTaskRequest`/`UpdateTaskRequest`, `CreateTaskBody`/`UpdateTaskBody`, and the task endpoints carry recurrence parameters as optional append-only values.
- EF Core configuration and migration `AddTaskRecurrence` add `recurrence`, `recurrence_interval`, `recurrence_end_date`, and `recurrence_generated`; enums persist as strings.
- Demo seed adds a recurring "Weekly planning" task (tomorrow 09:00, weekly, ends in eight weeks) and persists reminders, so Mailpit shows real mail during a smoke test.
- 12 unit tests for recurrence and structure copying; 9 integration and 3 API tests for generation, rescheduling, cancellation, delivery, and validation; 1 bUnit test for the repeat indicator; 1 Playwright journey for completing a recurring task.

## Decisions

- The next occurrence is created on the transition to `Done`, not on a timer. The product model stays event-driven, and the demo works without a scheduler process.
- `RecurrenceGenerated` makes generation idempotent per source occurrence. Reopening and completing again is a no-op, while changing the due date resets the guard so the moved occurrence can generate once more.
- The next occurrence copies content and reminder settings but resets checklist completion. A recurring task should carry its structure forward without inheriting yesterday's progress.
- The outbox reuses the existing `Reminder` aggregate (`Status`, `Attempts`, `LastError`) instead of a new table. The aggregate already had the lifecycle the dispatcher needs.
- Delivery is at-least-once: a reminder is marked `Sent` only after the sender succeeds, so a crash between send and save can resend. Failures are terminal in this phase; retry and backoff arrive with the worker split.
- The dispatcher runs inside the web process for the MVP. Moving it to a worker later changes only the host that registers it.
- Email templates use a direct `ResourceManager` over the embedded `.resx`, because `IStringLocalizer` did not resolve the Infrastructure assembly's resources and returned the key instead of the translated text.
- Date-only tasks remind at 09:00 local rather than midnight; that matches how people read a "due today" task and avoids a useless 00:00 email.

## Fixed after E2E

- `ReminderDeliveryService` initially injected `IStringLocalizer<ReminderEmailResources>`; the integration assertion received `ReminderSubject` instead of a subject line. The service now reads the embedded resources through `ResourceManager` with the recipient's culture.
- The seeded recurring task was due today, so the calendar journey saw an extra task in today's cell. The seed now places it tomorrow, which also demonstrates an upcoming recurring occurrence.
- The bUnit repeat assertion now counts matching elements instead of using `ShouldContain` on a collection of elements, which did not check the tooltip attribute as intended.

## Metrics

- Build: passing with 0 warnings and 0 errors in Debug and Release.
- Tests: 47 unit, 45 integration, 32 component, and 12 E2E tests passing (136 total).
- Smoke checks: with the dispatcher running in Development, Mailpit received the English "Reminder: Write portfolio case study" and the Russian "Напоминание: …" for the same task after switching the demo user's culture. The recurrence controls and repeat icons render in English ("Every week", "Repeat every", "Repeat until") and Russian ("Каждую неделю", "Повторять каждые", "Повторять до").

## Next

- Week 8: import/export, the full E2E pass, accessibility review, and the v1.0 release.
- Publish the repository and confirm the first GitHub Actions run.
- Choose a hosting target and a production SMTP provider before the v1.0 deployment.
