<#
.SYNOPSIS
Restores a TodoList PostgreSQL dump into a scratch database.

.DESCRIPTION
Copies a pg_dump custom-format file into the running PostgreSQL container,
recreates the target database, restores the dump, and prints a row-count
sanity check. Restoring into a scratch database lets you verify a backup
without touching production data.

Never point the target at the live database. The default is todolist_restore.

.PARAMETER BackupFile
Path to a .dump file produced by backup-postgres.ps1.

.PARAMETER TargetDatabase
Scratch database that receives the restore. Must be a valid PostgreSQL
identifier.

.PARAMETER ComposeFile
Compose file that defines the running stack.

.PARAMETER EnvFile
Optional compose env file. Pass an empty string for the development stack.

.PARAMETER Force
Replace the target database if it already exists.

.EXAMPLE
pwsh ./scripts/restore-postgres.ps1 `
  -BackupFile D:\backups\todolist\todolist-20261112-120000.dump `
  -ComposeFile docker-compose.production.yml `
  -EnvFile .env.production `
  -Force
#>
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string]$TargetDatabase = "todolist_restore",
    [string]$ComposeFile = "docker-compose.production.yml",
    [string]$EnvFile = ".env.production",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $BackupFile)) {
    throw "Backup file '$BackupFile' was not found."
}

$BackupFile = [System.IO.Path]::GetFullPath($BackupFile)

$composeArgs = @("compose")
if (-not [string]::IsNullOrWhiteSpace($EnvFile)) {
    if (-not (Test-Path -LiteralPath $EnvFile)) {
        throw "Compose env file '$EnvFile' was not found. Pass -EnvFile '' for the development stack."
    }

    $composeArgs += @("--env-file", $EnvFile)
}

$composeArgs += @("-f", $ComposeFile)

function Invoke-Compose {
    param([string[]]$Arguments)
    & docker @composeArgs @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE."
    }
}

$postgresId = (Invoke-Compose @("ps", "-q", "postgres") | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($postgresId)) {
    throw "The compose PostgreSQL service is not running."
}

$containerDump = "/tmp/todolist-restore.dump"

Write-Host "==> Copying $BackupFile into the PostgreSQL container"
& docker cp $BackupFile "${postgresId}:$containerDump"
if ($LASTEXITCODE -ne 0) {
    throw "docker cp failed with exit code $LASTEXITCODE."
}

try {
    $targetExists = (Invoke-Compose @(
        "exec", "-T", "-e", "TARGET_DB=$TargetDatabase", "postgres", "sh", "-c",
        'psql -U "$POSTGRES_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = ''$TARGET_DB''"'
    ) | Out-String).Trim()

    if ($targetExists -eq "1" -and -not $Force) {
        throw "Database '$TargetDatabase' already exists. Pass -Force to replace it."
    }

    Write-Host "==> Recreating $TargetDatabase"
    Invoke-Compose @(
        "exec", "-T", "-e", "TARGET_DB=$TargetDatabase", "postgres", "sh", "-c",
        'dropdb -U "$POSTGRES_USER" --if-exists --force "$TARGET_DB"'
    ) | Out-Null

    Invoke-Compose @(
        "exec", "-T", "-e", "TARGET_DB=$TargetDatabase", "postgres", "sh", "-c",
        'createdb -U "$POSTGRES_USER" "$TARGET_DB"'
    ) | Out-Null

    Write-Host "==> Restoring into $TargetDatabase"
    Invoke-Compose @(
        "exec", "-T", "-e", "TARGET_DB=$TargetDatabase", "postgres", "sh", "-c",
        'pg_restore -U "$POSTGRES_USER" -d "$TARGET_DB" --no-owner --no-acl --clean --if-exists /tmp/todolist-restore.dump'
    ) | Out-Null

    Write-Host "==> Verifying restore"
    $userCount = (Invoke-Compose @(
        "exec", "-T", "-e", "TARGET_DB=$TargetDatabase", "postgres", "sh", "-c",
        'psql -U "$POSTGRES_USER" -d "$TARGET_DB" -tAc "SELECT count(*) FROM users"'
    ) | Out-String).Trim()

    if ([string]::IsNullOrWhiteSpace($userCount)) {
        throw "The restored database did not answer the users count."
    }

    Write-Host "    users: $userCount"
}
finally {
    Invoke-Compose @("exec", "-T", "postgres", "rm", "-f", $containerDump) | Out-Null
}

Write-Host ""
Write-Host "Restore complete: $TargetDatabase"
Write-Host "Drop the scratch database when the drill is done with:"
Write-Host "  dropdb -U `$POSTGRES_USER --if-exists --force $TargetDatabase"
