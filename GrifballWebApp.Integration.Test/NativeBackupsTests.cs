using GrifballWebApp.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using static GrifballWebApp.Integration.Test.BackupTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>NativeBackups: SQL Server's .bak read back through SQL Server, and pg_dump run with the app's connection.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NativeBackupsTests
{
    /// <summary>The test server's folder for bulk reads, as the chart's SQL Server has /var/opt/grif-download: outside /var/opt/mssql, in allowedpathslist.</summary>
    private const string AllowedFolder = "/tmp/grif-download";

    private static readonly Lazy<Task<string>> AllowFolder = new(async () =>
    {
        var container = (DotNet.Testcontainers.Containers.IContainer)SetUpFixture.DatabaseContainer!;
        var result = await container.ExecAsync(["sh", "-c",
            $"mkdir -p {AllowedFolder} && /opt/mssql/bin/mssql-conf set bulkadmin.allowedpathslist {AllowedFolder}"]);
        return $"exit {result.ExitCode}: {result.Stdout} {result.Stderr}";
    });

    [Test]
    public async Task PgDump_writes_the_archive()
    {
        var fake = FakePgDump("printf PGDMP > \"$file\"");
        var output = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.dump");
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
    public async Task PgDump_failing_says_why()
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
    public async Task PgDump_started_while_its_file_is_still_open_for_writing_is_tried_again()
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
    public async Task PgDump_is_stopped_when_the_download_is()
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
    public async Task PgDump_missing_is_unavailable()
    {
        var e = await Assert.ThrowsAsync<NativeBackupUnavailableException>(() => NativeBackups.PgDumpAsync("Host=db;Database=d", "/nonexistent/pg_dump", "/tmp/unused.dump", CancellationToken.None));

        Assert.That(e!.Message, Does.Contain("PostgreSQL script instead"));
    }

    [Test]
    public async Task SqlServer_backs_up_with_the_charts_rights_into_a_folder_allowed_for_bulk_reads()
    {
        if (TestDatabase.Provider != DatabaseProvider.SqlServer)
            Assert.Ignore("SQL Server only.");
        var allowed = await AllowFolder.Value;
        var context = await TestDatabase.NewGrifballContext();
        var output = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.bak");
        try
        {
            var login = await NativeBackupLoginAsync(context.Database.GetConnectionString()!);

            await NativeBackups.SqlServerAsync(login, AllowedFolder, output, CancellationToken.None);

            await using var file = File.OpenRead(output);
            var magic = new byte[4];
            await file.ReadExactlyAsync(magic);
            Assert.That(System.Text.Encoding.ASCII.GetString(magic), Is.EqualTo(CompressedBakHeader), allowed);
        }
        finally
        {
            File.Delete(output);
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task SqlServer_without_sysadmin_cant_read_back_from_its_own_folders_and_says_why()
    {
        if (TestDatabase.Provider != DatabaseProvider.SqlServer)
            Assert.Ignore("SQL Server only.");
        await AllowFolder.Value;
        var context = await TestDatabase.NewGrifballContext();
        try
        {
            var login = await NativeBackupLoginAsync(context.Database.GetConnectionString()!);

            // /var/opt/mssql: never allowed for bulk reads, so not the backup folder either.
            var e = await Assert.ThrowsAsync<NativeBackupUnavailableException>(() => NativeBackups.SqlServerAsync(login, "/var/opt/mssql/data/",
                Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.bak"), CancellationToken.None));

            Assert.That(e!.Message, Does.Contain("allowedpathslist").And.Contain("instead"));
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task SqlServer_backs_up_into_the_folder_given()
    {
        if (TestDatabase.Provider != DatabaseProvider.SqlServer)
            Assert.Ignore("SQL Server only.");
        var context = await TestDatabase.NewGrifballContext();
        var output = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.bak");
        try
        {
            await NativeBackups.SqlServerAsync(context.Database.GetConnectionString()!, "/var/opt/mssql/data/", output, CancellationToken.None);

            await using var file = File.OpenRead(output);
            var magic = new byte[4];
            await file.ReadExactlyAsync(magic);
            Assert.That(System.Text.Encoding.ASCII.GetString(magic), Is.EqualTo(CompressedBakHeader));
        }
        finally
        {
            File.Delete(output);
            await context.DropDatabaseAndDispose();
        }
    }
}
