<#
.SYNOPSIS
    Starts the local Grifball development and test environment.
.PARAMETER Background
    If specified, runs the backend and frontend in the background instead of foreground.
.PARAMETER ResetDb
    If specified, drops and recreates the local database before applying migrations.
    Useful when branch updates or merges modify existing EF migrations (schema drift).
#>
[CmdletBinding()]
param(
    [switch]$Background,
    [switch]$ResetDb
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")

Write-Host "=== Starting Grifball Test Environment ===" -ForegroundColor Cyan
Write-Host "Repository root: $repoRoot" -ForegroundColor Gray

# 1. Start Docker Desktop and SQL Server container
Write-Host "`n[1/4] Ensuring SQL Server is running..." -ForegroundColor Yellow

$dockerReady = $false
$prevEAP = $ErrorActionPreference
try {
    $ErrorActionPreference = 'SilentlyContinue'
    $null = docker info 2>$null
    if ($LASTEXITCODE -eq 0) {
        $dockerReady = $true
    }
} finally {
    $ErrorActionPreference = $prevEAP
}

if (-not $dockerReady) {
    Write-Host "Docker daemon is not responding. Checking for Docker Desktop..." -ForegroundColor Yellow
    $dockerExeCandidates = @(
        "C:\Program Files\Docker\Docker\Docker Desktop.exe",
        "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe",
        "$env:LOCALAPPDATA\Programs\Docker Desktop\Docker Desktop.exe"
    )
    $dockerDesktopPath = $dockerExeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($dockerDesktopPath) {
        Write-Host "Launching Docker Desktop from: $dockerDesktopPath" -ForegroundColor Gray
        Start-Process -FilePath $dockerDesktopPath -ErrorAction SilentlyContinue
        Write-Host "Waiting up to 60 seconds for Docker daemon to become ready..." -ForegroundColor Gray
        $dockerRetries = 0
        $dockerMaxRetries = 60
        while (-not $dockerReady -and $dockerRetries -lt $dockerMaxRetries) {
            Start-Sleep -Seconds 2
            $dockerRetries += 2
            $prevEAP = $ErrorActionPreference
            try {
                $ErrorActionPreference = 'SilentlyContinue'
                $null = docker info 2>$null
                if ($LASTEXITCODE -eq 0) {
                    $dockerReady = $true
                    Write-Host "Docker daemon is now ready." -ForegroundColor Green
                    break
                }
            } finally {
                $ErrorActionPreference = $prevEAP
            }
        }
    }

    if (-not $dockerReady) {
        Write-Error "Unable to communicate with Docker daemon. Please ensure Docker Desktop is running."
        return
    }
}

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
    if ($ResetDb) {
        Write-Host "ResetDb specified: Dropping existing local database to ensure clean schema sync..." -ForegroundColor Yellow
        dotnet ef database drop --force --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
        Write-Host "Database dropped successfully." -ForegroundColor Gray
    }
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
    $logsDir = Join-Path $repoRoot "logs"
    if (-not (Test-Path $logsDir)) {
        New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
    }
    $backendLog = Join-Path $logsDir "backend.log"
    $backendErrLog = Join-Path $logsDir "backend-error.log"

    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList "run --project GrifballWebApp.Server --launch-profile https" `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $backendLog `
        -RedirectStandardError $backendErrLog `
        -PassThru

    Start-Sleep -Seconds 3
    if ($process.HasExited) {
        Write-Error "Backend process exited prematurely (ExitCode: $($process.ExitCode)). Check logs at: $backendErrLog or $backendLog"
        return
    }

    Write-Host "Backend launched (PID: $($process.Id)). Logs: $backendLog" -ForegroundColor Green
    Write-Host "Run 'status.ps1' to verify when ready." -ForegroundColor Green
} else {
    Write-Host "Starting backend in foreground (Press Ctrl+C to stop)..." -ForegroundColor Yellow
    Push-Location $repoRoot
    try {
        dotnet run --project GrifballWebApp.Server --launch-profile https
    } finally {
        Pop-Location
    }
}
