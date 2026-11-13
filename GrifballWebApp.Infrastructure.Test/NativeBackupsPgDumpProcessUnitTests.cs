using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// NativeBackups.PgDumpAsync, run against a fake pg_dump (a shell script): no database server or network, so these stay
/// here rather than in GrifballWebApp.Integration.Test (which keeps SqlServerAsync's own tests: a real SQL Server).
/// The fake is a <c>#!/bin/sh</c> script, so not on Windows. Each test works in a temp directory of its own, deleted
/// after it.
/// </summary>
[TestFixture]
[Platform(Exclude = "Win")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class NativeBackupsPgDumpProcessUnitTests
{
    private string _directory = null!;

    private string Output => Path.Combine(_directory, "backup.dump");

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory("grif-pgdump-test-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    private string FakePgDump(string body) => PgDumpTestData.FakePgDump(body, _directory);

    [Test]
    public async Task PgDumpAsync_WritesWhateverTheProcessWritesToItsFile()
    {
        var fake = FakePgDump("printf PGDMP > \"$file\"");

        await NativeBackups.PgDumpAsync("Host=db;Database=d", fake, Output, CancellationToken.None);

        Assert.That(await File.ReadAllTextAsync(Output), Is.EqualTo("PGDMP"));
    }

    [Test]
    public async Task PgDumpAsync_NonZeroExit_ThrowsWithItsStandardError()
    {
        var fake = FakePgDump("echo 'pg_dump: error: server version mismatch' >&2; exit 1");

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => NativeBackups.PgDumpAsync("Host=db;Database=d", fake, Output, CancellationToken.None));

        Assert.That(e!.Message, Is.EqualTo("pg_dump failed (1): pg_dump: error: server version mismatch"));
    }

    [Test]
    public async Task PgDumpAsync_StartedWhileItsFileIsStillOpenForWriting_IsTriedAgain()
    {
        // As when another test (or thread) was starting a process while the file was written: "Text file busy"
        var fake = FakePgDump("exit 0");
        var writer = new FileStream(fake, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        var dump = NativeBackups.PgDumpAsync("Host=db;Database=d", fake, Output, CancellationToken.None);
        await Task.Delay(30);
        await writer.DisposeAsync();

        Assert.That(async () => await dump, Throws.Nothing);
    }

    [Test]
    public async Task PgDumpAsync_CancelledWhileRunning_KillsTheProcess()
    {
        var fake = FakePgDump("sleep 30");

        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.CatchAsync<OperationCanceledException>(() => NativeBackups.PgDumpAsync("Host=db;Database=d", fake, Output, cancel.Token));
    }

    [Test]
    public async Task PgDumpAsync_ExecutableMissing_IsReportedAsUnavailable()
    {
        var e = await Assert.ThrowsAsync<NativeBackupUnavailableException>(() => NativeBackups.PgDumpAsync("Host=db;Database=d", Path.Combine(_directory, "no-pg_dump"), Output, CancellationToken.None));

        Assert.That(e!.Message, Does.Contain("PostgreSQL script instead"));
    }
}
