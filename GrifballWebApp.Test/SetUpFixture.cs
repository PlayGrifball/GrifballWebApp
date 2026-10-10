using DotNet.Testcontainers.Containers;
using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Test;

[SetUpFixture]
public class SetUpFixture
{
    /// <summary>SQL Server or Postgres (<see cref="TestDatabase.Provider"/>).</summary>
    public static IDatabaseContainer DatabaseContainer { get; private set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        DatabaseContainer = await TestDatabase.StartServer();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await DatabaseContainer.DisposeAsync();
    }

    public static async Task<GrifballContext> NewGrifballContext(params IInterceptor[] interceptors)
    {
        return await Utility.NewGrifballContext(SetUpFixture.DatabaseContainer, interceptors);
    }
}
