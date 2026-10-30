<#
.SYNOPSIS
    Stops the local Grifball development and test environment.
.PARAMETER StopDocker
    If specified, also stops the SQL Server Docker container. Default is to leave the container running for faster restarts.
#>
[CmdletBinding()]
param(
    [switch]$StopDocker
)

$ErrorActionPreference = 'SilentlyContinue'

Write-Host "=== Stopping Grifball Test Environment ===" -ForegroundColor Cyan

# 1. Stop backend processes
$dotnetProcesses = Get-CimInstance Win32_Process | Where-Object {
    $_.CommandLine -like "*GrifballWebApp.Server*"
}
if ($dotnetProcesses) {
    Write-Host "Stopping backend (.NET) processes..." -ForegroundColor Yellow
    foreach ($proc in $dotnetProcesses) {
        Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "No backend (.NET) process found." -ForegroundColor Gray
}

# 2. Stop frontend (processes listening on port 4200)
$frontendConnections = Get-NetTCPConnection -LocalPort 4200 -ErrorAction SilentlyContinue
if ($frontendConnections) {
    Write-Host "Stopping frontend (Node.js) on port 4200..." -ForegroundColor Yellow
    foreach ($conn in $frontendConnections) {
        Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "No frontend process listening on port 4200." -ForegroundColor Gray
}

# Also check port 7210 / 5295 just in case
$backendConnections = Get-NetTCPConnection -LocalPort 7210,5295 -ErrorAction SilentlyContinue
if ($backendConnections) {
    foreach ($conn in $backendConnections) {
        Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
    }
}

# 3. Stop Docker container if requested
if ($StopDocker) {
    Write-Host "Stopping SQL Server Docker container..." -ForegroundColor Yellow
    docker stop grifball-sqlserver 2>$null
    Write-Host "SQL Server container stopped." -ForegroundColor Gray
} else {
    Write-Host "Leaving SQL Server container running for fast restarts (pass -StopDocker to stop it)." -ForegroundColor Gray
}

Start-Sleep -Seconds 2
Write-Host "=== Environment Stopped ===" -ForegroundColor Green
