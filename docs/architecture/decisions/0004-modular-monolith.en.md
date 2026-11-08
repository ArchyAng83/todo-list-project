# ADR-0004: Modular monolith without MediatR/AutoMapper

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The project is a solo, 6–8 week build with a strong emphasis on readable, controllable code. Pipeline-heavy abstractions such as MediatR and convention-based mapping add indirection that is hard to justify for this size.

## Decision

Use a **modular monolith** with four production projects and feature-oriented Application services. Commands and queries are ordinary service methods. Mapping is manual. Validation uses FluentValidation. No MediatR, no AutoMapper, no generic repository layer.

## Consequences

- The execution path is explicit and easy to debug.
- Fewer third-party dependencies and no licensing surprises.
- More mapping code than with AutoMapper; this is intentional.
- Application services can grow large, so they are split into commands (`TaskService`) and queries (`TaskQueries`) per feature.
- The same services are reused by Blazor and the API, keeping behavior consistent.
