<#
.SYNOPSIS
    Starts the local Grifball development and test environment (Windows only).
.DESCRIPTION
    Starts Docker Desktop if needed, brings up this checkout's SQL Server container (docker compose, port 5434),
    applies the EF migrations and launches the backend (SpaProxy starts the Angular client).
    The app settings it needs (connection string, preview mode, CreateDatabase, ApplyMigrations) are passed to the
    launched processes as environment variables; the developer's user secrets are left alone.
.PARAMETER Background
    Runs the backend in the background, with its output in logs/backend.log and logs/backend-error.log
    (earlier logs are kept as backend.<time>.log). Waits until /health answers before returning.
    A backend this script started in the background earlier is stopped first.
.PARAMETER ResetDb
    Drops and recreates the local database before applying migrations.
    Useful when branch updates or merges modify existing EF migrations (schema drift).
    Only runs against this checkout's own SQL Server container, and asks first unless -Force is given.
.PARAMETER Force
    With -ResetDb: drops the database without asking.
.PARAMETER DockerTimeoutSeconds
    How long to wait for the Docker daemon after launching Docker Desktop (default 180).
.PARAMETER StartupTimeoutSeconds
    With -Background: how long to wait for the backend's /health to answer (default 300; the first run builds).
#>
[CmdletBinding()]
param(
    [switch]$Background,
    [switch]$ResetDb,
    [switch]$Force,
    [int]$DockerTimeoutSeconds = 180,
    [int]$StartupTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
    throw "start.ps1 is Windows-only (Docker Desktop paths, the compose file's Windows bind mount, taskkill). On Linux or macOS follow the manual steps in SKILL.md."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
$sqlPort = 5434
$backendPorts = @(7210, 5295)
$healthUrl = "https://localhost:7210/health"
$databaseName = "GrifballWebApp"
$connectionString = "Server=localhost,$sqlPort;Database=$databaseName;Encrypt=False;User Id=sa;Password=Pass@word;"
$logsDir = Join-Path $repoRoot "logs"
$pidFile = Join-Path $logsDir "backend.pid"

# What the backend and dotnet ef need, passed as environment variables (they override appsettings and user secrets).
$appEnvironment = [ordered]@{
    'ConnectionStrings__GrifballWebApp' = $connectionString
    'Preview__Enabled'                  = 'true'
    'CreateDatabase'                    = 'true'
    'ApplyMigrations'                   = 'true'
}

# Windows PowerShell 5.1 does not stop on a native command's failure, so every call that matters goes through this.
function Invoke-Native {
    param([string]$What, [scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit code $LASTEXITCODE)."
    }
}

function Test-DockerReady {
    $prevEAP = $ErrorActionPreference
    try {
        # A native command's stderr becomes an error record under 'Stop' in 5.1.
        $ErrorActionPreference = 'SilentlyContinue'
        $null = docker info 2>$null
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    } finally {
        $ErrorActionPreference = $prevEAP
    }
}

function Test-Port {
    param([int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.ConnectAsync("127.0.0.1", $Port)
        return ($connect.Wait(1000) -and $client.Connected)
    } catch {
        return $false
    } finally {
        $client.Dispose()
    }
}

function Get-HealthStatus {
    $prevEAP = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'SilentlyContinue'
        $code = curl.exe -k -s -o NUL --max-time 5 -w "%{http_code}" $healthUrl 2>$null
        return "$code"
    } catch {
        return ""
    } finally {
        $ErrorActionPreference = $prevEAP
    }
}

function Get-SqlContainerId {
    $id = Invoke-Native "docker compose ps" { docker compose ps -q sqlserver }
    return (@($id) | Where-Object { $_ } | Select-Object -First 1)
}

# Stops the backend an earlier -Background run of this checkout started (its PID is in logs/backend.pid).
function Stop-PreviousBackend {
    if (-not (Test-Path $pidFile)) { return }
    $saved = (Get-Content $pidFile -Raw).Trim() -split '\s+'
    $oldProcess = Get-Process -Id ([int]$saved[0]) -ErrorAction SilentlyContinue
    # The PID may have been reused since: only stop the process that started at the saved time.
    if ($oldProcess -and $saved.Count -gt 1 -and $oldProcess.StartTime.ToString('o') -eq $saved[1]) {
        Write-Host "Stopping the backend started by an earlier -Background run (PID $($oldProcess.Id))..." -ForegroundColor Yellow
        $prevEAP = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'SilentlyContinue'
            # /T: dotnet run's children too (the server and the Angular dev server SpaProxy started).
            $null = taskkill.exe /PID $oldProcess.Id /T /F 2>$null
        } finally {
            $ErrorActionPreference = $prevEAP
        }
        if (-not $oldProcess.WaitForExit(15000)) {
            throw "Could not stop the earlier backend (PID $($oldProcess.Id)). Run stop.ps1, then try again."
        }
        $deadline = (Get-Date).AddSeconds(30)
        while (($backendPorts | Where-Object { Test-Port $_ }) -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
        }
    }
    Remove-Item $pidFile -Force
}

