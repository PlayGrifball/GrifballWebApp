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
[Parallelizable(ParallelScope.All)]
public class DatabaseBackupServiceDumpUnitTests
{
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
        var fake = NativeBackupsPgDumpProcessUnitTests.FakePgDump("printf PGDMP > \"$file\"");
        try
        {
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
        finally
        {
            File.Delete(fake);
        }
    }

    [Test]
    public async Task CreateAsync_AnotherAlreadyRunning_ReturnsNull()
    {
        // Writes the (empty) file before sleeping, so CreateAsync can still open it once pg_dump exits
        var fake = NativeBackupsPgDumpProcessUnitTests.FakePgDump("printf '' > \"$file\"; sleep 2");
        try
        {
            var service = Service(fake);
            var first = service.CreateAsync(BackupFormat.Dump, CancellationToken.None);
            await Task.Delay(300);

            var second = await service.CreateAsync(BackupFormat.Dump, CancellationToken.None);

            Assert.That(second, Is.Null);
            var done = await first;
            await done!.File.DisposeAsync();
        }
        finally
        {
            File.Delete(fake);
        }
    }
}
