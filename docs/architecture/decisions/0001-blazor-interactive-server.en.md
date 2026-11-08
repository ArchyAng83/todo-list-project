# ADR-0001: Blazor Interactive Server with static SSR auth pages

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The UI must be interactive, fast to build, and compatible with ASP.NET Core Identity cookie authentication. Blazor WebAssembly would require a separate API boundary, token handling, and more infrastructure. Blazor Server keeps one process and direct access to Application services.

Cookie authentication cannot be performed inside a SignalR circuit: the `Set-Cookie` header must be written during an HTTP response.

## Decision

Use a **Blazor Web App with Interactive Server** render mode for application pages. Authentication pages (login, register, password reset, email confirmation) are **static SSR** and are excluded from interactive routing. They call `SignInManager`/`UserManager` during the HTTP request and then redirect.

## Consequences

- One deployable process and one codebase.
- Cookie authentication follows the standard ASP.NET Core flow.
- Auth forms use server-side validation and full-page navigation; the rest of the app is interactive.
- Scaling Blazor Server later requires sticky sessions or a SignalR backplane; v1.0 runs as a single instance.
