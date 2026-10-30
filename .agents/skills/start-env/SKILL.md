---
name: start-env
description: >-
  Manage the local Grifball development and test environment. Use this skill when
  the user wants to start, stop, restart, or check the status of the local test
  environment (SQL Server container, .NET Kestrel backend in preview mode, and Angular client).
---

# Local Test Environment Skill (`start-env`)

This skill defines the procedures and automated scripts for starting, inspecting, and stopping the local Grifball development and test environment.

---

## Architecture & Endpoints

| Component | Technology | Local Port / URL | Notes |
| :--- | :--- | :--- | :--- |
| **Frontend** | Angular 22 | [https://localhost:4200](https://localhost:4200) | Auto-spawned via `SpaProxy` or `npm start` |
| **Backend API** | ASP.NET Core (.NET 10) | [https://localhost:7210](https://localhost:7210) | Launch profile `https` (HTTP at port 5295) |
| **Swagger UI** | Swashbuckle / OpenAPI | [https://localhost:7210/swagger](https://localhost:7210/swagger) | Interactive API documentation |
| **Health Check** | ASP.NET HealthChecks | [https://localhost:7210/health](https://localhost:7210/health) | Returns `Healthy` (HTTP 200) |
| **Database** | SQL Server 2022 | `localhost:5434` | Running in Docker container `grifball-sqlserver` |

> [!NOTE]
> The app runs in **Preview Mode** (`Preview:Enabled=true`), bypassing Discord gateway bot logins, OAuth secrets, and Halo Infinite API keys so you can test features locally without external credentials.

---

## Quick Commands (Automated Scripts)

Helper PowerShell scripts are located in [`scripts/`](./scripts/):

### 1. Start the Environment
```powershell
# Foreground (press Ctrl+C to exit):
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/start.ps1

# Or in background:
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/start.ps1 -Background
```

### 2. Check Status
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/status.ps1
```

### 3. Stop the Environment
```powershell
# Stop backend & frontend (leaves SQL Server container running for fast restarts):
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/stop.ps1

# Stop backend, frontend, AND SQL Server container:
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/stop.ps1 -StopDocker
```

---

## Step-by-Step Manual Workflow

If you prefer to start or stop services manually:

### Step 1: Start SQL Server
Ensure Docker Desktop is open, then start the container:
```powershell
docker compose up sqlserver -d
```
Verify port 5434 is listening:
```powershell
Test-NetConnection -ComputerName localhost -Port 5434
```

### Step 2: Configure Preview Mode User Secrets
Run once per machine (already configured in user profile):
```powershell
dotnet user-secrets set "Preview:Enabled" "true" --project GrifballWebApp.Server
dotnet user-secrets set "CreateDatabase" "true" --project GrifballWebApp.Server
dotnet user-secrets set "ApplyMigrations" "true" --project GrifballWebApp.Server
dotnet user-secrets set "ConnectionStrings:GrifballWebApp" "Server=localhost,5434;Database=GrifballWebApp;Encrypt=False;User Id=sa;Password=Pass@word;" --project GrifballWebApp.Server
```

### Step 3: Apply Migrations
```powershell
$env:ConnectionStrings__GrifballWebApp = "Server=localhost,5434;Database=GrifballWebApp;Encrypt=False;User Id=sa;Password=Pass@word;"
dotnet ef database update --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
```

### Step 4: Run the Application
Start the backend from the repository root:
```powershell
dotnet run --project GrifballWebApp.Server --launch-profile https
```
ASP.NET Core's `SpaProxy` will detect `grifballwebapp.client` and run `npm start` automatically on [https://localhost:4200](https://localhost:4200).

---

## Troubleshooting & Important Notes

> [!IMPORTANT]
> **Windows Named Pipe / Sandbox Isolation:**
> On Windows, commands running in a sandboxed agent session cannot be accessed by external browsers or subsequent sessions unless executed with `BypassSandbox: true` or run directly in the host terminal.

> [!WARNING]
> **EF Core Check Constraints and Line Endings:**
> In Entity Framework Core 10, multi-line string check constraints formatted with `.Replace(Environment.NewLine, " ")` will trigger `PendingModelChangesWarning` if git checks out files with LF (`\n`) line endings instead of CRLF. Always maintain SQL check constraints as single-line strings in entity configurations ([SeasonMatchConfiguration.cs](file:///C:/Users/jeffr/Documents/GrifballWebApp-master/GrifballWebApp.Database/Configuration/SeasonMatchConfiguration.cs) and [MatchBracketInfoConfiguration.cs](file:///C:/Users/jeffr/Documents/GrifballWebApp-master/GrifballWebApp.Database/Configuration/MatchBracketInfoConfiguration.cs)).

> [!TIP]
> **ASP.NET HTTPS Dev Certificates:**
> If the browser warns about HTTPS certificates, trust the ASP.NET Core developer certificate:
> ```powershell
> dotnet dev-certs https --trust
> ```
