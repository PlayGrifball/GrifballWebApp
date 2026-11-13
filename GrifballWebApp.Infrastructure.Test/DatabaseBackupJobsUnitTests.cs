using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The Database Backup page's jobs: DatabaseBackupJob makes a backup (a PostgreSQL dump, by a fake pg_dump, so no
/// server) and keeps it in BackupFiles, its report naming the file; DatabaseExportJob unprotects its target and
/// exports, each failure reported with what went wrong. Each test has its own temp folder for the kept files and the
/// fake pg_dump.
/// </summary>
[TestFixture]
[Platform(Exclude = "Win")] // the fake pg_dump is a #!/bin/sh script
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DatabaseBackupJobsUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] BackupMadeLog = ["admin's dump backup is made: grifball-backup-20261113-080000.dump"];

    private string _directory = null!;
    private BackupFiles _files = null!;
    private readonly IJobExecutionContext _run = Substitute.For<IJobExecutionContext>();
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("grif-backupjobs-test-").FullName;
        _files = new BackupFiles(Configuration(("DatabaseBackup:Directory", Path.Combine(_directory, "kept"))), new FakeTimeProvider(Now));
    }

    [TearDown]
    public void TearDown()
    {
        _files.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>The app on PostgreSQL, dumped by a fake pg_dump that runs <paramref name="pgDumpBody"/>.</summary>
    private DatabaseBackupService PostgresService(string pgDumpBody) => new(
        Configuration(("Database:Provider", "Postgres"), ("ConnectionStrings:GrifballWebApp", "Host=db;Database=d"),
            ("DatabaseBackup:PgDump", PgDumpTestData.FakePgDump(pgDumpBody, _directory))),
        NullLoggerFactory.Instance, new FakeTimeProvider(Now));

    /// <summary>The app on a SQLite file that isn't there: any conversion of it stops at once.</summary>
    private DatabaseBackupService MissingSqliteService() => new(
        Configuration(("Database:Provider", "Sqlite"), ("ConnectionStrings:GrifballWebApp", $"Data Source={Path.Combine(_directory, "missing.db")}")),
        NullLoggerFactory.Instance, new FakeTimeProvider(Now));

    private readonly RecordingLogger<DatabaseBackupJob> _backupLog = new();

    private DatabaseBackupJob BackupJob(DatabaseBackupService service, IConfiguration? configuration = null)
        => new(service, _files, configuration ?? Configuration(), _backupLog);

    private JobRunReport Report() => (JobRunReport)_run.Result!;

    [Test]
    public async Task Backup_Made_IsKept_AndItsReportNamesTheFile()
    {
        await BackupJob(PostgresService("printf PGDMP > \"$file\""), Configuration(("DatabaseBackup:NodeUrl", "http://10.42.0.7:5295")))
            .Execute(_run, new BackupRequest(BackupFormat.Dump, "admin"));

        var report = Report();
        Assert.That(report.Result, Is.EqualTo(JobRunResult.Succeeded), report.Summary);
        var metrics = report.Metrics!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Summary, Is.EqualTo("grifball-backup-20261113-080000.dump: 0 tables, 0 rows"));
            Assert.That(metrics[BackupRuns.FileNameMetric], Is.EqualTo("grifball-backup-20261113-080000.dump"));
            Assert.That(metrics[BackupRuns.ContentTypeMetric], Is.EqualTo("application/octet-stream"));
            Assert.That(metrics[BackupRuns.SizeMetric], Is.EqualTo(5L));
            Assert.That(metrics[BackupRuns.FormatMetric], Is.EqualTo("dump"));
            Assert.That((metrics[BackupRuns.TablesMetric], metrics[BackupRuns.RowsMetric]), Is.EqualTo(((object?)0, (object?)0)));
            Assert.That(metrics[BackupRuns.NodeMetric], Is.EqualTo("http://10.42.0.7:5295"));
        }
        await using var kept = _files.Open((string)metrics[BackupRuns.FileMetric]!);
        Assert.That(kept, Is.Not.Null.And.Property(nameof(Stream.CanRead)).True);
        using var reader = new StreamReader(kept!);
        Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("PGDMP"));
        _run.Received().ReportProgress(0, "Making the dump backup");
        Assert.That(_backupLog.At(Microsoft.Extensions.Logging.LogLevel.Information), Is.EqualTo(BackupMadeLog));
    }

    [Test]
    public async Task Backup_OnTheOnePod_RecordsNoNode()
    {
        await BackupJob(PostgresService("printf PGDMP > \"$file\"")).Execute(_run, new BackupRequest(BackupFormat.Dump, "admin"));

        Assert.That(Report().Metrics![BackupRuns.NodeMetric], Is.Null);
    }

    [Test]
    public async Task Backup_TheNextOfAFormat_ReplacesTheOneKept()
    {
        var job = BackupJob(PostgresService("printf PGDMP > \"$file\""));
        await job.Execute(_run, new BackupRequest(BackupFormat.Dump, "admin"));
        var first = (string)Report().Metrics![BackupRuns.FileMetric]!;

        await job.Execute(_run, new BackupRequest(BackupFormat.Dump, "admin"));
        var second = (string)Report().Metrics![BackupRuns.FileMetric]!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(_files.Exists(first), Is.False, "discarded as the next one started");
            Assert.That(_files.Exists(second), Is.True);
        }
    }

    [Test]
    public async Task Backup_ThatFails_IsReportedFailed_WithWhy_AndItsFormat()
    {
        await BackupJob(PostgresService("echo 'connection refused' >&2; exit 1")).Execute(_run, new BackupRequest(BackupFormat.Dump, "admin"));

        var report = Report();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Failed));
            Assert.That(report.Summary, Is.EqualTo("pg_dump failed (1): connection refused"));
            Assert.That(report.Metrics![BackupRuns.FormatMetric], Is.EqualTo("dump"));
        }
    }

    [Test]
    public async Task Backup_OfANativeFormatWithNoServerForIt_IsReportedFailed_SayingWhatToDo()
    {
        await BackupJob(MissingSqliteService()).Execute(_run, new BackupRequest(BackupFormat.Bak, "admin"));

        Assert.That(Report().Summary, Does.StartWith("A .bak needs SQL Server, which the site doesn't run on"));
    }

    [Test]
    public async Task Export_ReadsItsProtectedTarget_AndReportsTheConvertersReason()
    {
        var target = $"Data Source={Path.Combine(_directory, "copy.db")}";
        var request = new ExportRequest(DatabaseProvider.Sqlite, BackupRuns.ExportProtector(_protection).Protect(target, BackupRuns.ExportRequestLifetime), "admin");

        await new DatabaseExportJob(MissingSqliteService(), _protection, NullLogger<DatabaseExportJob>.Instance).Execute(_run, request);

        var report = Report();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Failed));
            Assert.That(report.Summary, Is.EqualTo("The source database doesn't exist."));
            Assert.That(report.Metrics![BackupRuns.ProviderMetric], Is.EqualTo("Sqlite"));
        }
        _run.Received().ReportProgress(0, "Exporting to Sqlite");
    }

    [Test]
    public async Task Export_WhoseConnectionStringCanNoLongerBeRead_AsksForItAgain()
    {
        var request = new ExportRequest(DatabaseProvider.Postgres, "not-a-protected-payload", "admin");

        await new DatabaseExportJob(MissingSqliteService(), _protection, NullLogger<DatabaseExportJob>.Instance).Execute(_run, request);

        var report = Report();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Failed));
            Assert.That(report.Summary, Is.EqualTo("The export waited too long to start, and its connection string is no longer readable: start it again."));
            Assert.That(report.Metrics![BackupRuns.ProviderMetric], Is.EqualTo("Postgres"));
        }
    }

    [Test]
    public void JobProgress_ReportsThePercentAndMessage_OnTheRun()
    {
        new JobProgress(_run).Report(new ConversionProgress(3, 4, "Copying Teams (4 of 4)"));

        _run.Received(1).ReportProgress(75, "Copying Teams (4 of 4)");
    }
}
