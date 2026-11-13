using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// NativeBackups.PgDumpAsync, run against a fake pg_dump (a shell script): no database server or network, so these stay
/// here rather than in GrifballWebApp.Integration.Test (which keeps SqlServerAsync's own tests: a real SQL Server).
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NativeBackupsPgDumpProcessUnitTests
{
    [Test]
    public async Task PgDumpAsync_WritesWhateverTheProcessWritesToItsFile()
    {
        var fake = PgDumpTestData.FakePgDump("printf PGDMP > \"$file\"");
        var output = Path.Combine(Path.GetTempPath(), $"grif-backup-unit-test-{Guid.NewGuid():N}.dump");
        try
        {
            await NativeBackups.PgDumpAsync("Host=db;Database=d", fake, output, CancellationToken.None);

            Assert.That(await File.ReadAllTextAsync(output), Is.EqualTo("PGDMP"));
        }
        finally
        {
            File.Delete(fake);
            File.Delete(output);
        }
    }

    [Test]
    public async Task PgDumpAsync_NonZeroExit_ThrowsWithItsStandardError()
    {
        var fake = PgDumpTestData.FakePgDump("echo 'pg_dump: error: server version mismatch' >&2; exit 1");
        try
        {
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => NativeBackups.PgDumpAsync("Host=db;Database=d", fake, "/tmp/unused.dump", CancellationToken.None));

            Assert.That(e!.Message, Is.EqualTo("pg_dump failed (1): pg_dump: error: server version mismatch"));
        }
        finally
        {
            File.Delete(fake);
        }
    }

    [Test]
    public async Task PgDumpAsync_StartedWhileItsFileIsStillOpenForWriting_IsTriedAgain()
    {
        // As when another test (or thread) was starting a process while the file was written: "Text file busy"
        var fake = PgDumpTestData.FakePgDump("exit 0");
        try
        {
            var writer = new FileStream(fake, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            var dump = NativeBackups.PgDumpAsync("Host=db;Database=d", fake, "/tmp/unused.dump", CancellationToken.None);
            await Task.Delay(30);
            await writer.DisposeAsync();

            Assert.That(async () => await dump, Throws.Nothing);
        }
        finally
        {
            File.Delete(fake);
        }
    }

    [Test]
    public async Task PgDumpAsync_CancelledWhileRunning_KillsTheProcess()
    {
        var fake = PgDumpTestData.FakePgDump("sleep 30");
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await Assert.CatchAsync<OperationCanceledException>(() => NativeBackups.PgDumpAsync("Host=db;Database=d", fake, "/tmp/unused.dump", cancel.Token));
        }
        finally
        {
            File.Delete(fake);
        }
    }

    [Test]
    public async Task PgDumpAsync_ExecutableMissing_IsReportedAsUnavailable()
    {
        var e = await Assert.ThrowsAsync<NativeBackupUnavailableException>(() => NativeBackups.PgDumpAsync("Host=db;Database=d", "/nonexistent/pg_dump", "/tmp/unused.dump", CancellationToken.None));

        Assert.That(e!.Message, Does.Contain("PostgreSQL script instead"));
    }
}
