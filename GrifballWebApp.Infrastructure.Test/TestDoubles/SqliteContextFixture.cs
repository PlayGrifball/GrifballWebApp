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

    /// <summary>Disposes <see cref="Context"/> (not set if <see cref="CreateContext"/> threw), and whatever an override
    /// built it with.</summary>
    protected virtual ValueTask DisposeContextAsync() => Context is null ? ValueTask.CompletedTask : Context.DisposeAsync();

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
            await CleanUpAsync();
            throw;
        }
    }

    [TearDown]
    public Task TearDownDatabase() => CleanUpAsync();

    private async Task CleanUpAsync()
    {
        await DisposeContextAsync();
        foreach (var file in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }
}
