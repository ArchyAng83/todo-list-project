# ADR-0001: Blazor Interactive Server и static SSR auth-страницы

- **Статус:** принято
- **Дата:** 2026-11-08

## Контекст

UI должен быть интерактивным, быстрым в разработке и совместимым с cookie-аутентификацией ASP.NET Core Identity. Blazor WebAssembly потребовал бы отдельной границы API, работы с токенами и большей инфраструктуры. Blazor Server оставляет один процесс и прямой доступ к Application-сервисам.

Cookie-аутентификацию нельзя выполнить внутри SignalR-circuit: заголовок `Set-Cookie` должен быть записан в HTTP-ответе.

## Решение

Используем **Blazor Web App с Interactive Server** для страниц приложения. Страницы аутентификации (login, register, password reset, email confirmation) — **static SSR** и исключены из интерактивного роутинга. Они вызывают `SignInManager`/`UserManager` во время HTTP-запроса и затем делают redirect.

## Последствия

- Один deployable процесс и одна кодовая база.
- Cookie-аутентификация идёт по стандартному потоку ASP.NET Core.
- Auth-формы используют server-side валидацию и полную навигацию; остальное приложение интерактивно.
- Масштабирование Blazor Server позже потребует sticky sessions или SignalR backplane; v1.0 работает одним инстансом.
