# Documentation Policy

## Language

Documentation is bilingual. **English is canonical**; Russian is a synchronized translation.

- File pairs live side by side: `overview.en.md` and `overview.ru.md`.
- Every document starts with a language switcher: `English | Русский`.
- English docs are updated first, in the same pull request as the change.
- Russian translations are updated in the same pull request for significant changes; minor edits may be synced during the weekly review.
- If a translation lags, add `> Translation status: outdated` at the top of the Russian file and update the sync date after translating.
- Code, identifiers, XML docs, commit messages, and `CHANGELOG.md` are in English.
- UI strings are localized through `.resx` resources for RU and EN.

## Structure

```text
docs/
├─ README.{en,ru}.md
├─ product/
├─ architecture/
│  ├─ overview.{en,ru}.md
│  ├─ data-model.{en,ru}.md
│  └─ decisions/0001-...{en,ru}.md
├─ development/
├─ operations/
├─ api/
├─ progress/
│  ├─ status.{en,ru}.md
│  └─ week-XX.{en,ru}.md
└─ demo.{en,ru}.md
```

Directories and documents are added when the corresponding phase starts; empty placeholder files are avoided.

## ADR template

```markdown
# ADR-XXXX: Title

- **Status:** Proposed | Accepted | Superseded
- **Date:** YYYY-MM-DD

## Context

## Decision

## Consequences
```

## Progress tracking

- `status` is the current snapshot: phase, done, in progress, next, blockers, quality gates.
- `week-XX` is the weekly log: goals, results, decisions, metrics, carry-over.
- Update at the end of every work session and at the end of the week.
- User-visible changes and releases are recorded in `CHANGELOG.md`.

## Review checklist

- English and Russian files exist as a pair.
- Language switcher points to the correct file.
- Links resolve.
- The document matches the current code and status.
