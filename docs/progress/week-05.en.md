# Week 5 — Kanban board

Dates: 2026-11-30 - 2026-12-06

Status: Code complete; all suites green

## Goals

- Manual ordering for tasks inside a workflow column.
- A board that groups tasks into `Todo`, `InProgress`, and `Done` columns.
- Drag-and-drop between and inside columns, plus an accessible keyboard-friendly alternative.
- Integration, component, and E2E coverage for moving cards and persisting order.

## Done

- `MoveTaskRequest` and `MoveTaskRequestValidator`: the target status must be a known enum value and the target index cannot be negative.
- `TaskService.MoveAsync` moves one task, renumbers only the affected source and target columns, and saves them in one `SaveChangesAsync`.
- `TodoTask.SetSortOrder` ignores a call that would write the current value, so renumbering does not mark every card as modified.
- `TaskBoard` and `TaskBoardColumn` DTOs, plus `TaskQueries.BoardAsync`, which orders by `SortOrder` then `CreatedAt` and groups tasks into the three status columns.
- `ListAsync` and `BoardAsync` share one `ToListItem` projection expression, so the board and the list cannot drift apart.
- `/board` page with loading, refreshing, and error states, and a link between the board and the task list.
- `KanbanBoard` presentational component with HTML5 drag-and-drop, a drag-over highlight, per-column counts, and an empty-column placeholder.
- Card menu built on native `<details>`/`<summary>`: move up, move down, and move to each other column, with disabled items at the column edges.
- Edit and delete actions on each card, reusing the existing task dialog and confirmation flow.
- Board grid styled in `app.css`, stacking the columns below 960px, and a small `board.js` helper that seeds the drag payload.
- Board navigation entry, `Board`/`BoardTitle`/`BoardHint`/`MoveTo*` resource keys in English and Russian.
- `GET /api/v1/tasks/board` returns the ordered columns, and `POST /api/v1/tasks/{id}/move` moves a card for API clients.
- 6 integration tests for cross-column moves, in-column reordering, index clamping, cross-user access, validation, and the board API.
- 8 bUnit tests for the board's loading, error, column, metadata, empty, menu, drag, and action states.
- 2 Playwright journeys: moving a card with the menu and dragging a card to another column, both verified after a reload.

## Decisions

- Ordering is owned by the query: `BoardAsync` sorts by `SortOrder` and `TaskListItem` stays unchanged, so the list page and the `/api/v1` contract do not move.
- A move renumbers only the source and target columns; other columns keep their order and stay untouched in the change tracker.
- The client sends a target index, and the server clamps it to the target column size instead of failing a drag that landed past the last card.
- The board deliberately ignores the list filters and views: a filtered subset makes manual ordering meaningless, and the task list already covers filtering.
- The card menu uses native `<details>`/`<summary>` instead of `MudMenu`; it needs no popover provider, works with keyboard and screen readers, and is easy to exercise in bUnit.
- The component is named `KanbanBoard` because `TaskBoard` is already the Application DTO name.
- E2E contexts now use a 1280x800 viewport; the default Playwright viewport stacks the columns, and a stacked layout makes cross-column drag-and-drop unreliable.

## Fixed after E2E

- The board interaction test waited for the `_bl_*` handler marker, but cards rendered after enhanced navigation never carry it. Board tests now wait for the card to be visible; the click happens on an already-interactive page, so the marker is not needed.
- Dragging worked manually at desktop width but failed in E2E because the default viewport stacked the columns and the drop target sat far below the fold. The fixture now pins a desktop viewport.

## Metrics

- Build: passing with 0 warnings and 0 errors in Debug and Release.
- Tests: 35 unit, 30 integration, 22 component, and 8 E2E tests passing (95 total).
- Smoke checks: the board renders three columns with the demo data, the card menu moves cards, and drag-and-drop persists after a reload.

## Next

- Week 6: calendar month view with due-date placement and create-on-click.
- Publish the repository and confirm the first GitHub Actions run.
- Pick a hosting target and a production SMTP provider before the MVP deployment.
