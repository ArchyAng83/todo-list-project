# Post-deploy smoke test

`scripts/smoke-test.ps1` exercises the public surface of a running instance. It is deliberately small: it catches broken configuration, a database that is not reachable through the app, and a broken read/write path after a deploy.

## What it checks

1. `/health/live` and `/health/ready` answer `200`.
2. The landing page returns the hardening headers (`X-Content-Type-Options` and a Content Security Policy).
3. Bearer login with an existing account.
4. Task create, read, board, and calendar queries.
5. JSON export returns a non-empty document.
6. The created task is deleted (unless `-SkipDelete` is set).

## Run it

```powershell
pwsh ./scripts/smoke-test.ps1 `
  -BaseUrl https://todo.example.com `
  -Email smoke@example.com `
  -Password 'change-me'
```

The account must already exist. Create a dedicated smoke account during provisioning, or use the demo account when running against a disposable development host.

The script exits non-zero on the first failed step and prints the failing HTTP status. It does not send email, so it also works before an SMTP provider is configured.

## Local and CI runs

Against the disposable compose stack:

```bash
docker compose --env-file .env.smoke \
  -f docker-compose.production.yml \
  -f docker-compose.smoke.yml up -d

pwsh ./scripts/smoke-test.ps1 \
  -BaseUrl http://localhost:8080 \
  -Email demo@todolist.local \
  -Password DemoPass123
```

`docker-compose.smoke.yml` starts the production image in Development so the demo seeder creates that account, and disables the reminder dispatcher. CI runs the same sequence in the `container` job after building both images, so a broken image or migration is caught before a release.

## After a real deploy

Run the script from a machine that can reach the public URL, then check the rest of the go-live checklist in `docs/development/deployment.en.md`. The smoke test does not replace a browser check of Blazor interactivity; the Playwright journeys cover that in CI.
