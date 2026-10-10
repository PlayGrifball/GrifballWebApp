using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Test;

[TestFixture]
public class DatabaseProviderTests
{
    private const string SqlServerConnectionString = "Server=example;Database=Grif;User Id=sa;Password=secret-sql;";
    private const string PostgresConnectionString = "Host=example;Database=Grif;Username=postgres;Password=secret-pg;";

    private static IConfiguration Config(string? provider, string? connectionString = SqlServerConnectionString)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider,
            ["ConnectionStrings:GrifballWebApp"] = connectionString,
        }).Build();
    }

    private static GrifballContext Context(DatabaseProvider provider)
    {
        var connectionString = provider == DatabaseProvider.Postgres ? PostgresConnectionString : SqlServerConnectionString;
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(provider, connectionString).Options);
    }

    [TestCase(null, DatabaseProvider.SqlServer)]
    [TestCase("", DatabaseProvider.SqlServer)]
    [TestCase("SqlServer", DatabaseProvider.SqlServer)]
    [TestCase("sqlserver", DatabaseProvider.SqlServer)]
    [TestCase("Postgres", DatabaseProvider.Postgres)]
    [TestCase("POSTGRES", DatabaseProvider.Postgres)]
    public void GetDatabaseProvider_ReadsTheSetting(string? value, DatabaseProvider expected)
    {
        Assert.That(Config(value).GetDatabaseProvider(), Is.EqualTo(expected));
    }

    [TestCase("MySql")]
    [TestCase("7")]
    public void GetDatabaseProvider_UnknownValue_Throws(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Config(value).GetDatabaseProvider());

        Assert.That(ex!.Message, Does.Contain(value).And.Contain("SqlServer, Postgres"));
    }

    [TestCase(DatabaseProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer", DatabaseProviderExtensions.SqlServerMigrationsAssembly)]
    [TestCase(DatabaseProvider.Postgres, "Npgsql.EntityFrameworkCore.PostgreSQL", DatabaseProviderExtensions.PostgresMigrationsAssembly)]
    public void UseGrifballDatabase_UsesTheProviderAndItsMigrations(DatabaseProvider provider, string providerName, string migrationsAssembly)
    {
        using var context = Context(provider);

        Assert.Multiple(() =>
        {
            Assert.That(context.Database.ProviderName, Is.EqualTo(providerName));
            Assert.That(context.GetService<IMigrationsAssembly>().Assembly.GetName().Name, Is.EqualTo(migrationsAssembly));
            Assert.That(context.Database.GetMigrations(), Is.Not.Empty);
        });
    }

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

    // A model change needs a migration in both projects (README): this fails for whichever is missing.
    [TestCase(DatabaseProvider.SqlServer)]
    [TestCase(DatabaseProvider.Postgres)]
    public void Migrations_MatchTheModel(DatabaseProvider provider)
    {
        using var context = Context(provider);

        Assert.That(context.Database.HasPendingModelChanges(), Is.False,
            $"The model has changes with no {provider} migration: add one to {(provider == DatabaseProvider.Postgres ? DatabaseProviderExtensions.PostgresMigrationsAssembly : DatabaseProviderExtensions.SqlServerMigrationsAssembly)}");
    }

    [Test]
    public void Postgres_StoresDateTimeWithoutTimeZone()
    {
        using var context = Context(DatabaseProvider.Postgres);

        var column = context.Model.FindEntityType(typeof(GrifballWebApp.Database.Models.Season))!
            .FindProperty(nameof(GrifballWebApp.Database.Models.Season.SignupsOpen))!.GetColumnType();

        Assert.That(column, Is.EqualTo("timestamp without time zone"));
    }
}

// Console output is process-wide: nothing else may write while it's captured.
[TestFixture, NonParallelizable]
public class DesignTimeContextFactoryTests
{
    [TestCase(DatabaseProvider.SqlServer, "Server=example-sql;Database=Grif;User Id=sa;Password=hunter2-sql;", "server example-sql, database Grif")]
    [TestCase(DatabaseProvider.Postgres, "Host=example-pg;Database=Grif;Username=postgres;Password=hunter2-pg;", "server example-pg, database Grif")]
    public void CreateDbContext_UsesItsProvider_AndLogsNoPassword(DatabaseProvider provider, string connectionString, string logged)
    {
        var args = new[] { $"--ConnectionStrings:GrifballWebApp={connectionString}" };
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        GrifballContext context;
        try
        {
            context = provider == DatabaseProvider.Postgres
                ? new GrifballWebApp.Migrations.Postgres.DesignTimeContextFactory().CreateDbContext(args)
                : new GrifballWebApp.Migrations.SqlServer.DesignTimeContextFactory().CreateDbContext(args);
        }
        finally
        {
            Console.SetOut(original);
        }

        using (context)
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.Database.IsNpgsql(), Is.EqualTo(provider == DatabaseProvider.Postgres));
                Assert.That(output.ToString(), Does.Contain($"Using {provider} connection string for {logged}"));
                Assert.That(output.ToString(), Does.Not.Contain("hunter2"));
            });
        }
    }
}
