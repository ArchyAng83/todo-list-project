# AGENTS.md

Project guidance for coding agents working in this repository. Read it before
changing code. Deeper explanations live in `docs/`; this file is the short,
non-negotiable version.

## Project

TodoList is a multi-user task manager built as a production-minded portfolio
project. It targets .NET 10, Blazor Web App (Interactive Server), and
PostgreSQL 17. The UI and documentation are bilingual (RU/EN). The product is
an MVP in progress; an AI layer is planned only after v1.0 and must not leak
into the domain now.

Current status and weekly goals: `docs/progress/status.en.md` and
`docs/progress/week-*.en.md`.

## Golden rules

- Keep the modular-monolith boundaries: `Domain <- Application <- Infrastructure <- Web`.
- No generic repositories, MediatR, or AutoMapper. Do not add them without an ADR.
- Scope every user-owned query by `UserId`; cross-user access returns 404.
- Never hardcode UI strings; add both English and Russian resources.
- Never edit an applied EF Core migration; add a new one.
- Add package versions to `Directory.Packages.props`, not to project files.
- Run build and the relevant tests before committing. Warnings are errors.
- Do not commit secrets. The dev database credentials in docs are local only.

## Commands

```bash
# Infrastructure (PostgreSQL on 5433, Mailpit on 1025/8025)
docker compose up -d

# Build
dotnet build TodoList.sln -c Debug

# Run the app (http://localhost:5122)
dotnet run --project src/TodoList.Web --launch-profile http

# Tests
dotnet test tests/TodoList.UnitTests/TodoList.UnitTests.csproj
dotnet test tests/TodoList.IntegrationTests/TodoList.IntegrationTests.csproj   # needs Docker
dotnet test TodoList.sln -c Release                                            # same as CI

# EF Core migrations (dotnet-ef 10.x installed globally)
dotnet ef migrations add <Name> \
  --project src/TodoList.Infrastructure \
  --startup-project src/TodoList.Web \
  --output-dir Persistence/Migrations
dotnet ef database update \
  --project src/TodoList.Infrastructure \
  --startup-project src/TodoList.Web
```

## Repository map

```text
src/
  TodoList.Domain/         entities, enums, invariants; no framework dependencies
  TodoList.Application/    use cases, DTOs, validators, IAppDbContext, Result/Error
  TodoList.Infrastructure/ EF Core, Identity, email, background jobs
  TodoList.Web/            Blazor UI, auth pages, composition root, future /api/v1
tests/
  TodoList.UnitTests/       domain and pure Application tests (xUnit + Shouldly)
  TodoList.IntegrationTests/ WebApplicationFactory + Testcontainers PostgreSQL
  TodoList.ComponentTests/  bUnit (wired in Week 4)
  TodoList.E2ETests/        Playwright .NET (wired in Week 4)
  TodoList.Testing/         shared fixtures, builders, fakes (not a test project)
docs/                       bilingual docs, ADRs, progress tracking
```

## Architecture invariants

- `Domain` has no references to Application, Infrastructure, or Web.
- `Application` references only `Domain` and abstractions it owns.
- `Infrastructure` implements Application abstractions (`IAppDbContext`, `IEmailSender`).
- `Web` is the composition root and may reference Application and Infrastructure.
- Application services are split into commands and queries. Return `Result<T>` for
  expected failures and throw for unexpected failures.
- Pass `UserContext` explicitly; never read the current user from a static or
  ambient context.
- Manual mapping only. Keep DTOs in Application, not Domain.
- Use one `SaveChangesAsync` per use case; explicit transactions only for
  multi-step operations.
- MVPs use hard deletes. Soft delete/trash is a later feature.

## Time, IDs, and data

- Due dates are local `DateOnly`/`TimeOnly`. Instants are UTC `DateTimeOffset`.
- Inject `TimeProvider`; never call `DateTime.Now`, `DateTime.UtcNow`, or
  `DateTimeOffset.UtcNow` directly in Domain or Application.
- New aggregate IDs use `Guid.CreateVersion7()`.
- Enums are persisted as strings (`HasConversion<string>()`).
- `TodoTask` uses PostgreSQL `xmin` as its concurrency token.
- `TodoTask`, `Project`, `Tag`, `ChecklistItem`, `TaskTag`, and `Reminder` are
  the current aggregates. Checklist items and task-tag links are owned by
  `TodoTask`.

## EF Core and the database

- PostgreSQL is published on host port **5433** because port 5432 is often taken
  by a locally installed PostgreSQL. Do not change this without updating docs.
- snake_case naming is applied globally via `EFCore.NamingConventions`; do not
  hand-write table or column names unless there is a reason.
