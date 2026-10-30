<#
.SYNOPSIS
    Starts the local Grifball development and test environment.
.PARAMETER Background
    If specified, runs the backend and frontend in the background instead of foreground.
#>
[CmdletBinding()]
param(
    [switch]$Background
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")

Write-Host "=== Starting Grifball Test Environment ===" -ForegroundColor Cyan
Write-Host "Repository root: $repoRoot" -ForegroundColor Gray

# 1. Start SQL Server container
Write-Host "`n[1/4] Ensuring SQL Server is running..." -ForegroundColor Yellow
$containerStatus = docker ps --filter "name=grifball-sqlserver" --format "{{.Status}}" 2>$null
if (-not $containerStatus -or -not ($containerStatus -like "Up*")) {
    Write-Host "Starting SQL Server container via docker compose..." -ForegroundColor Gray
    Push-Location $repoRoot
    try {
        docker compose up sqlserver -d
    } finally {
        Pop-Location
    }
}

# Wait for port 5434
Write-Host "Waiting for SQL Server to listen on port 5434..." -ForegroundColor Gray
$maxRetries = 30
$retries = 0
$sqlReady = $false
while (-not $sqlReady -and $retries -lt $maxRetries) {
    $test = Test-NetConnection -ComputerName "localhost" -Port 5434 -WarningAction SilentlyContinue
    if ($test.TcpTestSucceeded) {
        $sqlReady = $true
        Write-Host "SQL Server is listening on port 5434." -ForegroundColor Green
        break
    }
    Start-Sleep -Seconds 1
    $retries++
}
if (-not $sqlReady) {
    Write-Error "Timed out waiting for SQL Server on port 5434. Make sure Docker Desktop is running."
    return
}

# 2. Configure user secrets for Preview Mode
Write-Host "`n[2/4] Verifying User Secrets for Preview Mode..." -ForegroundColor Yellow
Push-Location $repoRoot
try {
    dotnet user-secrets set "Preview:Enabled" "true" --project GrifballWebApp.Server | Out-Null
    dotnet user-secrets set "CreateDatabase" "true" --project GrifballWebApp.Server | Out-Null
    dotnet user-secrets set "ApplyMigrations" "true" --project GrifballWebApp.Server | Out-Null
    dotnet user-secrets set "ConnectionStrings:GrifballWebApp" "Server=localhost,5434;Database=GrifballWebApp;Encrypt=False;User Id=sa;Password=Pass@word;" --project GrifballWebApp.Server | Out-Null
    Write-Host "User secrets configured (Preview Mode active, Discord requirements bypassed)." -ForegroundColor Green
} finally {
    Pop-Location
}

# 3. Apply database migrations
Write-Host "`n[3/4] Applying database migrations..." -ForegroundColor Yellow
Push-Location $repoRoot
try {
    $env:ConnectionStrings__GrifballWebApp = "Server=localhost,5434;Database=GrifballWebApp;Encrypt=False;User Id=sa;Password=Pass@word;"
    dotnet ef database update --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
    Write-Host "Database is up to date." -ForegroundColor Green
} finally {
    Pop-Location
}

# 4. Launch Application
Write-Host "`n[4/4] Starting backend and frontend..." -ForegroundColor Yellow
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " App URLs:" -ForegroundColor Cyan
Write-Host "   Frontend:  https://localhost:4200" -ForegroundColor Green
Write-Host "   Backend:   https://localhost:7210" -ForegroundColor Green
Write-Host "   Swagger:   https://localhost:7210/swagger" -ForegroundColor Green
Write-Host "   Health:    https://localhost:7210/health" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan

if ($Background) {
    Write-Host "Starting backend in background (SpaProxy will launch the frontend)..." -ForegroundColor Yellow
    Start-Process -FilePath "dotnet" -ArgumentList "run --project GrifballWebApp.Server --launch-profile https" -WorkingDirectory $repoRoot
    Write-Host "Backend launched. Run 'status.ps1' to verify when ready." -ForegroundColor Green
} else {
    Write-Host "Starting backend in foreground (Press Ctrl+C to stop)..." -ForegroundColor Yellow
    Push-Location $repoRoot
    try {
        dotnet run --project GrifballWebApp.Server --launch-profile https
    } finally {
        Pop-Location
    }
}
