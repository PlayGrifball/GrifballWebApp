using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DatabaseBackupService.CreateAsync's native-backup path (BuildAsync, NativeBackupAsync, NativeAsync) for a
/// PostgreSQL dump made on the app's own server (no scratch server, so no DatabaseConverter): a fake pg_dump writes
/// the archive CreateAsync hands back as a file. DatabaseConverter's own paths (Sqlite/Postgres/SqlServer scripts,
/// ExportAsync, a scratch server) stay GrifballWebApp.Integration.Test's: they need a real database.
/// </summary>
[TestFixture]
[Platform(Exclude = "Win")] // the fake pg_dump is a #!/bin/sh script
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DatabaseBackupServiceDumpUnitTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory("grif-backupservice-test-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    private static DatabaseBackupService Service(string pgDumpPath) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["ConnectionStrings:GrifballWebApp"] = "Host=db;Database=d",
            ["DatabaseBackup:PgDump"] = pgDumpPath,
        }).Build(),
        NullLoggerFactory.Instance,
        new FakeTimeProvider(new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero)));

    [Test]
    public async Task CreateAsync_PostgresDumpOnItsOwnServer_WritesWhatPgDumpWrites()
    {
        var fake = PgDumpTestData.FakePgDump("printf PGDMP > \"$file\"", _directory);
        var service = Service(fake);

        var built = await service.CreateAsync(BackupFormat.Dump, CancellationToken.None);
        Assert.That(built, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(built!.FileName, Is.EqualTo("grifball-backup-20261113-080000.dump"));
            Assert.That(built.ContentType, Is.EqualTo("application/octet-stream"));
            Assert.That(built.Tables, Is.Empty);
        }
        using (built!.File)
        {
            using var reader = new StreamReader(built.File, leaveOpen: true);
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("PGDMP"));
        }
    }

    /// <summary>
    /// While one backup runs, a second is refused. CreateAsync takes its gate before its first await, so once the first
    /// call has returned its task the gate is held: no delay needed. The fake pg_dump runs until the test lets it go
    /// (a marker file), not for a fixed time.
    /// </summary>
    [Test]
    public async Task CreateAsync_AnotherAlreadyRunning_ReturnsNull()
    {
        var release = Path.Combine(_directory, "release");
        // Writes the (empty) file first, so CreateAsync can open it once pg_dump exits; gives up after ~30 s regardless
        var fake = PgDumpTestData.FakePgDump(
            $"printf '' > \"$file\"; i=0; while [ ! -e '{release}' ] && [ $i -lt 600 ]; do sleep 0.05; i=$((i+1)); done", _directory);
        var service = Service(fake);
        var first = service.CreateAsync(BackupFormat.Dump, CancellationToken.None);

        var second = await service.CreateAsync(BackupFormat.Dump, CancellationToken.None);
        await File.WriteAllTextAsync(release, "");

        Assert.That(second, Is.Null);
        var done = await first;
        await done!.File.DisposeAsync();
    }
}
