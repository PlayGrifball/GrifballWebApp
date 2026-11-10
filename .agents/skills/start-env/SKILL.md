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
| **Database** | SQL Server 2022 | `localhost:5434` | This checkout's `docker compose` service `sqlserver` (container `<checkout dir>-sqlserver-1`) |
| **Background Logs** | Text files | `logs/backend.log`<br>`logs/backend-error.log` | Captured when launched with `-Background`; the previous runs' logs are kept as `logs/backend.<time>.log` (newest five) |

> [!NOTE]
> The app runs in **Preview Mode** (`Preview:Enabled=true`), bypassing Discord gateway bot logins, OAuth secrets, and Halo Infinite API keys so you can test features locally without external credentials. Two preview smoke-test users (`admin` and `player`) are automatically initialized on startup.

---

## Quick Commands (Automated Scripts)

Helper PowerShell scripts are located in [`scripts/`](./scripts/).

> [!IMPORTANT]
> **Windows only.** The scripts rely on Docker Desktop for Windows, the compose file's Windows bind mount (`C:\Program Files\Microsoft SQL Server\...`), `taskkill`, `Get-NetTCPConnection` and `Win32_Process`. Run them with Windows PowerShell (`powershell`, as below) or PowerShell 7 on Windows (`pwsh` in place of `powershell`). On Linux or macOS, `start.ps1` stops with an error: follow the [manual workflow](#step-by-step-manual-workflow) instead.

### 1. Start the Environment
```powershell
# Foreground (press Ctrl+C to exit):
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/start.ps1

# In background (returns once /health answers; stops a backend an earlier -Background run started):
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/start.ps1 -Background

# Reset local database (drops & applies all migrations fresh, useful on schema drift; asks first):
powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/start.ps1 -Background -ResetDb
```

Other parameters: `-DockerTimeoutSeconds` (default 180) for a slow Docker Desktop start, `-StartupTimeoutSeconds` (default 300) for the backend's first build, and `-Force` to drop without the prompt.

The script passes the connection string, `Preview:Enabled=true`, `CreateDatabase` and `ApplyMigrations` to the backend as environment variables; it does not touch your user secrets.

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

### Step 2: Set the Preview Mode Settings
For this shell session only (environment variables override appsettings and user secrets):
```powershell
$env:ConnectionStrings__GrifballWebApp = "Server=localhost,5434;Database=GrifballWebApp;Encrypt=False;User Id=sa;Password=Pass@word;"
$env:Preview__Enabled = "true"
$env:CreateDatabase = "true"
$env:ApplyMigrations = "true"
```

### Step 3: Apply Migrations
```powershell
dotnet ef database update --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
```

### Step 4: Run the Application
Start the backend from the repository root, in the same shell:
```powershell
dotnet run --project GrifballWebApp.Server --launch-profile https
```
ASP.NET Core's `SpaProxy` will detect `grifballwebapp.client` and run `npm start` automatically on [https://localhost:4200](https://localhost:4200).

---

## Troubleshooting & Important Notes

> [!IMPORTANT]
> **Docker Desktop Auto-Detection:**
> `start.ps1` checks if the Docker engine daemon is responsive. If not, it automatically locates and launches `Docker Desktop.exe` and waits up to `-DockerTimeoutSeconds` (default 180) for the engine to initialize before bringing up this checkout's `sqlserver` compose service.

> [!NOTE]
> **Preview mode left on by an older `start.ps1`:**
> Earlier versions of the script wrote `Preview:Enabled`, `CreateDatabase`, `ApplyMigrations` and the connection string into the server's user secrets, which keeps preview mode on for every Development run. To undo that:
> ```powershell
> dotnet user-secrets remove "Preview:Enabled" --project GrifballWebApp.Server
> ```
> (and `CreateDatabase`, `ApplyMigrations` or `ConnectionStrings:GrifballWebApp` the same way, if you don't want them).

> [!WARNING]
> **Database Schema Drift / Modified Migrations (`Invalid column name '...'`):**
> When pulling branches or merging feature branches where EF migrations were modified in place (for example, adding columns like `RegionSource` directly into an existing migration file such as `AddHistoricalStats`), `dotnet ef database update` will report that the database is already up to date because the migration entry already exists in `__EFMigrationsHistory`. At startup, queries referencing the new columns will fail with `SqlException: Invalid column name '...'`.
> 
> **Solution:** Recreate the local test database cleanly by running:
> ```powershell
> powershell -ExecutionPolicy Bypass -File .agents/skills/start-env/scripts/start.ps1 -Background -ResetDb
> ```
> `-ResetDb` deletes all local data. It only drops the database when port 5434 is served by this checkout's own `sqlserver` container, stops a backend an earlier `-Background` run started first, and asks for confirmation. **Agents: ask the user before resetting**, then pass `-Force` (a non-interactive session cannot answer the prompt).
>
> Or manually (with the Step 2 environment variables set):
> ```powershell
> dotnet ef database drop --force --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
> dotnet ef database update --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
> ```

> [!IMPORTANT]
> **Windows Named Pipe / Sandbox Isolation:**
> On Windows, commands running in a sandboxed agent session cannot be accessed by external browsers or subsequent sessions unless executed with `BypassSandbox: true` or run directly in the host terminal.

> [!WARNING]
> **EF Core Check Constraints and Line Endings:**
> In Entity Framework Core 10, multi-line string check constraints formatted with `.Replace(Environment.NewLine, " ")` will trigger `PendingModelChangesWarning` if git checks out files with LF (`\n`) line endings instead of CRLF. Always maintain SQL check constraints as single-line strings in entity configurations ([SeasonMatchConfiguration.cs](../../../GrifballWebApp.Domain/League/SeasonMatchConfiguration.cs) and [MatchBracketInfoConfiguration.cs](../../../GrifballWebApp.Domain/League/MatchBracketInfoConfiguration.cs)).

> [!TIP]
> **ASP.NET HTTPS Dev Certificates:**
> If the browser warns about HTTPS certificates, trust the ASP.NET Core developer certificate:
> ```powershell
> dotnet dev-certs https --trust
> ```
