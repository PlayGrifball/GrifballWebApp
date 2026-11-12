# .NET Tests GitHub Action

This document describes the GitHub Actions workflow for running .NET unit tests in the GrifballWebApp repository.

## Workflow: `.github/workflows/dotnet-tests.yml`

### Purpose
Automatically runs unit tests on every pull request and push to the master branch to prevent regressions and ensure code quality.

### Triggers
- **Pull Requests**: Any PR targeting the `master` branch
- **Direct Pushes**: Any push to the `master` branch

### Environment
- **Runner**: `ubuntu-latest`
- **Timeout**: 30 minutes (prevents hung tests)
- **.NET Version**: 9.0.x (latest patch version)

### Test Framework Stack
- **Test Framework**: NUnit 4.x
- **Mocking**: NSubstitute 5.x (never Moq per project standards)
- **Database Testing**: Testcontainers with SQL Server 2022 or PostgreSQL 18 (a matrix job per database)
- **Code Coverage**: XPlat Code Coverage collection

### Workflow Steps

1. **Checkout**: Downloads repository source code
2. **Setup .NET**: Installs .NET 9.0 SDK
3. **Restore**: Downloads NuGet packages (`dotnet restore`)
4. **Build**: Compiles solution in Release mode
5. **Test**: Runs tests with coverage collection
6. **Upload Artifacts**: Saves test results for debugging
7. **Publish Results**: Displays test results in GitHub UI

### Test Execution
- **Configuration**: Release mode (matches production)
- **Output**: TRX format for GitHub integration
- **Coverage**: XPlat Code Coverage collected
- **Logging**: Normal verbosity for clear output

### Test Projects
A test (each test method, and each case of one) is an **integration test** when it actually integrates with something
real outside the process: a real database server (TestDatabase / Testcontainers: SQL Server, Postgres, or the SQLite
leg's real database files the integration suite creates per test), the hosted app (`WebApplicationFactory`,
`TestServer`), Discord transports, or the network. Every other test is a **unit test** (plain code, test doubles
only), and lives in the test project of the layer whose type it tests: a class with both kinds is split, the unit
tests to the layer's project and the others left in GrifballWebApp.Integration.Test (sharing helpers or an abstract
base fixture from the unit test project).

Since 2026-11-12, an in-memory SQLite database (`Data Source=:memory:`) or a temp file of the test's own (a
`GrifballContext` included) is fine inside a unit test project: it needs no server and no provider matrix, so it
doesn't need GrifballWebApp.Integration.Test either. Only a real database server, the hosted app, Discord transports
or the network push a test into the integration project.

- **`GrifballWebApp.Domain.Test`**: the domain's own rules, on GrifballWebApp.Domain's types alone.
- **`GrifballWebApp.Application.Test`**: the use cases (GrifballWebApp.Application) with test doubles.
- **`GrifballWebApp.Infrastructure.Test`**: GrifballWebApp.Infrastructure's types without a database (parsers, the
  Halo Infinite client's pacing, jobs' wiring, the model as reflection sees it).
- **`GrifballWebApp.Server.Test`**: GrifballWebApp.Server's types (controllers, hubs, telemetry, Discord replies)
  with test doubles.
- **`GrifballWebApp.Seeder.Test`** and **`GrifballWebApp.Convert.Test`**: the types of the two host apps beside the
  server, the seeder (GrifballWebApp.Seeder) and the conversion command (GrifballWebApp.Convert), each in its own project.
- **`GrifballWebApp.Migrations.Postgres.Test`**, **`GrifballWebApp.Migrations.Sqlite.Test`** and
  **`GrifballWebApp.Migrations.SqlServer.Test`**: each migrations project's own types (its design-time context
  factory and services, the SQL generator) and the row-history scaffolding (HistoryMigrationsModelDiffer, the
  design-time migration snapshot) only it exercises. One project per provider, not one for all three: a failing
  migration is that provider's alone, so each targets 100% of lines (the coverage-by-layer comment, below), not the
  usual 70%.
- **`GrifballWebApp.Architecture.Test`**: the architecture's rules, read from the compiled assemblies with Mono.Cecil:
  the layers depend inwards only (Domain <- Application <- Infrastructure <- Server), a type of one of the Domain's
  bounded contexts names another context's only where `ContextCrossings.Allowed` lists it, and an aggregate's
  internals are its own.
- **`GrifballWebApp.Integration.Test`**: the integration tests. It runs once per database, below.

The unit test projects run once, together, as the `test (Unit)` matrix leg. A helper or test double shared by several
projects lives in the lowest test project that uses it; the integration tests reference every unit test project for
them, and a unit test project never references the integration tests.

### Database Testing
The integration tests use Testcontainers to provision an isolated database server, one per matrix job (`GRIF_TEST_DATABASE`):
- **Images**: `mcr.microsoft.com/mssql/server:2022-latest` (`SqlServer`, the default), `postgres:18-alpine` (`Postgres`)
- **Isolation**: Each test gets a unique database
- **Cleanup**: Automatic container disposal after tests

### Environment Variables
- `TESTCONTAINERS_REUSE_ENABLE=false`: Ensures clean test environments
- `GRIF_TEST_DATABASE`: `SqlServer`, `Postgres` or `Sqlite`, from the matrix (`Unit` on the unit leg, which reads none)
- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`: Improves performance
- `DOTNET_NOLOGO=true`: Reduces log noise

### Test Results
- **GitHub UI**: Test results appear in PR checks
- **Artifacts**: Full test outputs uploaded for 90 days
- **Failure Handling**: Workflow fails if any tests fail

### Performance Considerations
- **Parallelization**: Tests can run in parallel where safe
- **Caching**: .NET packages cached between runs
- **Timeout**: 30-minute limit prevents resource waste

## Running Tests Locally

```bash
# Full solution build and test
dotnet restore GrifballWebApp.slnx
dotnet build GrifballWebApp.slnx --configuration Release
# Unit tests (one project per layer and per host app) and the architecture tests: no database, no Docker
for project in GrifballWebApp.Domain.Test GrifballWebApp.Application.Test GrifballWebApp.Infrastructure.Test GrifballWebApp.Server.Test GrifballWebApp.Seeder.Test GrifballWebApp.Convert.Test GrifballWebApp.Migrations.Postgres.Test GrifballWebApp.Migrations.Sqlite.Test GrifballWebApp.Migrations.SqlServer.Test GrifballWebApp.Architecture.Test; do
  dotnet test "$project/$project.csproj" --configuration Release
done
# Integration tests (Testcontainers: Docker; GRIF_TEST_DATABASE picks SqlServer, Postgres or Sqlite)
dotnet test GrifballWebApp.Integration.Test/GrifballWebApp.Integration.Test.csproj --configuration Release

# With coverage
dotnet test GrifballWebApp.Integration.Test/GrifballWebApp.Integration.Test.csproj --collect:"XPlat Code Coverage"
```

## Troubleshooting

### Common Issues
1. **Docker not available**: Ensure Docker daemon is running for Testcontainers
2. **.NET version mismatch**: Workflow uses .NET 9.0, ensure local environment matches
3. **Test timeouts**: Individual tests should complete within reasonable time

### Debugging Failed Tests
1. Check the GitHub Actions logs for detailed output
2. Download test result artifacts for local analysis
3. Run tests locally with same configuration

## Integration with Pull Requests

The workflow automatically:
- ✅ Runs on every PR to `master`
- ✅ Blocks merging if tests fail
- ✅ Shows test results in PR status checks
- ✅ Provides detailed failure information

This ensures no regressions are introduced to the codebase and maintains high code quality standards.
