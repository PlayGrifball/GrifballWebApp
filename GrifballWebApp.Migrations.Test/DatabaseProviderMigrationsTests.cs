using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Migrations.Test;

/// <summary>GrifballWebApp.Infrastructure.Test's DatabaseProviderUnitTests' tests that need a migrations assembly
/// loaded (GetMigrations, HasPendingModelChanges, a migration's Designer): no real database, but the connection
/// string is never opened either way.</summary>
[TestFixture]
public class DatabaseProviderMigrationsTests
{
    private const string SqlServerConnectionString = "Server=example;Database=Grif;User Id=sa;Password=secret-sql;";
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

    [TestCase(DatabaseProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer", DatabaseProviderExtensions.SqlServerMigrationsAssembly)]
    [TestCase(DatabaseProvider.Postgres, "Npgsql.EntityFrameworkCore.PostgreSQL", DatabaseProviderExtensions.PostgresMigrationsAssembly)]
    [TestCase(DatabaseProvider.Sqlite, "Microsoft.EntityFrameworkCore.Sqlite", DatabaseProviderExtensions.SqliteMigrationsAssembly)]
    public void UseGrifballDatabase_UsesTheProviderAndItsMigrations(DatabaseProvider provider, string providerName, string migrationsAssembly)
    {
        using var context = Context(provider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Database.ProviderName, Is.EqualTo(providerName));
            Assert.That(context.GetService<IMigrationsAssembly>().Assembly.GetName().Name, Is.EqualTo(migrationsAssembly));
            Assert.That(context.Database.GetMigrations(), Is.Not.Empty);
        }
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Database.IsNpgsql(), Is.EqualTo(provider == DatabaseProvider.Postgres));
                Assert.That(context.Database.IsSqlite(), Is.EqualTo(provider == DatabaseProvider.Sqlite));
                Assert.That(output.ToString(), Does.Contain($"Using {provider} connection string for {logged}"));
                Assert.That(output.ToString(), Does.Not.Contain("hunter2"));
            }
        }
    }
}
