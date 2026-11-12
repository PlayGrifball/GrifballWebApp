using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Migrations.Sqlite.Test;

/// <summary>UseGrifballDatabase(Sqlite), the tests that need the migrations assembly loaded (GetMigrations,
/// HasPendingModelChanges, a migration's Designer): no real database, the connection string never opened.</summary>
[TestFixture]
public class DatabaseProviderMigrationsTests
{
    private const string ConnectionString = "Data Source=grif.db";

    private static GrifballContext Context() =>
        new(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString).Options);

    [Test]
    public void UseGrifballDatabase_UsesTheProviderAndItsMigrations()
    {
        using var context = Context();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Database.ProviderName, Is.EqualTo("Microsoft.EntityFrameworkCore.Sqlite"));
            Assert.That(context.GetService<IMigrationsAssembly>().Assembly.GetName().Name, Is.EqualTo(DatabaseProviderExtensions.SqliteMigrationsAssembly));
            Assert.That(context.Database.GetMigrations(), Is.Not.Empty);
        }
    }

    // A model change needs a migration in every project (README): this fails for whichever is missing.
    [Test]
    public void Migrations_MatchTheModel()
    {
        using var context = Context();

        Assert.That(context.Database.HasPendingModelChanges(), Is.False,
            $"The model has changes with no Sqlite migration: add one to {context.GetService<IMigrationsAssembly>().Assembly.GetName().Name}");
    }

    // `ef migrations remove` rebuilds the snapshot from the newest remaining migration's Designer, so that Designer must hold the
    // snapshot's model. A migration made before another branch's merged (its Designer missing that branch's changes) fails here:
    // delete it and generate it again on top of master.
    [Test]
    public void NewestMigration_DesignerMatchesTheSnapshot()
    {
        using var context = Context();
        var migrations = context.GetService<IMigrationsAssembly>();
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var (id, type) = migrations.Migrations.Last();

        var designer = Initialize(initializer, migrations.CreateMigration(type, context.Database.ProviderName!).TargetModel!);
        var snapshot = Initialize(initializer, migrations.ModelSnapshot!.Model);
        var differences = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(designer.GetRelationalModel(), snapshot.GetRelationalModel());

        Assert.That(differences.Select(d => d.GetType().Name), Is.Empty,
            $"{id}'s Designer is not the Sqlite snapshot's model: delete the migration and generate it again on top of master");
    }

    // As the migrator does: a model built in code is finalized, then initialized for design time
    private static IModel Initialize(IModelRuntimeInitializer initializer, IModel model)
        => initializer.Initialize(model is IMutableModel mutable ? mutable.FinalizeModel() : model, designTime: true);
}

// Console output is process-wide: nothing else may write while it's captured.
[TestFixture, NonParallelizable]
public class DesignTimeContextFactoryTests
{
    [Test]
    public void CreateDbContext_UsesItsProvider_AndLogsNoPassword()
    {
        const string connectionString = "Data Source=/data/grif.db";
        var args = new[] { $"--ConnectionStrings:GrifballWebApp={connectionString}" };
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        GrifballContext context;
        try
        {
            context = new DesignTimeContextFactory().CreateDbContext(args);
        }
        finally
        {
            Console.SetOut(original);
        }

        using (context)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Database.IsNpgsql(), Is.False);
                Assert.That(context.Database.IsSqlite(), Is.True);
                Assert.That(output.ToString(), Does.Contain("Using Sqlite connection string for file /data/grif.db"));
                Assert.That(output.ToString(), Does.Not.Contain("hunter2"));
            }
        }
    }
}
