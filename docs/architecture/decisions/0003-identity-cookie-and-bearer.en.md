# ADR-0003: Identity cookie and bearer-only API

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The Blazor UI needs cookie authentication. External API clients need tokens. Mixing cookie and bearer authentication on the same endpoints creates CSRF and authorization ambiguity.

## Decision

Use **ASP.NET Core Identity** with two separate access paths:

- Blazor uses the **application cookie**.
- `/api/v1` accepts **bearer tokens only**, issued by the Identity API endpoints.
- Blazor calls Application services directly and never calls `/api/v1`.

## Consequences

- Clear separation between browser and API authentication.
- No CSRF exposure on the API because cookies are not accepted there.
- API clients use access and refresh tokens with standard Identity flows.
- Two authentication schemes must be configured explicitly; this is covered by integration tests.
- The API becomes a clean seam for future AI tools and integrations.
