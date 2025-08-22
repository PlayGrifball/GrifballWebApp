# Code Coverage

This document describes how to run tests with code coverage in the GrifballWebApp project.

## Running Tests with Coverage

The project is configured to use Coverlet for code coverage collection. To run tests with coverage:

```bash
# Run all tests with code coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Run specific test project with coverage
dotnet test GrifballWebApp.Test/GrifballWebApp.Test.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

## Coverage Configuration

The code coverage is configured in `coverlet.runsettings` with the following exclusions:
- Database migrations (module `[GrifballWebApp.Database.Migrations]*`)
- Migration files (file pattern `**/GrifballWebApp.Database/Migrations/*.cs`)

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

### BracketsControllerTests.cs  
- Tests for `SetCustomSeeds` endpoint covering:
  - Parameter passing to service layer
  - Exception propagation
  - Empty array handling

### CustomSeedDtoTests.cs
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
- Use `SetUpFixture.NewGrifballContext()` for database operations
- Use NSubstitute for mocking dependencies
- Use descriptive test method names following `Should_ExpectedBehavior_When_Condition` pattern
- Use `Assert.Multiple()` for grouped assertions