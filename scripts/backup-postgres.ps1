<#
.SYNOPSIS
Backs up the TodoList PostgreSQL database and the Data Protection key ring.

.DESCRIPTION
Runs pg_dump in the compose PostgreSQL service, copies the custom-format dump
to the host, optionally archives the app's /app/keys directory, and prunes
backups older than the retention window.

.PARAMETER ComposeFile
Compose file that defines the running stack.

.PARAMETER EnvFile
Optional compose env file. Pass an empty string for the development compose
file, which has no secrets file.

.PARAMETER OutputDirectory
Directory that receives the dump and the key-ring archive.

.PARAMETER RetentionDays
Delete backup files older than this many days. Use 0 to keep everything.

.PARAMETER SkipKeys
Do not archive the Data Protection key ring. Only use this when keys are backed
up by the platform, because restoring a database without its keys logs every
user out.

.EXAMPLE
pwsh ./scripts/backup-postgres.ps1 `
  -ComposeFile docker-compose.production.yml `
  -EnvFile .env.production `
  -OutputDirectory D:\backups\todolist
#>
param(
    [string]$ComposeFile = "docker-compose.production.yml",
    [string]$EnvFile = ".env.production",
    [string]$OutputDirectory = "backups",
    [ValidateRange(0, 3650)][int]$RetentionDays = 14,
    [switch]$SkipKeys
)

$ErrorActionPreference = "Stop"

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

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dumpName = "todolist-$timestamp.dump"
$containerDump = "/tmp/$dumpName"
$dumpPath = Join-Path $OutputDirectory $dumpName

$postgresId = (Invoke-Compose @("ps", "-q", "postgres") | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($postgresId)) {
    throw "The compose PostgreSQL service is not running."
}

Write-Host "==> Dumping PostgreSQL to $dumpPath"
Invoke-Compose @(
    "exec", "-T", "postgres", "sh", "-c",
    "pg_dump -U `"`$POSTGRES_USER`" -d `"`$POSTGRES_DB`" -Fc -f $containerDump"
) | Out-Null

try {
    & docker cp "${postgresId}:$containerDump" $dumpPath
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp failed with exit code $LASTEXITCODE."
    }
}
finally {
    Invoke-Compose @("exec", "-T", "postgres", "rm", "-f", $containerDump) | Out-Null
}

$dump = Get-Item -LiteralPath $dumpPath
if ($dump.Length -le 0) {
    throw "The database dump is empty."
}

Write-Host "    $($dump.Length) bytes"

if (-not $SkipKeys) {
    $keysDirectory = Join-Path $OutputDirectory "keys-$timestamp"

    Write-Host "==> Copying the Data Protection key ring to $keysDirectory"
    Invoke-Compose @("cp", "app:/app/keys", $keysDirectory) | Out-Null

    $keysArchive = Join-Path $OutputDirectory "keys-$timestamp.zip"
    Compress-Archive -Path (Join-Path $keysDirectory "*") -DestinationPath $keysArchive -Force
    Remove-Item -LiteralPath $keysDirectory -Recurse -Force

    Write-Host "    $keysArchive"
}

if ($RetentionDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$RetentionDays)

    Get-ChildItem -LiteralPath $OutputDirectory -File |
        Where-Object { $_.Name -match '^(todolist-.*\.dump|keys-.*\.zip)$' -and $_.LastWriteTime -lt $cutoff } |
        ForEach-Object {
            Write-Host "==> Pruning $($_.Name)"
            Remove-Item -LiteralPath $_.FullName -Force
        }
}

Write-Host ""
Write-Host "Backup complete: $dumpPath"
