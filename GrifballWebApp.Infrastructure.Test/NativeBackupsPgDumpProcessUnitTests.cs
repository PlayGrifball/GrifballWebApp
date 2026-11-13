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
    /// <summary>A stand-in for pg_dump: a shell script that finds its --file and --dbname arguments (as $file and $db) and runs <paramref name="body"/>.
    /// The caller deletes it.</summary>
    private static string FakePgDump(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"grif-backup-unit-pgdump-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\nwhile [ $# -gt 0 ]; do if [ \"$1\" = --file ]; then file=\"$2\"; fi; if [ \"$1\" = --dbname ]; then db=\"$2\"; fi; shift; done\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    [Test]
    public async Task PgDumpAsync_WritesWhateverTheProcessWritesToItsFile()
    {
        var fake = FakePgDump("printf PGDMP > \"$file\"");
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
        var fake = FakePgDump("echo 'pg_dump: error: server version mismatch' >&2; exit 1");
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
        var fake = FakePgDump("exit 0");
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
        var fake = FakePgDump("sleep 30");
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
