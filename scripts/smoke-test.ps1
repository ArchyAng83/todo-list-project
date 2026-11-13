<#
.SYNOPSIS
Post-deploy smoke test for a running TodoList instance.

.DESCRIPTION
Checks the health probes, the browser hardening headers, bearer login, task
create/read/delete, the board and calendar queries, and JSON export. It uses
only public endpoints, so it works the same against a container on localhost
and against a real deployment.

The script needs an existing account. Create one through /account/register or
use the demo account in a development host.

.PARAMETER BaseUrl
Base URL of the running app. Defaults to http://localhost:8080.

.PARAMETER Email
Account email used for the API login.

.PARAMETER Password
Account password used for the API login.

.PARAMETER SkipDelete
Keep the created task instead of deleting it at the end.

.EXAMPLE
pwsh ./scripts/smoke-test.ps1 `
  -BaseUrl https://todo.example.com `
  -Email smoke@example.com `
  -Password 'change-me'
#>
param(
    [string]$BaseUrl = "http://localhost:8080",
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password,
    [switch]$SkipDelete
)

$ErrorActionPreference = "Stop"

$BaseUrl = $BaseUrl.TrimEnd('/')

function Write-Step {
    param([string]$Name)
    Write-Host "==> $Name"
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

Write-Step "Health probes"
$live = Invoke-WebRequest -Uri "$BaseUrl/health/live" -UseBasicParsing
Assert-True ($live.StatusCode -eq 200) "Liveness returned $($live.StatusCode)."
$ready = Invoke-WebRequest -Uri "$BaseUrl/health/ready" -UseBasicParsing
Assert-True ($ready.StatusCode -eq 200) "Readiness returned $($ready.StatusCode)."

Write-Step "Browser hardening headers"
$landing = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing
Assert-True ($landing.StatusCode -eq 200) "The landing page returned $($landing.StatusCode)."
$nosniff = [string]$landing.Headers["X-Content-Type-Options"]
Assert-True ($nosniff.Contains("nosniff")) "Missing X-Content-Type-Options."
Assert-True (-not [string]::IsNullOrWhiteSpace([string]$landing.Headers["Content-Security-Policy"])) "Missing Content-Security-Policy."

Write-Step "Bearer login"
$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/login" `
    -ContentType "application/json" -Body $loginBody
$token = $login.accessToken
Assert-True (-not [string]::IsNullOrWhiteSpace($token)) "The login response did not contain an access token."
$headers = @{ Authorization = "Bearer $token"; Accept = "application/json" }

Write-Step "Create task"
$today = (Get-Date).ToString("yyyy-MM-dd")
$title = "Smoke test $([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$createBody = @{ title = $title; dueDate = $today; priority = "Medium" } | ConvertTo-Json
$created = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/tasks" `
    -Headers $headers -ContentType "application/json" -Body $createBody
$taskId = $created.id
Assert-True (-not [string]::IsNullOrWhiteSpace($taskId)) "The create response did not contain a task id."

Write-Step "Read task"
$task = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/tasks/$taskId" -Headers $headers
Assert-True ($task.title -eq $title) "The task title did not round-trip."

Write-Step "Board"
$board = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/tasks/board" -Headers $headers
Assert-True ($null -ne $board.columns) "The board response did not contain columns."

Write-Step "Calendar"
$calendar = Invoke-WebRequest -Method Get `
    -Uri "$BaseUrl/api/v1/tasks/calendar?from=$today&to=$today" -Headers $headers -UseBasicParsing
Assert-True ($calendar.StatusCode -eq 200) "Calendar returned $($calendar.StatusCode)."
Assert-True ($calendar.Content.TrimStart().StartsWith("[", [StringComparison]::Ordinal)) `
    "The calendar response was not a JSON list."

Write-Step "JSON export"
$export = Invoke-WebRequest -Method Get -Uri "$BaseUrl/api/v1/data/export" -Headers $headers -UseBasicParsing
Assert-True ($export.StatusCode -eq 200) "Export returned $($export.StatusCode)."
Assert-True ($export.RawContentLength -gt 0) "Export returned an empty body."

if (-not $SkipDelete) {
    Write-Step "Delete task"
    $deleted = Invoke-WebRequest -Method Delete -Uri "$BaseUrl/api/v1/tasks/$taskId" `
        -Headers $headers -UseBasicParsing
    Assert-True ($deleted.StatusCode -eq 204) "Delete returned $($deleted.StatusCode)."
}

Write-Host ""
Write-Host "Smoke test passed for $BaseUrl."
