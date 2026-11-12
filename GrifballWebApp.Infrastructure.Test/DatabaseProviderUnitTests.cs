using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using static GrifballWebApp.Infrastructure.Test.DatabaseProviderTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>UseGrifballDatabase and the model it builds, each provider's connection string never opened: no
/// database, host, file or network.</summary>
[TestFixture]
public class DatabaseProviderUnitTests
{
    private const string PostgresConnectionString = "Host=example;Database=Grif;Username=postgres;Password=secret-pg;";
    private const string SqliteConnectionString = "Data Source=grif.db";

    private static GrifballContext Context(DatabaseProvider provider)
    {
        var connectionString = provider switch
        {
            DatabaseProvider.Postgres => PostgresConnectionString,
            DatabaseProvider.Sqlite => SqliteConnectionString,
            _ => SqlServerConnectionString,
        };
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(provider, connectionString).Options);
    }

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

    // UseGrifballDatabase_UsesTheProviderAndItsMigrations: each migrations project's own Test (it needs that
    // provider's migrations assembly loaded) has it, as DatabaseProviderMigrationsTests.

    [Test]
    public void UseGrifballDatabase_FromConfiguration_UsesTheConfiguredProvider()
    {
        using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(Config("Postgres", PostgresConnectionString)).Options);

        Assert.That(context.Database.IsNpgsql(), Is.True);
    }

    [Test]
    public void UseGrifballDatabase_NonGenericBuilder_UsesTheConfiguredProvider()
    {
        var options = new DbContextOptionsBuilder();
        options.UseGrifballDatabase(Config(null));

        using var context = new GrifballContext(new DbContextOptions<GrifballContext>(
            options.Options.Extensions.ToDictionary(e => e.GetType(), e => e)));

        Assert.That(context.Database.IsSqlServer(), Is.True);
    }

    [Test]
    public void UseGrifballDatabase_MissingConnectionString_Throws()
    {
        var ex = Assert.Throws<Exception>(() => new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(Config("Postgres", null)));

        Assert.That(ex!.Message, Is.EqualTo("GrifballContext failed to configure"));
    }

    [Test]
    public void UseGrifballDatabase_UnknownProvider_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase((DatabaseProvider)7, SqlServerConnectionString));
    }

    // Migrations_MatchTheModel, NewestMigration_DesignerMatchesTheSnapshot: each migrations project's own Test (they
    // need that provider's migrations assembly loaded) has them, as DatabaseProviderMigrationsTests.

    [Test]
    public void Postgres_StoresDateTimeAsTimestamptz()
    {
        using var context = Context(DatabaseProvider.Postgres);

        var column = context.Model.FindEntityType(typeof(GrifballWebApp.Domain.League.Season))!
            .FindProperty(nameof(GrifballWebApp.Domain.League.Season.SignupsOpen))!.GetColumnType();

        Assert.That(column, Is.EqualTo("timestamp with time zone"));
    }

    [TestCase(DatabaseProvider.SqlServer)]
    [TestCase(DatabaseProvider.Postgres)]
    [TestCase(DatabaseProvider.Sqlite)]
    public void DateTimes_UseTheUtcConverter(DatabaseProvider provider)
    {
        using var context = Context(provider);

        var withoutConverter = context.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .Where(p => p.GetValueConverter() is not UtcDateTimeConverter)
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}");

        Assert.That(withoutConverter, Is.Empty);
    }

    [Test]
    public void Sqlite_EveryTextColumn_IsCaseInsensitive()
    {
        using var context = Context(DatabaseProvider.Sqlite);

        var otherCollation = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(string))
            .Where(p => p.GetCollation() != GrifballContext.SqliteCaseInsensitiveCollation)
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}");

        Assert.That(otherCollation, Is.Empty);
    }

    // SQLite can't sum, sort or compare TimeSpan or decimal as it stores them (text): numbers instead.
    [TestCase(typeof(GrifballWebApp.Domain.Halo.Match), nameof(GrifballWebApp.Domain.Halo.Match.Duration), typeof(long))]
    [TestCase(typeof(GrifballWebApp.Domain.League.PlayerSeasonStat), nameof(GrifballWebApp.Domain.League.PlayerSeasonStat.WinPercent), typeof(double))]
    public void Sqlite_StoresTimeSpansAndDecimalsAsNumbers(Type entity, string property, Type stored)
    {
        using var context = Context(DatabaseProvider.Sqlite);

        var mapping = context.Model.FindEntityType(entity)!.FindProperty(property)!.GetTypeMapping();

        Assert.That(mapping.Converter?.ProviderClrType, Is.EqualTo(stored));
    }

    // SQLite has no schemas: a table is named by its name alone.
    [Test]
    public void Sqlite_NoTwoTablesShareAName()
    {
        using var context = Context(DatabaseProvider.Sqlite);

        var shared = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel().Tables
            .GroupBy(t => t.Name).Where(g => g.Count() > 1).Select(g => g.Key);

        Assert.That(shared, Is.Empty);
    }

    // The same tables keep history as on Postgres, and each declares its triggers, so EF reads PeriodStart back
    // after a write rather than by RETURNING.
    [Test]
    public void Sqlite_KeepsTheSameHistoryAsPostgres_EachTableDeclaringItsTriggers()
    {
        using var sqlite = Context(DatabaseProvider.Sqlite);
        using var postgres = Context(DatabaseProvider.Postgres);
        static List<string> HistoryTables(GrifballContext c) => c.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetEntityTypes()
            .Where(e => e.FindAnnotation(PostgresHistory.HistoryOfAnnotation) is not null).Select(e => e.GetTableName()!).Order().ToList();

        var withoutTriggers = sqlite.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetEntityTypes()
            .Where(e => e.BaseType is null && e.FindHistoryEntityType() is not null)
            .Where(e => !e.GetDeclaredTriggers().Select(t => t.ModelName).Order()
                .SequenceEqual(new[] { SqliteHistory.DeleteTrigger(e.GetTableName()!), SqliteHistory.UpdateTrigger(e.GetTableName()!) }.Order()))
            .Select(e => e.DisplayName());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(HistoryTables(sqlite), Is.EqualTo(HistoryTables(postgres)).And.Not.Empty);
            Assert.That(withoutTriggers, Is.Empty);
        }
    }

    [Test]
    public void Postgres_EveryTextColumn_IsCaseInsensitive()
    {
        using var context = Context(DatabaseProvider.Postgres);

        var otherCollation = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(string))
            .Where(p => p.GetCollation() != GrifballContext.CaseInsensitiveCollation)
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}");

        Assert.That(otherCollation, Is.Empty);
    }
}

// DesignTimeContextFactoryTests: each migrations project's own Test (it needs that provider's migrations assembly).

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
