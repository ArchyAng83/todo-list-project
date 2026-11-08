# ADR-0002: PostgreSQL, EF Core, and IAppDbContext

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The project needs a free, production-friendly relational database, reliable migrations, good support for date/time types, and straightforward hosting. The Application layer needs persistence access without drowning in repository boilerplate.

## Decision

Use **PostgreSQL** with **EF Core 10 + Npgsql**. Application services depend on a thin `IAppDbContext` that exposes `DbSet<T>` and `SaveChangesAsync`. There are no generic repositories. Entity configurations, migrations, Identity stores, and provider-specific details live in Infrastructure.

## Consequences

- Full EF Core query power in Application services: projections, `ILike`, `AsNoTracking`, explicit includes.
- Fewer files and less indirection than repository-per-aggregate.
- Application references EF Core abstractions. This is an accepted tradeoff for a modular monolith.
- Integration tests run against real PostgreSQL via Testcontainers; provider differences are caught early.
- Repositories can be introduced per aggregate later if persistence isolation becomes necessary.