# Keeps the previous run's log as <name>.<time>.log (the newest five of each), instead of overwriting it.
function Move-OldLog {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return }
    $stamp = (Get-Item $Path).LastWriteTime.ToString('yyyyMMdd-HHmmss')
    Move-Item -Path $Path -Destination ([IO.Path]::ChangeExtension($Path, "$stamp.log")) -Force
    $baseName = [IO.Path]::GetFileNameWithoutExtension($Path)
    Get-ChildItem -Path (Split-Path $Path) -Filter "$baseName.*.log" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip 5 |
        Remove-Item -Force
}

function Show-LogTail {
    param([string[]]$Paths)
    foreach ($path in $Paths) {
        if ((Test-Path $path) -and (Get-Item $path).Length -gt 0) {
            Write-Host "--- last lines of $path ---" -ForegroundColor Gray
            Get-Content $path -Tail 20 | ForEach-Object { Write-Host $_ }
        }
    }
}

$savedEnvironment = @{}
foreach ($name in $appEnvironment.Keys) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Write-Host "=== Starting Grifball Test Environment ===" -ForegroundColor Cyan
Write-Host "Repository root: $repoRoot" -ForegroundColor Gray

Push-Location $repoRoot
try {
    # 1. Docker and this checkout's SQL Server container
    Write-Host "`n[1/4] Ensuring SQL Server is running..." -ForegroundColor Yellow

    if (-not (Test-DockerReady)) {
        Write-Host "Docker daemon is not responding. Checking for Docker Desktop..." -ForegroundColor Yellow
        $dockerDesktopPath = @(
            "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe",
            "$env:LOCALAPPDATA\Programs\Docker Desktop\Docker Desktop.exe"
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $dockerDesktopPath) {
            throw "Docker daemon is not responding and Docker Desktop was not found. Start Docker, then try again."
        }

        Write-Host "Launching Docker Desktop from: $dockerDesktopPath" -ForegroundColor Gray
        Start-Process -FilePath $dockerDesktopPath -ErrorAction SilentlyContinue
        Write-Host "Waiting up to $DockerTimeoutSeconds seconds for the Docker daemon..." -ForegroundColor Gray
        $deadline = (Get-Date).AddSeconds($DockerTimeoutSeconds)
        $dockerReady = $false
        while (-not $dockerReady -and (Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
            $dockerReady = Test-DockerReady
        }
        if (-not $dockerReady) {
            throw "Docker daemon did not become ready within $DockerTimeoutSeconds seconds. Re-run with a larger -DockerTimeoutSeconds once Docker Desktop is up."
        }
        Write-Host "Docker daemon is ready." -ForegroundColor Green
    }

    $containerId = Get-SqlContainerId
    $running = $false
    if ($containerId) {
        $running = (Invoke-Native "docker inspect" { docker inspect -f "{{.State.Running}}" $containerId }) -eq 'true'
    }
    if (-not $running) {
        Write-Host "Starting this checkout's SQL Server container via docker compose..." -ForegroundColor Gray
        Invoke-Native "docker compose up sqlserver (is port $sqlPort taken by another checkout's container?)" { docker compose up -d sqlserver }
        $containerId = Get-SqlContainerId
        if (-not $containerId) {
            throw "docker compose did not create the sqlserver container."
        }
    }
    $containerName = (Invoke-Native "docker inspect" { docker inspect -f "{{.Name}}" $containerId }).TrimStart('/')

    Write-Host "Waiting for SQL Server to listen on port $sqlPort..." -ForegroundColor Gray
    $deadline = (Get-Date).AddSeconds(60)
    while (-not (Test-Port $sqlPort) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
    }
    if (-not (Test-Port $sqlPort)) {
        throw "Timed out waiting for SQL Server ($containerName) on port $sqlPort."
    }
    Write-Host "SQL Server ($containerName) is listening on port $sqlPort." -ForegroundColor Green

    # 2. Checks before anything changes
    Write-Host "`n[2/4] Checking the database and backend ports..." -ForegroundColor Yellow
    if ($ResetDb) {
        # The drop goes to whatever answers on localhost:5434: make sure that is this checkout's container.
        $published = @(Invoke-Native "docker port" { docker port $containerId 1433/tcp })
        $publishers = @(Invoke-Native "docker ps" { docker ps -q --filter "publish=$sqlPort" })
        $ours = ($published | Where-Object { $_ -match ":$sqlPort$" }) -and
            $publishers.Count -eq 1 -and $containerId.StartsWith($publishers[0])
        if (-not $ours) {
            throw "Port $sqlPort is not served by this checkout's SQL Server container ($containerName); refusing to drop the database."
        }
        if (-not $Force) {
            try {
                $answer = Read-Host "Drop database '$databaseName' in container $containerName (port $sqlPort)? Type 'yes' to continue"
            } catch {
                throw "Cannot ask before dropping the database (non-interactive session). Pass -Force to drop it without asking."
            }
            if ($answer -ne 'yes') {
                throw "Database reset cancelled. Nothing was changed."
            }
        }
    }

    Stop-PreviousBackend
    $busy = @($backendPorts | Where-Object { Test-Port $_ })
    if ($busy.Count -gt 0) {
        throw "Port(s) $($busy -join ', ') already in use (a backend started in the foreground or from another checkout?). Stop it (stop.ps1), then try again."
    }

    foreach ($name in $appEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $appEnvironment[$name], 'Process')
    }

    # 3. Database migrations
    Write-Host "`n[3/4] Applying database migrations..." -ForegroundColor Yellow
    if ($ResetDb) {
        Write-Host "ResetDb specified: dropping database '$databaseName' in $containerName..." -ForegroundColor Yellow
        Invoke-Native "dotnet ef database drop" {
            dotnet ef database drop --force --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
        }
        Write-Host "Database dropped." -ForegroundColor Gray
    }
    Invoke-Native "dotnet ef database update" {
        dotnet ef database update --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
    }
    Write-Host "Database is up to date." -ForegroundColor Green

    # 4. Backend (SpaProxy starts the frontend)
    Write-Host "`n[4/4] Starting backend and frontend (preview mode)..." -ForegroundColor Yellow
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host " App URLs:" -ForegroundColor Cyan
    Write-Host "   Frontend:  https://localhost:4200" -ForegroundColor Green
    Write-Host "   Backend:   https://localhost:7210" -ForegroundColor Green
    Write-Host "   Swagger:   https://localhost:7210/swagger" -ForegroundColor Green
    Write-Host "   Health:    $healthUrl" -ForegroundColor Green
    Write-Host "=============================================" -ForegroundColor Cyan

    if ($Background) {
        if (-not (Test-Path $logsDir)) {
            New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
        }
        $backendLog = Join-Path $logsDir "backend.log"
        $backendErrLog = Join-Path $logsDir "backend-error.log"
        Move-OldLog $backendLog
        Move-OldLog $backendErrLog

        Write-Host "Starting backend in background..." -ForegroundColor Yellow
        # It inherits this process's environment, the settings above included.
        $process = Start-Process -FilePath "dotnet" `
            -ArgumentList "run --project GrifballWebApp.Server --launch-profile https" `
            -WorkingDirectory $repoRoot `
            -RedirectStandardOutput $backendLog `
            -RedirectStandardError $backendErrLog `
            -PassThru
        # Windows PowerShell only fills in ExitCode once the handle has been read.
        $null = $process.Handle
        Set-Content -Path $pidFile -Value "$($process.Id) $($process.StartTime.ToString('o'))"

        Write-Host "Waiting up to $StartupTimeoutSeconds seconds for $healthUrl (PID $($process.Id))..." -ForegroundColor Gray
        $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
        $healthy = $false
        while (-not $healthy -and (Get-Date) -lt $deadline) {
            if ($process.HasExited) {
                Show-LogTail @($backendErrLog, $backendLog)
                Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
                throw "Backend exited before it was healthy (exit code $($process.ExitCode)). Logs: $backendErrLog, $backendLog"
            }
            $healthy = (Get-HealthStatus) -eq '200'
            if (-not $healthy) { Start-Sleep -Seconds 2 }
        }
        if (-not $healthy) {
            Show-LogTail @($backendErrLog, $backendLog)
            throw "Backend (PID $($process.Id)) did not report healthy within $StartupTimeoutSeconds seconds; it is still running. Logs: $backendLog"
        }

        Write-Host "Backend is healthy (PID: $($process.Id)). Logs: $backendLog" -ForegroundColor Green
        Write-Host "The frontend may take a little longer; run status.ps1 to check it." -ForegroundColor Green
    } else {
        Write-Host "Starting backend in foreground (Press Ctrl+C to stop)..." -ForegroundColor Yellow
        dotnet run --project GrifballWebApp.Server --launch-profile https
    }
} finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    Pop-Location
}
