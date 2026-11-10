using DotNet.Testcontainers.Containers;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Test;

[SetUpFixture]
public class SetUpFixture
{
    /// <summary>SQL Server or Postgres (<see cref="TestDatabase.Provider"/>); null on SQLite, which has no server.</summary>
    public static IDatabaseContainer? DatabaseContainer { get; private set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        // Temporary Discord responses are deleted at once, not after the app's 5 seconds.
        GrifballWebApp.Server.Extensions.InteractionResponseExtensions.TempResponseLifetime = TimeSpan.Zero;
        DatabaseContainer = await TestDatabase.StartServer();
        await TestDatabase.CreateTemplate(DatabaseContainer);
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        if (DatabaseContainer is not null)
            await DatabaseContainer.DisposeAsync();
    }

    internal static async Task<GrifballContext> NewGrifballContext(params IInterceptor[] interceptors)
    {
        return await Utility.NewGrifballContext(SetUpFixture.DatabaseContainer, interceptors);
    }
}
