#pragma warning disable EF1001 // EF's migrations services, subclassed in place of the Migrations assemblies
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Sqlite.Migrations.Internal;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// A database's migrations as a test sets them, in place of the Migrations assemblies Infrastructure.Test can't
/// reference: the migrations this build knows, those the database has applied, what migrating does (make the model's
/// tables, as EnsureCreated does, or fail), and the script the migrations would write. What was asked of them is kept.
/// </summary>
internal sealed class MigrationsState
{
    public List<string> Known { get; init; } = [];
    public List<string> Applied { get; init; } = [];
    public bool FailMigrate { get; init; }
    public bool FailUndo { get; init; }

    /// <summary>A table dropped after migrating: a target that doesn't have every table of the model.</summary>
    public string? DropAfterMigrate { get; init; }

    public string Script { get; init; } = "-- the target's migrations";

    /// <summary>Each migration asked for: "latest", or "0" (Migration.InitialDatabase: undone).</summary>
    public List<string> MigratedTo { get; } = [];
}

/// <summary>A GrifballContext whose migrations are <see cref="State"/>.</summary>
internal sealed class FakeMigrationsContext(DbContextOptions<GrifballContext> options, MigrationsState state) : GrifballContext(options)
{
    public MigrationsState State { get; } = state;

    /// <summary>The options of a context on the database whose migrations services are this file's fakes.</summary>
    public static DbContextOptions<GrifballContext> Options(DatabaseProvider provider, string connectionString) => new DbContextOptionsBuilder<GrifballContext>()
        .UseGrifballDatabase(provider, connectionString)
        .ReplaceService<IMigrationsAssembly, FakeMigrationsAssembly>()
        .ReplaceService<IHistoryRepository, FakeHistoryRepository>()
        .ReplaceService<IMigrator, FakeMigrator>()
        .Options;

    public static MigrationsState StateOf(ICurrentDbContext current) => ((FakeMigrationsContext)current.Context).State;
}

/// <summary>The migrations this build knows: the state's, by ID. (EF's own MigrationsAssembly loads the Migrations
/// assembly as it's made, so this implements the interface instead.)</summary>
internal sealed class FakeMigrationsAssembly(ICurrentDbContext current) : IMigrationsAssembly
{
    public IReadOnlyDictionary<string, TypeInfo> Migrations
        => FakeMigrationsContext.StateOf(current).Known.ToDictionary(id => id, _ => typeof(Migration).GetTypeInfo());

    public ModelSnapshot? ModelSnapshot => null;

    public Assembly Assembly => typeof(FakeMigrationsAssembly).Assembly;

    public string? FindMigrationId(string nameOrId) => Migrations.Keys.FirstOrDefault(id => id == nameOrId || id.EndsWith("_" + nameOrId, StringComparison.Ordinal));

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider) => throw new NotSupportedException("The fake migrator runs no migration classes");
}

internal sealed class FakeHistoryRepository(HistoryRepositoryDependencies dependencies) : SqliteHistoryRepository(dependencies)
{
    public override Task<IReadOnlyList<HistoryRow>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HistoryRow>>([.. FakeMigrationsContext.StateOf(Dependencies.CurrentContext).Applied.Select(id => new HistoryRow(id, "10.0"))]);
}

internal sealed class FakeMigrator(ICurrentDbContext current) : IMigrator
{
    public void Migrate(string? targetMigration = null) => throw new NotSupportedException("The code migrates asynchronously");

    public async Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default)
    {
        var context = (FakeMigrationsContext)current.Context;
        var state = context.State;
        state.MigratedTo.Add(targetMigration ?? "latest");
        if (targetMigration == Migration.InitialDatabase)
        {
            if (state.FailUndo)
                throw new InvalidOperationException("the down migrations failed");
            // Every migration undone: no tables left
            await context.Database.EnsureDeletedAsync(cancellationToken);
            return;
        }
        if (state.FailMigrate)
            throw new InvalidOperationException("a migration failed");
        await context.Database.EnsureCreatedAsync(cancellationToken);
        if (state.DropAfterMigrate is { } table)
            await context.Database.ExecuteSqlRawAsync(string.Concat("DROP TABLE \"", table, "\""), cancellationToken);
    }

    public string GenerateScript(string? fromMigration = null, string? toMigration = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
        => FakeMigrationsContext.StateOf(current).Script;

    public bool HasPendingModelChanges() => false;
}
