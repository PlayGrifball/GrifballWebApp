using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The conversion and the export script with their migrations faked (TestDoubles/FakeMigrations): DatabaseConverter
/// .ConvertAsync refusing a source whose migrations aren't this build's or a target with tables, migrating and filling a
/// new target, and putting a target back as it was when a step fails (dropped when it made it, its migrations undone
/// when it was there); DatabaseScriptExporter.WriteAsync on SQL Server and PostgreSQL; and the Database Backup page's
/// SQLite file and script built through them. Between temp-file SQLite databases: no server.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ConversionMigrationsTests : SqliteContextFixture
{
    private const string Init = "20260101000000_Init";
    private static readonly string[] Migrated = ["latest"];
    private static readonly string[] MigratedThenUndone = ["latest", "0"];
    private static readonly byte[] Utf8ByteOrderMark = [0xEF, 0xBB, 0xBF];
    private readonly FakeLogger<DatabaseConverter> _log = new();
    private MigrationsState _source = new() { Known = [Init], Applied = [Init] };
    private MigrationsState _target = new() { Known = [Init] };
    private string _targetPath = null!;

    [SetUp]
    public void SetUpTarget() => _targetPath = Path.Combine(Path.GetTempPath(), $"grif-infrastructure-convert-{Guid.NewGuid():N}.db");

    [TearDown]
    public void TearDownTarget() => SqliteFiles.Delete(_targetPath);

    private DatabaseEndpoint Source => new(DatabaseProvider.Sqlite, ConnectionString);
    private DatabaseEndpoint Target => new(DatabaseProvider.Sqlite, $"Data Source={_targetPath};Pooling=False");

    /// <summary>The source's context with the source's migrations, any other database's with the target's.</summary>
    private GrifballContext Contexts(DatabaseEndpoint endpoint) => new FakeMigrationsContext(
        FakeMigrationsContext.Options(endpoint.Provider, endpoint.ConnectionString), endpoint.ConnectionString == ConnectionString ? _source : _target);

    private DatabaseConverter Converter() => new(_log) { Contexts = Contexts };

    private async Task SeedUsersAsync()
    {
        Context.Users.AddRange(User.Seed(userName: "one"), User.Seed(userName: "two"), User.Seed(userName: "three"));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    /// <summary>A target that's there already, empty: a database made beforehand.</summary>
    private async Task EmptyTargetAsync()
    {
        await using var connection = new SqliteConnection(Target.ConnectionString);
        await connection.OpenAsync();
    }

    private async Task<int> TargetUsersAsync()
    {
        await using var target = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, Target.ConnectionString).Options);
        return await target.Users.CountAsync();
    }

    [Test]
    public async Task Convert_ANewTarget_IsMigrated_ThenFilled()
    {
        await SeedUsersAsync();

        var tables = await Converter().ConvertAsync(Source, Target, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_target.MigratedTo, Is.EqualTo(Migrated));
            Assert.That(tables.Single(t => t.Name == "Users").Rows, Is.EqualTo(3));
            Assert.That(await TargetUsersAsync(), Is.EqualTo(3));
            Assert.That(_log.At(LogLevel.Information), Does.Contain("Applying the migrations to the target (1)"));
        }
    }

    [Test]
    public void Convert_ASourceMissingAMigration_IsRefused_BeforeTheTargetIsTouched()
    {
        _source = new MigrationsState { Known = [Init, "20260201000000_Next"], Applied = [Init] };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await Converter().ConvertAsync(Source, Target, CancellationToken.None),
                Throws.TypeOf<ConversionException>().With.Message.EqualTo("The source lacks migrations of this build (20260201000000_Next): migrate it first, or convert it with its own build."));
            Assert.That(File.Exists(_targetPath), Is.False);
            Assert.That(_target.MigratedTo, Is.Empty);
        }
    }

    [Test]
    public void Convert_ASourceWithAMigrationThisBuildHasnt_IsRefused()
    {
        _source = new MigrationsState { Known = [Init], Applied = [Init, "20990101000000_Future"] };

        Assert.That(async () => await Converter().ConvertAsync(Source, Target, CancellationToken.None),
            Throws.TypeOf<ConversionException>().With.Message.EqualTo("The source has migrations this build doesn't (20990101000000_Future): convert it with the build that made them."));
    }

    [Test]
    public async Task Convert_ATargetWithTables_IsRefused_AndLeftAlone()
    {
        await using (var target = Contexts(Target))
            await target.Database.EnsureCreatedAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await Converter().ConvertAsync(Source, Target, CancellationToken.None),
                Throws.TypeOf<ConversionException>().With.Message.StartsWith("The target database already has tables"));
            Assert.That(_target.MigratedTo, Is.Empty);
            Assert.That(File.Exists(_targetPath), Is.True, "not dropped: it wasn't the conversion's");
        }
    }

    [Test]
    public void Convert_ANewTargetWhoseMigrationFails_IsDropped()
    {
        _target = new MigrationsState { Known = [Init], FailMigrate = true };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await Converter().ConvertAsync(Source, Target, CancellationToken.None), Throws.InvalidOperationException.With.Message.EqualTo("a migration failed"));
            Assert.That(File.Exists(_targetPath), Is.False);
        }
    }

    [Test]
    public async Task Convert_AnEmptyTargetWhoseMigrationFails_HasItsMigrationsUndone()
    {
        await EmptyTargetAsync();
        _target = new MigrationsState { Known = [Init], FailMigrate = true };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await Converter().ConvertAsync(Source, Target, CancellationToken.None), Throws.InvalidOperationException);
            Assert.That(_target.MigratedTo, Is.EqualTo(MigratedThenUndone), "migrated, then back to before the first");
            Assert.That(_log.At(LogLevel.Information), Does.Contain("Undid the target's migrations: it has no tables again"));
        }
    }

    [Test]
    public async Task Convert_ACopyThatFails_OnATargetItMade_DropsIt()
    {
        await SeedUsersAsync();
        _target = new MigrationsState { Known = [Init], DropAfterMigrate = "Teams" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await Converter().ConvertAsync(Source, Target, CancellationToken.None), Throws.TypeOf<ConversionException>());
            Assert.That(File.Exists(_targetPath), Is.False);
            Assert.That(_log.At(LogLevel.Error).Single(), Does.StartWith("The conversion failed (").And.EndWith("dropping the target database it created"));
        }
    }

    [Test]
    public async Task Convert_ACopyThatFails_OnATargetThatWasThere_UndoesItsMigrations_SayingSoWhenThatFails()
    {
        await EmptyTargetAsync();
        _target = new MigrationsState { Known = [Init], DropAfterMigrate = "Teams", FailUndo = true };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await Converter().ConvertAsync(Source, Target, CancellationToken.None), Throws.TypeOf<ConversionException>());
            Assert.That(_target.MigratedTo, Is.EqualTo(MigratedThenUndone));
            Assert.That(_log.At(LogLevel.Error), Has.Some.StartsWith("The conversion failed (").And.Some.StartsWith("Undoing the target's migrations failed"));
        }
    }

    // ---- DatabaseScriptExporter.WriteAsync ----

    [TestCase(DatabaseProvider.SqlServer, "INSERT INTO [Auth].[Users] (")]
    [TestCase(DatabaseProvider.Postgres, "INSERT INTO \"Auth\".\"Users\" (")]
    public async Task Script_HasTheTargetsMigrations_ThenTheSourcesRows(DatabaseProvider target, string usersInsert)
    {
        await SeedUsersAsync();
        _target = new MigrationsState { Known = [Init], Script = "CREATE TABLE \"Marker\" (\"Id\" int);" };
        await using var output = new StringWriter();
        var steps = new List<ConversionProgress>();

        var tables = await new DatabaseScriptExporter(_log) { Contexts = Contexts }
            .WriteAsync(Source, target, output, new SynchronousProgress(steps.Add), CancellationToken.None);

        var script = output.ToString();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(script, Does.Contain($"Migrations up to {Init}."));
            Assert.That(script, Does.Contain("CREATE TABLE \"Marker\" (\"Id\" int);"));
            Assert.That(script.Split('\n').Count(l => l.StartsWith(usersInsert, StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(tables.Single(t => t.Name == "Users").Rows, Is.EqualTo(3));
            Assert.That(steps.Select(s => s.Total), Is.All.EqualTo(tables.Count));
            Assert.That(_log.At(LogLevel.Information)[^1], Is.EqualTo($"Scripted {tables.Count} tables, {tables.Sum(t => t.Rows)} rows, for {target}"));
        }
    }

    [Test]
    public void Script_OfASourceMissingAMigration_IsRefused_WritingNothing()
    {
        _source = new MigrationsState { Known = [Init, "20260201000000_Next"], Applied = [Init] };
        using var output = new StringWriter();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await new DatabaseScriptExporter(_log) { Contexts = Contexts }.WriteAsync(Source, DatabaseProvider.Postgres, output),
                Throws.TypeOf<ConversionException>());
            Assert.That(output.ToString(), Is.Empty);
        }
    }

    // ---- The Database Backup page's downloads, through them ----

    private DatabaseBackupService Backups() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:GrifballWebApp"] = ConnectionString,
        }).Build(),
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, new FakeTimeProvider(new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero)))
    {
        Contexts = Contexts,
    };

    [Test]
    public async Task Backup_AsASqliteFile_IsTheCopy_InOneFile()
    {
        await SeedUsersAsync();

        var built = await Backups().MakeAsync(BackupFormat.Sqlite, CancellationToken.None)
            ?? throw new InvalidOperationException("refused as if another backup were running");

        try
        {
            await using var copy = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
                .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={built.Path};Pooling=False").Options);
            using (Assert.EnterMultipleScope())
            {
                Assert.That((built.FileName, built.ContentType), Is.EqualTo(("grifball-backup-20261113-080000.sqlite", "application/vnd.sqlite3")));
                Assert.That(built.Tables.Single(t => t.Name == "Users").Rows, Is.EqualTo(3));
                Assert.That(File.Exists(built.Path + "-wal"), Is.False, "everything in the one file that's downloaded");
                Assert.That(await copy.Users.CountAsync(), Is.EqualTo(3));
            }
        }
        finally
        {
            SqliteFiles.Delete(built.Path);
        }
    }

    [Test]
    public async Task Backup_AsASqlServerScript_IsTheScript_WithItsByteOrderMark()
    {
        await SeedUsersAsync();

        var built = await Backups().MakeAsync(BackupFormat.SqlServer, CancellationToken.None)
            ?? throw new InvalidOperationException("refused as if another backup were running");

        try
        {
            var bytes = await File.ReadAllBytesAsync(built.Path);
            using (Assert.EnterMultipleScope())
            {
                Assert.That((built.FileName, built.ContentType), Is.EqualTo(("grifball-backup-20261113-080000.sqlserver.sql", "application/sql")));
                Assert.That(bytes.Take(3), Is.EqualTo(Utf8ByteOrderMark), "sqlcmd reads it as UTF-8");
                Assert.That(System.Text.Encoding.UTF8.GetString(bytes), Does.Contain("INSERT INTO [Auth].[Users] ("));
            }
        }
        finally
        {
            File.Delete(built.Path);
        }
    }

    /// <summary>Hears each report as it's made (Progress&lt;T&gt; would post them later).</summary>
    private sealed class SynchronousProgress(Action<ConversionProgress> report) : IProgress<ConversionProgress>
    {
        public void Report(ConversionProgress value) => report(value);
    }
}
