# Backup and restore

PostgreSQL is the only source of truth for tasks, projects, tags, and users. The Data Protection key ring is the second stateful asset: restoring a database without its keys still forces every user to sign in again and invalidates outstanding bearer and refresh tokens.

Back up both together, store the copies off the host, and rehearse the restore regularly.

## What to back up

| Asset | Location | Why |
|---|---|---|
| PostgreSQL | `postgres-data` volume of the compose stack | All application data |
| Data Protection keys | `data-protection-keys` volume, `/app/keys` in the app container | Cookies, antiforgery tokens, bearer/refresh tokens |
| Configuration secrets | Platform secret store or `.env.production` | The database password and SMTP credentials are not in the database |

The scripts in `scripts/` back up the database and the key ring. Keep `.env.production` and the key archive in the same protected backup location, because they are equally sensitive.

## Create a backup

Run the script from the repository root on the host that runs the compose stack:

```powershell
pwsh ./scripts/backup-postgres.ps1 `
  -ComposeFile docker-compose.production.yml `
  -EnvFile .env.production `
  -OutputDirectory D:\backups\todolist `
  -RetentionDays 14
```

The script:

1. Runs `pg_dump -Fc` inside the `postgres` service.
2. Copies the dump to `OutputDirectory` as `todolist-<timestamp>.dump`.
3. Copies `/app/keys` from the `app` service and zips it as `keys-<timestamp>.zip`.
4. Deletes dumps and key archives older than `RetentionDays`.

Use `-SkipKeys` only when the platform already backs up the key volume.

For the local development stack, pass `-ComposeFile docker-compose.yml -EnvFile "" -SkipKeys`: there is no app service to copy keys from.

### Schedule

Run a daily backup from cron or the platform scheduler. Example for a Linux host:

```cron
15 2 * * * cd /srv/todolist && pwsh ./scripts/backup-postgres.ps1 -ComposeFile docker-compose.production.yml -EnvFile .env.production -OutputDirectory /srv/backups/todolist -RetentionDays 14 >> /var/log/todolist-backup.log 2>&1
```

Copy the output directory to object storage or another host. A backup that exists only on the database host is not a backup.

## Restore drill

Never restore into the live database while the app is running. Restore into a scratch database, verify it, and only then decide whether a live restore is required.

```powershell
pwsh ./scripts/restore-postgres.ps1 `
  -BackupFile D:\backups\todolist\todolist-20261112-020015.dump `
  -ComposeFile docker-compose.production.yml `
  -EnvFile .env.production `
  -TargetDatabase todolist_restore `
  -Force
```

The script recreates the target database, runs `pg_restore --no-owner --no-acl`, and prints the `users` row count. If the command fails before dropping anything, the live database is untouched.

Then verify:

1. Compare `users`, `tasks`, `projects`, and `tags` counts with the backup source.
2. Start a temporary app instance against the scratch database, or point the existing one at it through `ConnectionStrings__Default`.
3. Sign in and run `scripts/smoke-test.ps1` against that instance.
4. Drop the scratch database after the drill.

```bash
docker compose --env-file .env.production -f docker-compose.production.yml \
  exec -T postgres sh -c 'dropdb -U "$POSTGRES_USER" --if-exists --force todolist_restore'
```

## Restore the key ring

Stop the app before replacing keys. The volume name follows the compose project: `todolist-production_data-protection-keys`.

```bash
docker run --rm \
  -v todolist-production_data-protection-keys:/keys \
  -v /srv/backups/todolist:/backup:ro \
  alpine sh -c 'rm -rf /keys/* && cd /keys && unzip /backup/keys-20261112-020015.zip'
```

Start the app and verify that an existing cookie session still works. If the keys are lost, every user is signed out; that is recoverable, but schedule the key backup with the database so it does not happen during a restore.

## Live restore

For a real incident:

1. Stop the app (`docker compose ... stop app`) so no writes or reminder dispatches race the restore.
2. Take a safety backup of the current database if it is still reachable.
3. Restore the selected dump into the live database. `pg_restore --clean --if-exists` drops and recreates objects; use it only after the safety backup.
4. Restore the matching key ring if the host was rebuilt.
5. Start the app, check `/health/ready`, then run the smoke test.
6. Confirm that the reminders table contains no stuck `Pending` rows older than the outage window. The dispatcher will retry them after startup.

## Verification checklist

- [ ] The dump is non-empty and `pg_restore --list` can read it.
- [ ] The scratch restore reports the expected row counts.
- [ ] The key archive is in the same backup set as the dump.
- [ ] The restore scenario was rehearsed in the last quarter.
- [ ] Backups are encrypted and copied off the host.
- [ ] The retention window matches the agreed recovery point objective.
