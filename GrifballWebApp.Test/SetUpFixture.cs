using GrifballWebApp.Database;
using Testcontainers.MsSql;

namespace GrifballWebApp.Test;

[SetUpFixture]
public class SetUpFixture
{
    public static MsSqlContainer MsSqlContainer { get; private set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        MsSqlContainer = await Utility.NewSqlServer();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await MsSqlContainer.DisposeAsync();
    }

    public static async Task<GrifballContext> NewGrifballContext()
    {
        return await Utility.NewGrifballContext(SetUpFixture.MsSqlContainer);
    }
}
