using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using static GrifballWebApp.Infrastructure.Test.DatabaseProviderTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>DatabaseProviderUnitTests' tests that need a migrations assembly loaded (GetMigrations,
/// HasPendingModelChanges, a migration's Designer): one subclass per provider, in that provider's own
/// Migrations.*.Test project (Infrastructure.Test itself may not reference the migrations projects), giving its
/// provider, connection string and migrations assembly name. Its own migrations assembly stays the only one
/// loaded, so each provider's coverage is still its own.</summary>
public abstract class DatabaseProviderMigrationsTestsBase
{
    protected abstract DatabaseProvider Provider { get; }
    protected abstract string ProviderName { get; }
    protected abstract string ConnectionString { get; }
    protected abstract string MigrationsAssemblyName { get; }

    private GrifballContext Context() =>
        new(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(Provider, ConnectionString).Options);

    [Test]
    public void UseGrifballDatabase_UsesTheProviderAndItsMigrations()
    {
        using var context = Context();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Database.ProviderName, Is.EqualTo(ProviderName));
            Assert.That(context.GetService<IMigrationsAssembly>().Assembly.GetName().Name, Is.EqualTo(MigrationsAssemblyName));
            Assert.That(context.Database.GetMigrations(), Is.Not.Empty);
        }
    }

    // A model change needs a migration in every project (README): this fails for whichever is missing.
    [Test]
    public void Migrations_MatchTheModel()
    {
        using var context = Context();

        Assert.That(context.Database.HasPendingModelChanges(), Is.False,
            $"The model has changes with no {Provider} migration: add one to {context.GetService<IMigrationsAssembly>().Assembly.GetName().Name}");
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
            $"{id}'s Designer is not the {Provider} snapshot's model: delete the migration and generate it again on top of master");
    }

    // As the migrator does: a model built in code is finalized, then initialized for design time
    private static IModel Initialize(IModelRuntimeInitializer initializer, IModel model)
        => initializer.Initialize(model is IMutableModel mutable ? mutable.FinalizeModel() : model, designTime: true);
}

/// <summary>Each provider's DesignTimeContextFactory (dotnet ef's own entry point): one subclass per provider, in
/// that provider's own Migrations.*.Test project, giving its provider, connection string, the connection's logged
/// form and how to create the context. Console output is process-wide: nothing else may write while it's
/// captured.</summary>
public abstract class DesignTimeContextFactoryTestsBase
{
    protected abstract DatabaseProvider Provider { get; }
    protected abstract string ConnectionString { get; }
    protected abstract string Logged { get; }
    protected abstract GrifballContext CreateDbContext(string[] args);

    [Test]
    public void CreateDbContext_UsesItsProvider_AndLogsNoPassword()
    {
        var args = new[] { $"--ConnectionStrings:GrifballWebApp={ConnectionString}" };
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        GrifballContext context;
        try
        {
            context = CreateDbContext(args);
        }
        finally
        {
            Console.SetOut(original);
        }

        using (context)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Database.IsNpgsql(), Is.EqualTo(Provider == DatabaseProvider.Postgres));
                Assert.That(context.Database.IsSqlite(), Is.EqualTo(Provider == DatabaseProvider.Sqlite));
                Assert.That(output.ToString(), Does.Contain($"Using {Provider} connection string for {Logged}"));
                Assert.That(output.ToString(), Does.Not.Contain("hunter2"));
            }
        }
    }
}
