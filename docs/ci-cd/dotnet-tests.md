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
- **Database Testing**: Testcontainers with SQL Server 2022
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

### Database Testing
The tests use Testcontainers to provision isolated SQL Server instances:
- **Image**: `mcr.microsoft.com/mssql/server:2022-latest`
- **Isolation**: Each test gets a unique database
- **Cleanup**: Automatic container disposal after tests

### Environment Variables
- `TESTCONTAINERS_REUSE_ENABLE=false`: Ensures clean test environments
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
dotnet restore GrifballWebApp.sln
dotnet build GrifballWebApp.sln --configuration Release
dotnet test GrifballWebApp.Test/GrifballWebApp.Test.csproj --configuration Release

# With coverage
dotnet test GrifballWebApp.Test/GrifballWebApp.Test.csproj --collect:"XPlat Code Coverage"
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