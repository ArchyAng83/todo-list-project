# Git Workflow

## Model

Trunk-based development with short-lived branches. `main` is always releasable.

- `main` is protected: pull requests only, CI must pass.
- Feature branches are short-lived (ideally 1–3 days).
- Merge with **squash** to keep a clean history.
- Delete branches after merge.

## Branch names

```text
feat/task-editor
fix/reminder-timezone
docs/week-02
chore/ci-cache
test/task-isolation
```

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```text
feat(tasks): add due-date filters
fix(auth): redirect to login after cookie expiry
test(tasks): cover cross-user access
docs(adr): add time model decision
chore(ci): cache NuGet packages
```

Allowed types: `feat`, `fix`, `test`, `docs`, `chore`, `refactor`, `perf`, `build`, `ci`.

## Pull requests

- Small and focused.
- Description includes what changed, why, and how it was tested.
- CI must be green.
- At least one self-review pass before merge.
- Update `CHANGELOG.md` and progress docs when the change is user-visible or milestone-level.

## Releases

Tag releases on `main` as `v0.x.y`. Update `CHANGELOG.md` before tagging.
