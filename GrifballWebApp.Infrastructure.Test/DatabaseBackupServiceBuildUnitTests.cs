using System.Data.Common;
using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DatabaseBackupService.MakeAsync and ExportAsync without a database server: a dump by a fake pg_dump; a backup or
/// export of a source that isn't there, which the converter refuses before writing anything (and the next one still
/// runs: the one-at-a-time gate is let go); and a native backup on a scratch server started on demand that never lets
/// its administrator in, which stops the scratch server again. A copy that succeeds needs a migrated source (the
/// Migrations tests').
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DatabaseBackupServiceBuildUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] ScratchStartReport = ["Starting the scratch Postgres server"];
    private string _directory = null!;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory("grif-backupbuild-test-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    private string MissingSource => $"Data Source={Path.Combine(_directory, "missing.db")}";

    /// <summary>The app on a SQLite file that isn't there.</summary>
    private DatabaseBackupService MissingSqliteService() => new(
        Configuration(("Database:Provider", "Sqlite"), ("ConnectionStrings:GrifballWebApp", MissingSource)),
        NullLoggerFactory.Instance, new FakeTimeProvider(Now));

    /// <summary>Every progress report, in order, as it's made (Progress&lt;T&gt; would post them later).</summary>
    private sealed class RecordingProgress : IProgress<ConversionProgress>
    {
        public List<ConversionProgress> Reports { get; } = [];
        public void Report(ConversionProgress value) => Reports.Add(value);
    }

    [Test]
    [Platform(Exclude = "Win")] // the fake pg_dump is a #!/bin/sh script
    public async Task MakeAsync_ADump_LeavesTheFileForTheCaller_ReportingTheStep()
    {
        var service = new DatabaseBackupService(
            Configuration(("Database:Provider", "Postgres"), ("ConnectionStrings:GrifballWebApp", "Host=db;Database=d"),
                ("DatabaseBackup:PgDump", PgDumpTestData.FakePgDump("printf PGDMP > \"$file\"", _directory))),
            NullLoggerFactory.Instance, new FakeTimeProvider(Now));
        var progress = new RecordingProgress();

        // Null only when another backup is running, which none is here
        var built = await service.MakeAsync(BackupFormat.Dump, CancellationToken.None, progress)
            ?? throw new InvalidOperationException("MakeAsync refused the backup as if another were running");

        try
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That((built.FileName, built.ContentType, built.Tables.Count), Is.EqualTo(("grifball-backup-20261113-080000.dump", "application/octet-stream", 0)));
                Assert.That(await File.ReadAllTextAsync(built.Path), Is.EqualTo("PGDMP"));
                Assert.That(progress.Reports, Is.EqualTo(new[] { new ConversionProgress(0, 1, "Making the .dump with pg_dump") }));
            }
        }
        finally
        {
            File.Delete(built.Path);
        }
    }

    [TestCase(BackupFormat.Sqlite)]
    [TestCase(BackupFormat.Postgres)]
    [TestCase(BackupFormat.SqlServer)]
    public void MakeAsync_OfASourceThatIsntThere_Throws_AndLetsTheNextOneRun(BackupFormat format)
    {
        var service = MissingSqliteService();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await service.MakeAsync(format, CancellationToken.None), Throws.TypeOf<ConversionException>().With.Message.EqualTo("The source database doesn't exist."));
            // Not refused as "another is running" (null): the failed one let the gate go
            Assert.That(async () => await service.MakeAsync(format, CancellationToken.None), Throws.TypeOf<ConversionException>());
        }
    }

    [Test]
    public void ExportAsync_OfASourceThatIsntThere_Throws_AndLetsTheNextOneRun()
    {
        var service = MissingSqliteService();
        var target = $"Data Source={Path.Combine(_directory, "copy.db")}";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await service.ExportAsync(DatabaseProvider.Sqlite, target, CancellationToken.None), Throws.TypeOf<ConversionException>());
            Assert.That(async () => await service.ExportAsync(DatabaseProvider.Sqlite, target, CancellationToken.None), Throws.TypeOf<ConversionException>());
            Assert.That(File.Exists(Path.Combine(_directory, "copy.db")), Is.False, "Refused before the target was made");
        }
    }

    [Test]
    public void ExportAsync_ToAConnectionStringTheProviderCantRead_IsRefused()
        => Assert.That(async () => await MissingSqliteService().ExportAsync(DatabaseProvider.SqlServer, "Nonsense Key=1", CancellationToken.None),
            Throws.InstanceOf<ArgumentException>());

    [Test]
    public void ExportAsync_ToNoConnectionString_IsRefused()
        => Assert.That(async () => await MissingSqliteService().ExportAsync(DatabaseProvider.Postgres, " ", CancellationToken.None),
            Throws.ArgumentException.With.Message.EqualTo("A connection string is required."));

    /// <summary>
    /// A .dump with the app on SQLite: made on the scratch PostgreSQL, started on demand. Its port refuses every
    /// connection (nothing listens on port 1), and with no time to wait for it the first refusal ends the backup; the
    /// scratch server is stopped again all the same.
    /// </summary>
    [Test]
    public async Task MakeAsync_OnAScratchServerThatNeverLetsItIn_StopsTheScratchServerAgain()
    {
        var scaler = Substitute.For<IScratchScaler>();
        var loggers = new LogRecorderFactory();
        var service = new DatabaseBackupService(
            Configuration(("Database:Provider", "Sqlite"), ("ConnectionStrings:GrifballWebApp", MissingSource),
                ("DatabaseBackup:Scratch:Provider", "Postgres"),
                ("DatabaseBackup:Scratch:ConnectionString", "Host=127.0.0.1;Port=1;Database=postgres;Username=admin;Password=x;Timeout=2")),
            loggers, new FakeTimeProvider(Now), scaler) { ScratchLoginWait = TimeSpan.Zero };
        var progress = new RecordingProgress();

        Assert.That(async () => await service.MakeAsync(BackupFormat.Dump, CancellationToken.None, progress), Throws.InstanceOf<DbException>());

        await scaler.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await scaler.Received(1).StopAsync(CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress.Reports.Select(r => r.Message), Is.EqualTo(ScratchStartReport));
            Assert.That(loggers.Logger.At(Microsoft.Extensions.Logging.LogLevel.Information), Is.EqualTo(ScratchStartReport), "and in the log");
        }
    }

    /// <summary>
    /// While a dump is being made, another backup or an export is refused (null). MakeAsync takes its gate before its
    /// first await, so the gate is held once the call has returned its task. The fake pg_dump runs until the test lets it
    /// go (a marker file), not for a fixed time.
    /// </summary>
    [Test]
    [Platform(Exclude = "Win")] // the fake pg_dump is a #!/bin/sh script
    public async Task MakeAndExport_WhileABackupIsBeingMade_AreRefused()
    {
        var release = Path.Combine(_directory, "release");
        var fake = PgDumpTestData.FakePgDump(
            $"printf '' > \"$file\"; i=0; while [ ! -e '{release}' ] && [ $i -lt 600 ]; do sleep 0.05; i=$((i+1)); done", _directory);
        var service = new DatabaseBackupService(
            Configuration(("Database:Provider", "Postgres"), ("ConnectionStrings:GrifballWebApp", "Host=db;Database=d"), ("DatabaseBackup:PgDump", fake)),
            NullLoggerFactory.Instance, new FakeTimeProvider(Now));
        var first = service.MakeAsync(BackupFormat.Dump, CancellationToken.None);

        var secondBackup = await service.MakeAsync(BackupFormat.Dump, CancellationToken.None);
        var export = await service.ExportAsync(DatabaseProvider.Sqlite, $"Data Source={Path.Combine(_directory, "copy.db")}", CancellationToken.None);
        await File.WriteAllTextAsync(release, "");
        var made = await first;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondBackup, Is.Null);
            Assert.That(export, Is.Null);
            Assert.That(made?.FileName, Is.EqualTo("grifball-backup-20261113-080000.dump"));
        }
        if (made is not null)
            File.Delete(made.Path);
    }

    [Test]
    public void ExportAsync_SaysWhereItsExporting_WhenInformationIsLogged()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger>();
        logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information).Returns(true);
        var loggers = Substitute.For<Microsoft.Extensions.Logging.ILoggerFactory>();
        loggers.CreateLogger(Arg.Any<string>()).Returns(logger);
        var service = new DatabaseBackupService(
            Configuration(("Database:Provider", "Sqlite"), ("ConnectionStrings:GrifballWebApp", MissingSource)), loggers, new FakeTimeProvider(Now));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await service.ExportAsync(DatabaseProvider.Sqlite, $"Data Source={Path.Combine(_directory, "copy.db")}", CancellationToken.None),
                Throws.TypeOf<ConversionException>());
            Assert.That(logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Log" && Equals(c.GetArguments()[0], Microsoft.Extensions.Logging.LogLevel.Information)
            && c.GetArguments()[2]?.ToString() == "Exporting the database to a new Sqlite database"), Is.EqualTo(1));
        }
    }
}
