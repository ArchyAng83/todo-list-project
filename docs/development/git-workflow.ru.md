# Git Workflow

## Модель

Trunk-based development с короткоживущими ветками. `main` всегда готов к релизу.

- `main` защищён: только pull requests, CI должен проходить.
- Feature-ветки короткие (желательно 1–3 дня).
- Merge через **squash**, чтобы история была чистой.
- После merge ветку удаляем.

## Имена веток

```text
feat/task-editor
fix/reminder-timezone
docs/week-02
chore/ci-cache
test/task-isolation
```

## Commit messages

Используем [Conventional Commits](https://www.conventionalcommits.org/):

```text
feat(tasks): add due-date filters
fix(auth): redirect to login after cookie expiry
test(tasks): cover cross-user access
docs(adr): add time model decision
chore(ci): cache NuGet packages
```

Разрешённые типы: `feat`, `fix`, `test`, `docs`, `chore`, `refactor`, `perf`, `build`, `ci`.

## Pull requests

- Маленькие и сфокусированные.
- В описании: что изменилось, почему и как тестировалось.
- CI должен быть зелёным.
- Минимум один self-review перед merge.
- Обновлять `CHANGELOG.md` и progress docs, если изменение user-visible или milestone-level.

## Релизы

Релизы тегируются на `main` как `v0.x.y`. Перед тегом обновить `CHANGELOG.md`.
