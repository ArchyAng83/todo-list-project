# ADR-0007: Reminder dispatch claim

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

`ReminderDispatcher` polls the `reminders` outbox and sends email in-process. The original MVP assumed a single app replica because `ReminderDeliveryService` selected due rows without any claim: two replicas could read the same `Pending` reminder and send it twice.

The app should be able to run more than one replica without duplicating email, but a full outbox claim (`Processing` status, `FOR UPDATE SKIP LOCKED`, recovery of stale claims) adds schema and recovery logic that is disproportionate for the current volume.

## Decision

Serialize reminder dispatch with a PostgreSQL **session-level advisory lock**:

- `ReminderDispatchLock` opens the `AppDbContext` connection and calls `pg_try_advisory_lock` with the stable key `0x546F646F4C697374` (`TodoList` as ASCII).
- The dispatcher holds the lock for one poll. A second replica gets `false` and skips the poll instead of reading due rows.
- Disposing the lock calls `pg_advisory_unlock`; closing the connection or crashing the process releases it automatically.
- The lock is acquired before the due-row query, so selection and sending happen inside the critical section.
- Retention cleanup does not use the lock: it only deletes terminal rows and is idempotent.

The schema and the reminder status machine stay unchanged. The lock key is an application constant and must never be reused for another feature.

## Alternatives considered

- **`FOR UPDATE SKIP LOCKED` plus a `Processing` status.** Correct and more granular, but it needs a claim timestamp, a stale-claim timeout, and recovery for a replica that dies mid-send. Rejected for now; it remains the natural next step if email volume requires parallel senders.
- **One replica forever.** Simple, but blocks scaling for reasons unrelated to Blazor circuits and leaves a single point of failure for reminder delivery.
- **A leader-election table.** More moving parts than a built-in PostgreSQL primitive.

## Consequences

- Several app replicas can run the dispatcher safely; only one sends per poll.
- Blazor Server still keeps circuits in memory, so the UI needs sticky sessions before it can scale horizontally. The reminder constraint is no longer the reason to keep one replica.
- A slow SMTP call holds the lock for the batch. This is acceptable for a single batch dispatcher; keep `Reminders:BatchSize` modest and the SMTP timeout finite.
- The lock is scoped to one database. Two stacks sharing a database serialize against each other, which is the desired behaviour.
- `pg_locks` shows the advisory lock, which makes a stuck dispatcher diagnosable without application logs.
- `ReminderDispatchLock` and the dispatcher are covered by integration tests that acquire the lock from two DI scopes and by the existing delivery tests.
