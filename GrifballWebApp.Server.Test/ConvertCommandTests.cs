using GrifballWebApp.Convert;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrifballWebApp.Server.Test;

/// <summary>DatabaseConversionTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ConvertCommandTests
{
    [Test]
    public async Task Command_TakesItsSettingsFromTheCommandLine()
    {
        string[] args = ["--Source:Provider=Oracle", "--Source:ConnectionString=x", "--Target:Provider=Postgres", "--Target:ConnectionString=x"];

        Assert.That(await ConvertCommand.MainAsync(args), Is.EqualTo(2));
    }

    [TestCase(null, "x", "Postgres", "x", TestName = "Command_NeedsTheSourceProvider")]
    [TestCase("Oracle", "x", "Postgres", "x", TestName = "Command_NeedsAKnownProvider")]
    [TestCase("SqlServer", null, "Postgres", "x", TestName = "Command_NeedsTheSourceConnectionString")]
    [TestCase("SqlServer", "x", null, "x", TestName = "Command_NeedsTheTargetProvider")]
    [TestCase("SqlServer", "x", "Postgres", null, TestName = "Command_NeedsTheTargetConnectionString")]
    public async Task Command_Settings(string? sourceProvider, string? sourceConnection, string? targetProvider, string? targetConnection)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Source:Provider"] = sourceProvider,
            ["Source:ConnectionString"] = sourceConnection,
            ["Target:Provider"] = targetProvider,
            ["Target:ConnectionString"] = targetConnection,
        }).Build();

        Assert.That(await ConvertCommand.RunAsync(configuration, NullLoggerFactory.Instance), Is.EqualTo(2));
    }
}