- Identity tables are renamed to `users`, `roles`, `user_roles`, `user_claims`,
  `user_logins`, `user_tokens`, and `role_claims`.
- `IEntityTypeConfiguration<T>` classes live in
  `Infrastructure/Persistence/Configurations`.
- Use `AsNoTracking` for read queries and project into DTOs when possible.
- Development applies migrations on startup through `MigrateDatabaseAsync`.
  Production must apply them as a separate deploy step.
- Register the context through `IDbContextFactory` in components; never hold a
  long-lived scoped `DbContext` in a Blazor component.

## Blazor and UI

- App pages use Interactive Server. Auth pages use static SSR so Identity can set
  cookies during the HTTP response.
- `App.razor` selects the render mode with
  `HttpContext.AcceptsInteractiveRouting()`; auth pages opt out with
  `[ExcludeFromInteractiveRouting]`.
- Keep `public partial class Program;` in `Program.cs`; integration tests need it
  as the `WebApplicationFactory` entry point.
- Use MudBlazor components for the app shell and pages. Keep custom CSS in
  `wwwroot/app.css` or scoped `.razor.css` files.
- Every data-driven page must show loading, empty, and error states.
- Forms use `EditForm`. `[SupplyParameterFromForm]` properties must use
  `= default!` and be initialized with `Input ??= new();` in `OnInitialized`
  to avoid BL0008.
- The app has RU and EN cultures resolved from a cookie, then `Accept-Language`.

## Localization

- UI strings live in `src/TodoList.Web/Resources/SharedResource.resx` (English)
  and `SharedResource.ru.resx` (Russian).
- Add or change keys in both files in the same commit.
- Use `IStringLocalizer<SharedResource>`, never literal user-facing text.
- Code, identifiers, XML docs, commit messages, and English docs are English.
  UI text and Russian docs are translated.

## Testing

- Domain rule or pure Application logic -> unit test (xUnit + Shouldly).
- Auth, persistence, API, or Identity change -> integration test.
- Integration tests run against real PostgreSQL via Testcontainers and require
  Docker. Do not use the EF in-memory provider.
- Shared fixtures live in `TodoList.Testing`; test files live in the test
  projects. `[CollectionDefinition]` must be in the same assembly as the tests
  that use it (xUnit1041).
- Testcontainers 4.16 uses `new PostgreSqlBuilder("postgres:17-alpine")`; the
  parameterless builder is obsolete.
- Name tests `Method_State_ExpectedBehavior`.
- No network calls in unit tests. Replace `TimeProvider` and `IEmailSender`
  with fakes where time or email matters.
- bUnit and Playwright are wired in during Week 4; do not claim component or
  E2E coverage before that.

## Conventions

- Nullable reference types and implicit usings are enabled; warnings are errors.
- File-scoped namespaces, one public type per file, `sealed` by default.
- `async`/`await` everywhere; append `Async` to asynchronous methods; forward
  `CancellationToken`. No `.Result` or `.Wait()`.
- Four-space indentation. `.editorconfig` is the source of truth.
- Keep diffs focused. Do not perform drive-by refactors or reformat unrelated
  files.

## Documentation and git

- English docs are canonical; update the paired `.ru.md` file in the same change.
- When a milestone closes, update `docs/progress/status.*`,
  `docs/progress/week-*.md`, and `CHANGELOG.md`.
- Significant architectural decisions get an ADR in
  `docs/architecture/decisions/`.
- Work on `main`, though the repository may still be published later. Use
  Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `chore:`).
- `README.md` holds the public overview; do not let it drift from the docs.

## Known gotchas

- The app locks its DLLs on Windows. Stop the process on port 5122 before
  rebuilding if the build fails with file-in-use errors.
- Docker Desktop must be running for PostgreSQL, Mailpit, and integration tests.
- The SDK is pinned in `global.json` (10.0.400). A mismatch fails the build.
- `dotnet-ef` may print a version warning; it is harmless as long as migrations
  are generated correctly.
- `.playwright-mcp/` and `TestResults/` are ignored artifacts; do not commit them.
- If a build fails only in `TodoList.Testing`, make sure it stays
  `<IsTestProject>false</IsTestProject>`.

## Read more

- `docs/development/setup.en.md` — local setup and infrastructure.
- `docs/development/conventions.en.md` — coding conventions.
- `docs/development/testing.en.md` — test pyramid and commands.
- `docs/architecture/overview.en.md` — architecture and flows.
- `docs/architecture/decisions/` — ADRs.
- `docs/product/roadmap.en.md` — phased roadmap.
