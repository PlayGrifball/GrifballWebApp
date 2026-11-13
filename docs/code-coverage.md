# Code Coverage

This document describes how to run tests with code coverage in the GrifballWebApp project.

## Running Tests with Coverage

The project is configured to use Coverlet for code coverage collection. To run tests with coverage:

```bash
# Run all tests with code coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Run specific test project with coverage
dotnet test GrifballWebApp.Domain.Test/GrifballWebApp.Domain.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Application.Test/GrifballWebApp.Application.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Infrastructure.Test/GrifballWebApp.Infrastructure.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Server.Test/GrifballWebApp.Server.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Seeder.Test/GrifballWebApp.Seeder.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Convert.Test/GrifballWebApp.Convert.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Migrations.Postgres.Test/GrifballWebApp.Migrations.Postgres.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Migrations.Sqlite.Test/GrifballWebApp.Migrations.Sqlite.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Migrations.SqlServer.Test/GrifballWebApp.Migrations.SqlServer.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Architecture.Test/GrifballWebApp.Architecture.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet test GrifballWebApp.Integration.Test/GrifballWebApp.Integration.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

## Coverage Configuration

The code coverage is configured in `coverlet.runsettings` with the following exclusions:
- Database migrations, generated, for both databases (file pattern `**/GrifballWebApp.Migrations.*/Migrations/*.cs`)
- The generated NetCord wrappers and their generator (`**/DiscordInterfaces/*.cs`, `**/DiscordInterfaceSourceGen/*.cs`)
- What .NET's regex and logging source generators write at build time (`**/System.Text.RegularExpressions.Generator/**/*.g.cs`, `**/Microsoft.Extensions.Logging.Generators/**/*.g.cs`, and `**/Microsoft.Gen.Logging/**/*.g.cs`, which writes them instead where Microsoft.Extensions.Telemetry is referenced): the matchers behind `[GeneratedRegex]` and the methods behind `[LoggerMessage]`. The pattern or log message is ours and is tested through the code that calls it; the generated matcher's backtracking branches are .NET's. Other generators' output is still counted. These files are never in the repo, so Codecov and SonarCloud didn't count them anyway; this changes the per-layer comment only
- The test projects' assemblies (`[GrifballWebApp.*.Test]*`), which load each other for their shared helpers (the integration tests every unit test project, the unit tests the domain tests)

The thin NetCord modules (`DiscordCommands`, the `*Interactions` classes) are not excluded: `DiscordTransportTests` runs every one of their entry points through NetCord with only the HTTP transport faked.

## Coverage Output

Coverage results are generated in the `TestResults` folder in Cobertura XML format, which can be used by various coverage reporting tools.

## New Test Coverage

The following new test files have been added to cover the custom seeding functionality:

### BracketServiceTests.cs
- Tests for `SetCustomSeeds` method covering:
  - Valid custom seed ordering
  - Error handling for already seeded teams
  - Error handling for missing seed numbers
  - Error handling for null seed numbers
  - Multiple round bracket handling
  - Mixed match types (with and without seed numbers)

### BracketsControllerTests.cs (GrifballWebApp.Server.Test)  
- Tests for `SetCustomSeeds` endpoint covering:
  - Parameter passing to service layer
  - Exception propagation
  - Empty array handling

### CustomSeedDtoTests.cs (GrifballWebApp.Application.Test)
- Tests for `CustomSeedDto` record covering:
  - Property assignment
  - Record equality semantics
  - Hash code consistency
  - Value comparison scenarios

## Running Individual Test Files

To run specific test files:

```bash
# Run BracketService tests only
dotnet test --filter "FullyQualifiedName~BracketServiceTests"

# Run BracketsController tests only  
dotnet test --filter "FullyQualifiedName~BracketsControllerTests"

# Run CustomSeedDto tests only
dotnet test --filter "FullyQualifiedName~CustomSeedDtoTests"
```

## Test Categories

All new tests follow the project's testing patterns:
- Use `[TestFixture]` with `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]`
- A test is an **integration test** (`GrifballWebApp.Integration.Test`) only when it integrates with something real
  outside the process: a real database server (Testcontainers), the hosted app (`WebApplicationFactory`/`TestServer`),
  Discord transports, or the network. Since 2026-11-12, an in-memory SQLite database (`Data Source=:memory:`) or a
  temp file of the test's own (a `GrifballContext` included) is fine inside a unit test project: it needs no server
  and no provider matrix, so it doesn't push the test into `GrifballWebApp.Integration.Test` (see
  `docs/ci-cd/dotnet-tests.md`). A class with both kinds of test is split: the unit tests to the layer's own project,
  the others left in `GrifballWebApp.Integration.Test`.
- Use NSubstitute for mocking dependencies
- Use descriptive test method names following `MethodName_ExpectedBehavior_WhenCondition` pattern
- Use `Assert.Multiple()` (or `Assert.EnterMultipleScope()`) for grouped assertions