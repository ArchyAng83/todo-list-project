# ADR-0002: PostgreSQL, EF Core и IAppDbContext

- **Статус:** принято
- **Дата:** 2026-11-08

## Контекст

Проекту нужна бесплатная, production-friendly реляционная база, надёжные миграции, хорошая поддержка date/time типов и простое хостинг-развёртывание. Application-слою нужен доступ к персистентности без утопания в repository boilerplate.

## Решение

Используем **PostgreSQL** с **EF Core 10 + Npgsql**. Application-сервисы зависят от тонкого `IAppDbContext`, который отдаёт `DbSet<T>` и `SaveChangesAsync`. Generic-репозиториев нет. Конфигурации сущностей, миграции, Identity stores и provider-specific детали живут в Infrastructure.

## Последствия

- Полная мощь EF Core в Application-сервисах: projections, `ILike`, `AsNoTracking`, explicit includes.
- Меньше файлов и indirection, чем repository-per-aggregate.
- Application ссылается на EF Core abstractions. Это принятый tradeoff для модульного монолита.
- Integration-тесты идут против реального PostgreSQL через Testcontainers; различия провайдеров ловятся рано.
- Репозитории можно ввести по агрегатам позже, если понадобится изоляция персистентности.
