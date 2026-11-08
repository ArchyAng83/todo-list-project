# Coding Conventions

## C#

- Nullable reference types enabled; warnings are errors.
- File-scoped namespaces, one public type per file.
- `async`/`await` everywhere; append `Async` to asynchronous methods.
- Accept and forward `CancellationToken` in Application, Infrastructure, and Web entry points.
- Prefer explicit types when they improve readability; use `var` for obvious assignments.
- No `.Result` or `.Wait()`.

## Domain and Application

- Domain methods protect invariants; avoid public setters for mutable state.
- Application services are split into commands (`TaskService`) and queries (`TaskQueries`).
- Return `Result<T>` for expected failures; throw for unexpected failures.
- `UserContext` is passed explicitly; never read the current user from a static or ambient context.
- One `SaveChangesAsync` per use case; explicit transactions only for multi-step operations.

## EF Core

- Use `AsNoTracking` for read queries and projections into DTOs.
- Put `IEntityTypeConfiguration<T>` classes in Infrastructure.
- Filter by `UserId` in every user-scoped query; cross-user access returns 404.
- Use `Guid.CreateVersion7()` for new aggregate IDs.
- Use PostgreSQL `xmin` for optimistic concurrency.
- Migrations are reviewed and never edited after being applied.

## Blazor

- App pages use Interactive Server; auth pages use static SSR.
- Use `IDbContextFactory` in components, never a long-lived scoped `DbContext`.
- Forms use `EditForm` and the same FluentValidation validators as the API.
- Show loading, empty, and error states for every data-driven page.
- Keep components small; extract reusable UI into `Components/`.

## Tests

- Name tests `Method_State_ExpectedBehavior`.
- Prefer builders and fakes over large setup blocks.
- Integration tests use real PostgreSQL, not the in-memory provider.
- No network calls in unit tests.

## Language

- Code, identifiers, XML docs, commit messages, and English docs are in English.
- UI and Russian docs are bilingual.
