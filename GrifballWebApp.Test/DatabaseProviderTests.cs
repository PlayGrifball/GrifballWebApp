using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Test;

[TestFixture]
public class DatabaseProviderTests
{
    private const string SqlServerConnectionString = "Server=example;Database=Grif;User Id=sa;Password=secret-sql;";
    private const string PostgresConnectionString = "Host=example;Database=Grif;Username=postgres;Password=secret-pg;";
    private const string SqliteConnectionString = "Data Source=grif.db";

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

    [TestCase(DatabaseProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer", DatabaseProviderExtensions.SqlServerMigrationsAssembly)]
    [TestCase(DatabaseProvider.Postgres, "Npgsql.EntityFrameworkCore.PostgreSQL", DatabaseProviderExtensions.PostgresMigrationsAssembly)]
    [TestCase(DatabaseProvider.Sqlite, "Microsoft.EntityFrameworkCore.Sqlite", DatabaseProviderExtensions.SqliteMigrationsAssembly)]
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

    // A model change needs a migration in every project (README): this fails for whichever is missing.
    [TestCase(DatabaseProvider.SqlServer)]
    [TestCase(DatabaseProvider.Postgres)]
    [TestCase(DatabaseProvider.Sqlite)]
    public void Migrations_MatchTheModel(DatabaseProvider provider)
    {
        using var context = Context(provider);

        Assert.That(context.Database.HasPendingModelChanges(), Is.False,
            $"The model has changes with no {provider} migration: add one to {context.GetService<IMigrationsAssembly>().Assembly.GetName().Name}");
    }

    // `ef migrations remove` rebuilds the snapshot from the newest remaining migration's Designer, so that Designer must hold the
    // snapshot's model. A migration made before another branch's merged (its Designer missing that branch's changes) fails here:
    // delete it and generate it again on top of master.
    [TestCase(DatabaseProvider.SqlServer)]
    [TestCase(DatabaseProvider.Postgres)]
    [TestCase(DatabaseProvider.Sqlite)]
    public void NewestMigration_DesignerMatchesTheSnapshot(DatabaseProvider provider)
    {
        using var context = Context(provider);
        var migrations = context.GetService<IMigrationsAssembly>();
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var (id, type) = migrations.Migrations.Last();

        var designer = Initialize(initializer, migrations.CreateMigration(type, context.Database.ProviderName!).TargetModel!);
        var snapshot = Initialize(initializer, migrations.ModelSnapshot!.Model);
        var differences = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(designer.GetRelationalModel(), snapshot.GetRelationalModel());

        Assert.That(differences.Select(d => d.GetType().Name), Is.Empty,
            $"{id}'s Designer is not the {provider} snapshot's model: delete the migration and generate it again on top of master");
    }

    // As the migrator does: a model built in code is finalized, then initialized for design time
    private static IModel Initialize(IModelRuntimeInitializer initializer, IModel model)
        => initializer.Initialize(model is IMutableModel mutable ? mutable.FinalizeModel() : model, designTime: true);

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

        Assert.Multiple(() =>
        {
            Assert.That(HistoryTables(sqlite), Is.EqualTo(HistoryTables(postgres)).And.Not.Empty);
            Assert.That(withoutTriggers, Is.Empty);
        });
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

// Console output is process-wide: nothing else may write while it's captured.
[TestFixture, NonParallelizable]
public class DesignTimeContextFactoryTests
{
    [TestCase(DatabaseProvider.SqlServer, "Server=example-sql;Database=Grif;User Id=sa;Password=hunter2-sql;", "server example-sql, database Grif")]
    [TestCase(DatabaseProvider.Postgres, "Host=example-pg;Database=Grif;Username=postgres;Password=hunter2-pg;", "server example-pg, database Grif")]
    [TestCase(DatabaseProvider.Sqlite, "Data Source=/data/grif.db", "file /data/grif.db")]
    public void CreateDbContext_UsesItsProvider_AndLogsNoPassword(DatabaseProvider provider, string connectionString, string logged)
    {
        var args = new[] { $"--ConnectionStrings:GrifballWebApp={connectionString}" };
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        GrifballContext context;
        try
        {
            context = provider switch
            {
                DatabaseProvider.Postgres => new GrifballWebApp.Migrations.Postgres.DesignTimeContextFactory().CreateDbContext(args),
                DatabaseProvider.Sqlite => new GrifballWebApp.Migrations.Sqlite.DesignTimeContextFactory().CreateDbContext(args),
                _ => new GrifballWebApp.Migrations.SqlServer.DesignTimeContextFactory().CreateDbContext(args),
            };
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
                Assert.That(context.Database.IsSqlite(), Is.EqualTo(provider == DatabaseProvider.Sqlite));
                Assert.That(output.ToString(), Does.Contain($"Using {provider} connection string for {logged}"));
                Assert.That(output.ToString(), Does.Not.Contain("hunter2"));
            });
        }
    }
}
