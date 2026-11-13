using GrifballWebApp.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using static GrifballWebApp.Integration.Test.BackupTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// NativeBackups: SQL Server's .bak read back through SQL Server. pg_dump's own process handling needs no database server:
/// GrifballWebApp.Infrastructure.Test's NativeBackupsPgDumpProcessUnitTests.
/// </summary>
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
