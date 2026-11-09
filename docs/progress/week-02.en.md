# Week 2 — Core domain

Dates: 2026-11-09 — 2026-11-15

Status: Code complete; manual browser verification passed

## Goals

- Projects: create, edit, archive, unarchive, and delete.
- Tags: create, edit, delete, with usage counts.
- Tasks: create, edit, delete, status, priority, due date and time, reminder offset.
- Subtasks as checklist items with completion tracking.
- Task views: search, filters by status, project, and tag, plus quick add.

## Done

- Application layer split into command services (`ProjectService`, `TagService`, `TaskService`) and query services (`ProjectQueries`, `TagQueries`, `TaskQueries`).
- DTOs for lists and details: `ProjectListItem`, `TagListItem`, `TagRef`, `TaskListItem`, `TaskDetails`, `ChecklistItemDto`.
- Requests, FluentValidation validators, and `ValidationErrors` mapping to `Result<T>`.
- `CurrentUserProvider` resolves `UserContext` explicitly; Application services never read ambient state.
- `AddApplication()` registers services and validators; `Program.cs` wires the Web dependencies.
- Projects page with create, edit, archive toggle, archive visibility switch, and open/total task counts.
- Tags page with create, edit, delete, color preview, and task usage counts.
- Tasks page with quick add, debounced search, status/project/tag filters, clear filters, status toggle, edit, and delete.
- `TaskEditDialog` covers title, notes, status, project, priority, due date and time, reminder, multi-select tags, and checklist items.
- `ProjectEditDialog` and `TagEditDialog` with inline validation and hex color preview.
- Bilingual resources for every new UI string in English and Russian.
- Integration tests for project scoping/archiving, project task counts, tag duplicate conflicts, task update with checklist and tags, task filters, and cross-user 404 behavior.
- Manual browser verification of projects, tags, quick add, full task edit, search, tag filter, status toggle, archive/unarchive, and RU/EN switching.

## Decisions

- Command and query separation inside Application; no MediatR, no generic repositories, manual mapping only.
- Queries use `AsNoTracking` and project directly into DTOs.
- Blazor pages and dialogs create a DI scope per service operation through `IServiceScopeFactory`, so no scoped `DbContext` lives for the component lifetime.
- EF Core uses `QuerySplittingBehavior.SplitQuery` to avoid cartesian explosion when a task loads checklist items and tags.
- Checklist items and task-tag links are created through the `TodoTask` aggregate and added to the context explicitly before saving.
- Search uses `ToLower().Contains(...)` because `EF.Functions.ILike` is not available in Application.
- Cross-user access returns `not_found` (404 semantics) for every service operation.

## Metrics

- Build: passing with 0 warnings and 0 errors.
- Tests: 32 unit tests and 13 integration tests passing.
- Manual verification: projects, tags, task editing, search, filters, archive flow, and localization confirmed in the browser.

## Next

- Week 3: expanded search and filters, settings, demo seed, `/api/v1`, and OpenAPI.
