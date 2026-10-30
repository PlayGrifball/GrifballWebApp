<#
.SYNOPSIS
    Checks the status of the local Grifball development/test environment.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'SilentlyContinue'

Write-Host "=== Grifball Local Test Environment Status ===" -ForegroundColor Cyan

# 1. SQL Server
$sqlConnection = Test-NetConnection -ComputerName "localhost" -Port 5434 -WarningAction SilentlyContinue
if ($sqlConnection.TcpTestSucceeded) {
    Write-Host "[OK] SQL Server container is listening on port 5434" -ForegroundColor Green
} else {
    Write-Host "[DOWN] SQL Server is NOT listening on port 5434" -ForegroundColor Red
}

# 2. Backend API
try {
    $backendRes = curl.exe -k -s -o /dev/null -w "%{http_code}" "https://localhost:7210/health" 2>$null
    if ($backendRes -eq "200") {
        Write-Host "[OK] Backend API is Healthy (https://localhost:7210/health)" -ForegroundColor Green
        Write-Host "     Swagger UI available at https://localhost:7210/swagger" -ForegroundColor Gray
    } else {
        Write-Host "[DOWN] Backend API is NOT responding on https://localhost:7210 (HTTP $backendRes)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "[DOWN] Backend API is not responding on https://localhost:7210" -ForegroundColor Red
}

# 3. Frontend Client
try {
    $frontendRes = curl.exe -k -s -o /dev/null -w "%{http_code}" "https://localhost:4200" 2>$null
    if ($frontendRes -eq "200") {
        Write-Host "[OK] Frontend Client is running (https://localhost:4200)" -ForegroundColor Green
    } else {
        Write-Host "[DOWN] Frontend is NOT responding on https://localhost:4200 (HTTP $frontendRes)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "[DOWN] Frontend is not responding on https://localhost:4200" -ForegroundColor Red
}

Write-Host "=============================================" -ForegroundColor Cyan
