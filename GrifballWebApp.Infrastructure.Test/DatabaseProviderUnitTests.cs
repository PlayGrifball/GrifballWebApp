using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using static GrifballWebApp.Infrastructure.Test.DatabaseProviderTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>DatabaseProviderTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class DatabaseProviderUnitTests
{
    [TestCase(null, DatabaseProvider.SqlServer)]
    [TestCase("", DatabaseProvider.SqlServer)]
    [TestCase("SqlServer", DatabaseProvider.SqlServer)]
    [TestCase("sqlserver", DatabaseProvider.SqlServer)]
    [TestCase("Postgres", DatabaseProvider.Postgres)]
    [TestCase("POSTGRES", DatabaseProvider.Postgres)]
    [TestCase("Sqlite", DatabaseProvider.Sqlite)]
    [TestCase("sqlite", DatabaseProvider.Sqlite)]
    public void GetDatabaseProvider_ReadsTheSetting(string? value, DatabaseProvider expected)
    {
        Assert.That(Config(value).GetDatabaseProvider(), Is.EqualTo(expected));
    }

    [TestCase("MySql")]
    [TestCase("7")]
    public void GetDatabaseProvider_UnknownValue_Throws(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Config(value).GetDatabaseProvider());

        Assert.That(ex!.Message, Does.Contain(value).And.Contain("SqlServer, Postgres, Sqlite"));
    }
}

/// <summary>DatabaseProviderTests' helpers, shared with its integration tests.</summary>
internal static class DatabaseProviderTestData
{
    internal const string SqlServerConnectionString = "Server=example;Database=Grif;User Id=sa;Password=secret-sql;";

    internal static IConfiguration Config(string? provider, string? connectionString = SqlServerConnectionString)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider,
            ["ConnectionStrings:GrifballWebApp"] = connectionString,
        }).Build();
    }
}
