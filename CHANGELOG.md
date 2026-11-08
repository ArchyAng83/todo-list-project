# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial solution structure: Domain, Application, Infrastructure, Web, and test projects.
- Central package management, pinned .NET SDK, `.editorconfig`, and Git attributes.
- Docker Compose for PostgreSQL and Mailpit.
- Bilingual documentation skeleton and project progress tracking.
- GitHub Actions CI skeleton.
- Domain model for projects, tags, tasks, checklist items, task tags, and reminders.
- EF Core 10 + Npgsql persistence with snake_case naming, concurrency tokens, and the initial migration.
- ASP.NET Core Identity with cookie authentication and static SSR login, register, logout, and access-denied pages.
- MudBlazor application shell, public landing page, tasks placeholder, and profile settings page.
- RU/EN localization with a cookie-based culture switcher and `Accept-Language` fallback.
- Domain unit tests and Testcontainers-based integration tests for auth and persistence.
