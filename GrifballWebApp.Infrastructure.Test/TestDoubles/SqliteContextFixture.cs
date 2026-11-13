using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// A real <see cref="GrifballContext"/> on a temp-file SQLite database for each test case: no database server, host or
/// network. A fresh file per test (use with <see cref="LifeCycle.InstancePerTestCase"/>), so tests run in parallel;
/// foreign keys and check constraints are enforced as in production. The base [SetUp] runs before a fixture's own and
/// the base [TearDown] after, so <see cref="Context"/> is ready in a fixture's [SetUp] and still open in its [TearDown].
/// If creating the schema fails, the context is disposed and the file deleted right there: NUnit skips [TearDown] when
/// [SetUp] throws.
/// </summary>
public abstract class SqliteContextFixture
{
    protected GrifballContext Context { get; private set; } = null!;

    /// <summary>The test's database file.</summary>
    protected string DatabasePath { get; private set; } = null!;

    /// <summary>
    /// The test's connection string. Pooling=False: tests run in parallel, each on its own file that is never reused,
    /// so pooling gains nothing, and a pooled connection could keep the file open after the test.
    /// </summary>
    protected string ConnectionString => $"Data Source={DatabasePath};Pooling=False";

    /// <summary>The test's context, on <see cref="ConnectionString"/>. Overridden to build it some other way (through
    /// dependency injection, with interceptors).</summary>
    protected virtual GrifballContext CreateContext() =>
        new(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString).Options);

    /// <summary>Disposes whatever an override of <see cref="CreateContext"/> built the context with (a DI scope and
    /// container), after the context itself. Nothing by default.</summary>
    protected virtual ValueTask DisposeContextOwnersAsync() => ValueTask.CompletedTask;

    [SetUp]
    public async Task SetUpDatabase()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"grif-infrastructure-test-{Guid.NewGuid():N}.db");
        try
        {
            Context = CreateContext();
            await Context.Database.EnsureCreatedAsync();
        }
        catch
        {
            if (Context is not null)
                await Context.DisposeAsync();
            await DisposeContextOwnersAsync();
            DeleteFiles();
            throw;
        }
    }

    [TearDown]
    public async Task TearDownDatabase()
    {
        await Context.DisposeAsync();
        await DisposeContextOwnersAsync();
        DeleteFiles();
    }

    private void DeleteFiles() => DeleteDatabaseFiles(DatabasePath);

    /// <summary>A SQLite database file and the -wal and -shm files beside it, whichever exist: for a test that makes its own
    /// database file outside this fixture.</summary>
    internal static void DeleteDatabaseFiles(string path)
    {
        foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }
}
