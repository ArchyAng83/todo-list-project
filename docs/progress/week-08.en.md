# Week 8 — Import/export, E2E coverage, accessibility, and v1.0

Dates: 2026-11-09

Status: Code complete; all suites green

## Goals

- JSON import/export with a versioned schema and CSV export for tasks.
- A `/data` page with browser downloads, a file picker, and loading/error/empty states.
- API endpoints for export and import under `/api/v1/data`.
- Full E2E pass covering export, CSV BOM, JSON import, and merged data.
- Accessibility review: skip link, main landmark, localized labels, focus styles.
- Release v1.0: version bump, changelog, docs, tag, and push.

## Done

- `ExportDocument` (version 1) carries projects, tags, tasks, and nested checklist items with all schedules: titles, notes, status, priority, due date/time, reminder offset, recurrence, project, sort order, tags, checklist items with completion, and timestamps.
- `TaskCsvExport.Build` renders the CSV: UTF-8 BOM, quoted fields with doubled quotes, CRLF line endings, inline tag and checklist columns, ISO-8601 dates, and invariant formatting.
- `DataTransferService` on the Application layer exports the signed-in user's data and imports a JSON document with merge semantics:
  - projects and tags are reused by name (case-insensitive, trimmed), tasks are always created as new records owned by the current user, and imported IDs are mapped to existing or new IDs;
  - a missing project or tag reference is a validation failure and nothing is saved;
  - `CreatedAt` is reset to the import moment and `CompletedAt` follows the imported status;
  - no next occurrence is generated for recurring tasks; reminder outbox is reconciled for each imported task;
  - an unsupported export version is rejected, and failed imports are atomic.
- `/data` page with export JSON/CSV buttons, a JSON-only file picker, import progress indicator, an `aria-live` result region, loading/error/empty states, and a per-user summary of projects/tags/tasks.
- `data.js` helper for client-side blob downloads with a suggested filename.
- Data endpoints under `/api/v1/data`: `GET /export` (versioned JSON), `GET /export/csv`, and `POST /import` (returns `ImportSummary`); all enforce bearer authorization and 404-scope the data to the owner.
- Unit tests for CSV escaping, BOM/CRLF, date/time formats, enum rendering, and schema version; integration tests for round-trip structure, reuse-by-name, owner scoping, version and foreign-key validation, no-next-occurrence after import; E2E journeys for JSON/CSV download and for importing a file and verifying the task appears in the list.
- Accessibility additions: skip link to the main landmark, `id="main-content"` on the page region, localized menu label and navigation `aria-label`, `:focus-visible` styles, and an `aria-live="polite"` import-result region.
- RU/EN resource keys for the page, buttons, hints, validation messages, and import summary.
- Build: passing with 0 warnings and 0 errors in Debug and Release.
- Tests: 52 unit, 57 integration, 32 component, and 14 E2E tests passing (155 total). Smoke test: the demo user exports a JSON file whose root `version` is 1, downloads the CSV with a valid UTF-8 BOM, imports it via the file picker, and sees the imported task in the task list in both English and Russian.

## Decisions

- The export document is versioned by an explicit `Version` field with `CurrentVersion = 1`. Schema evolution stays safe because the importer rejects unknown versions instead of silently guessing.
- Merge-style import reuses projects and tags by name rather than by ID. Cross-user ID collision would be surprising in a portable backup, while a name collision is what a human expects when restoring their own file.
- Checklist items are re-created (titles only) in their imported order with completion reset to incomplete. The new task must be actionable, and importing raw IDs would couple the user's IDs to another export.
- Reminder outbox is reconciled on import so reminders with offsets are scheduled even though no UI created the task. Completed tasks do not generate a next occurrence and do not add a reminder.
- The JSON export is written indented with enums as names so a human can inspect it; the CSV is designed for spreadsheets and deliberately flat.

## Fixed after tests

- `TodoTask.CreateNextOccurrence` had an irregular one-line body; restored to a normal block form.
- E2E helper `WaitForBlazorAsync` was made shared for the data page instead of repeating the `button[type=submit]` selector, since the data page uses download buttons rather than a submit button.

## Metrics

- Coverage is now unit, integration, component, and E2E for the import/export path; every existing path remains covered, and the full suite (155 tests) passes in Release.

## Next

- Choose a hosting target and a production SMTP provider before the v1.0 deployment.
- Create a GitHub Release after the `v1.0.0` tag is pushed (GitHub CLI is not installed on the machine; a tag can still be pushed directly).

## Follow-up: v1.0.1 deployment hardening

- The repository is deployment-ready without a host or SMTP account: Data Protection keys persist on a volume, forwarded headers are opt-in with an explicit trust list, auth and antiforgery cookies are secure outside Development, and startup warns about an unpersisted key ring or a missing SMTP host.
- `Dockerfile.migrations` and `docker-compose.production.yml` package the schema step and the runtime stack; `.env.production.example` documents the configuration and ADR 0006 records the shape.
- The full suite is now 176 tests (54 unit / 72 integration / 32 component / 14 E2E).
