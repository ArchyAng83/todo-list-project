# Week 6 — Calendar month view

Dates: 2026-12-07 - 2026-12-13

Status: Code complete; all suites green

## Goals

- A month calendar at `/calendar` that places tasks on their due date.
- Navigation between months, a "today" shortcut, and the user's persisted week start.
- Create a task by clicking a day, prefilled with that date.
- Integration, component, and E2E coverage for placing, creating, and navigating.

## Done

- `CreateTaskRequest` and `CreateTaskBody` gain `Priority`, `DueDate`, and `DueTime` as optional append-only parameters, so existing callers keep compiling.
- `CreateTaskRequestValidator` rejects an unknown priority and a due time without a due date.
- `TaskService.CreateAsync` applies priority and due date/time through the existing aggregate methods.
- `TaskQueries.CalendarAsync` returns the tasks whose `DueDate` falls inside an inclusive range, ordered by day, then time, then status, then priority.
- `ListAsync`, `BoardAsync`, and `CalendarAsync` share one `ToListItem` projection, so the list, board, calendar, and API cannot drift apart.
- `GET /api/v1/tasks/calendar?from=&to=` returns the range for API clients and responds with `400` when `from` is after `to`.
- `/calendar` page with loading, error, and retry states, month navigation, a "today" button, and links to the task list.
- The page reads `UserSettings.WeekStart` and requests exactly the visible 42-day window, including the adjacent days that fill the grid.
- `CalendarGrid` presentational component: culture-aware weekday headers, outside-month styling, a today marker, and a 42-cell grid computed from the configured week start.
- Up to three task chips per day, sorted by the query, with an "and N more" counter and the full title in a tooltip.
- Done tasks render struck through, and unfinished tasks before today get an overdue accent.
- Day cells are keyboard reachable: `Enter` and `Space` open the create dialog, and task chips stop propagation so editing a task never creates another one.
- `TaskCreateDialog` prefilled with the clicked date, plus project, priority, due date, and due time.
- Calendar navigation entry, `Calendar`/`CalendarTitle`/`CalendarHint`/`NewTask`/`Today`/`PreviousMonth`/`NextMonth`/`MoreTasks` resource keys in English and Russian.
- Calendar grid and chip styles in `app.css`, including a compact layout below 700px.
- 1 integration test for range filtering, ordering, scoping, and the inverted range; 2 API tests for the calendar endpoint.
- 9 bUnit tests for loading, error/retry, Monday and Sunday week starts, task placement and styles, overflow count, day click, task click, and month controls.
- 3 Playwright journeys: seeded tasks on their due dates, month navigation, and create-on-click verified after a reload.

## Decisions

- `CalendarAsync` returns `TaskListItem`, so the calendar, the list, the board, and `/api/v1` keep one task shape and the API stays additive.
- The server returns a flat ordered list for the range; the UI owns the 42-day grid, because the grid depends on the persisted week start, not on the task data.
- The page requests the full grid window instead of only the current month, so tasks in the leading and trailing days are not silently missing.
- `CreateTaskRequest` grows optional parameters at the end instead of a new request type; every existing call site and API client remains valid.
- A day cell is the create target, while a task chip is an edit target. `@onclick:stopPropagation` on the chip keeps the two actions unambiguous.
- The grid shows three chips per day and collapses the rest into a counter; a month cell is too small to scroll comfortably, and the tooltip keeps the full title accessible.
- Month and weekday names use `CultureInfo.CurrentCulture`; CSS capitalizes the month heading so Russian lowercase month names still read as a title.

## Fixed after E2E

- `ComponentTestContext` set only `CurrentUICulture`, while the calendar formats month and weekday names from `CurrentCulture`. On a Russian machine the bUnit assertions compared English expectations with Russian output. The context now pins both cultures to `en`.
- The calendar E2E create test clicks the day number rather than the cell center, because the center can land on an existing task chip and open the edit dialog instead of the create dialog.

## Metrics

- Build: passing with 0 warnings and 0 errors in Debug and Release.
- Tests: 35 unit, 33 integration, 31 component, and 11 E2E tests passing (110 total).
- Smoke checks: the calendar renders the demo data in English and Russian at 1280x800, month navigation works, and a task created by clicking a day appears, opens in the edit dialog, and deletes.

## Next

- Week 7: recurring tasks and email reminders with a database-backed outbox and dispatcher.
- Publish the repository and confirm the first GitHub Actions run.
- Pick a hosting target and a production SMTP provider before the MVP deployment.
