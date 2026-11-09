using System.Data.Common;
using DotNet.Testcontainers.Containers;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Test;

/// <summary>
/// A native backup of the kind of server the app doesn't run on, made on the scratch server (the chart's
/// backupScratch): here the suite's other server, PostgreSQL beside SQL Server or SQLite, SQL Server beside PostgreSQL.
/// The database is copied into a temporary database there, backed up, and the copy dropped, whatever happens.
/// </summary>
[TestFixture]
[NonParallelizable]
public class DatabaseBackupScratchTests
{
    private static IDatabaseContainer _other = null!;
    private GrifballContext _context = null!;
    private readonly List<string> _files = [];

    [OneTimeSetUp]
    public static async Task StartOtherServer() => _other = await TestDatabase.StartServer(TestDatabase.Other);

    [OneTimeTearDown]
    public static async Task StopOtherServer() => await _other.DisposeAsync();

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _context.Seasons.Add(Season.Seed(seasonName: "Backed up on the scratch server"));
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
        foreach (var file in _files)
            File.Delete(file);
    }

    private static bool ScratchIsPostgres => TestDatabase.Other == DatabaseProvider.Postgres;

    /// <summary>The scratch server's administrator connection, as the chart gives it (its maintenance database).</summary>
    private static string ScratchConnectionString => TestDatabase.ConnectionString(_other, ScratchIsPostgres ? "postgres" : "master", TestDatabase.Other);

    private DatabaseBackupService Service(string? pgDump = null, IScratchScaler? scaler = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GrifballWebApp"] = _context.Database.GetConnectionString(),
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["DatabaseBackup:Scratch:Provider"] = TestDatabase.Other.ToString(),
            ["DatabaseBackup:Scratch:ConnectionString"] = ScratchConnectionString,
            [NativeBackups.PgDumpKey] = pgDump,
        }).Build();
        return new DatabaseBackupService(configuration, NullLoggerFactory.Instance, TimeProvider.System, scaler);
    }

    /// <summary>
    /// A stand-in pg_dump (the runner's may be older than the scratch server): writes the archive's magic and the database
    /// it was given, or fails.
    /// </summary>
    private string PgDump(bool fail = false)
    {
        var path = NativeBackupsTests.FakePgDump(fail
            ? "echo 'pg_dump: error: connection refused' >&2; exit 1"
            : "printf 'PGDMP %s' \"$db\" > \"$file\"");
        _files.Add(path);
        return path;
    }

    /// <summary>The temporary copies the scratch server has: none once a backup is done.</summary>
    private static async Task<List<string>> CopiesAsync()
    {
        await using DbConnection connection = ScratchIsPostgres ? new NpgsqlConnection(ScratchConnectionString) : new SqlConnection(ScratchConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = ScratchIsPostgres
            ? "SELECT datname FROM pg_database WHERE datname LIKE 'grif\\_backup\\_%'"
            : "SELECT name FROM sys.databases WHERE name LIKE 'grif\\_backup\\_%' ESCAPE '\\'";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }

    [Test]
    public async Task Makes_the_other_kinds_native_backup_on_the_scratch_server_and_drops_the_copy()
    {
        var service = Service(PgDump());
        Assert.That(service.Formats, Does.Contain(ScratchIsPostgres ? BackupFormat.Dump : BackupFormat.Bak));

        var backup = await service.CreateAsync(ScratchIsPostgres ? BackupFormat.Dump : BackupFormat.Bak, CancellationToken.None);

        Assert.That(backup!.FileName, Does.EndWith(ScratchIsPostgres ? ".dump" : ".bak"));
        string text;
        await using (backup.File)
        using (var reader = new StreamReader(backup.File, System.Text.Encoding.Latin1))
            text = await reader.ReadToEndAsync();
        if (ScratchIsPostgres)
            // pg_dump was given the temporary copy, on the scratch server.
            Assert.That(text, Does.Match("^PGDMP grif_backup_[0-9a-f]{32}$"));
        else
            Assert.That(text[..4], Is.EqualTo(NativeBackupsTests.CompressedBakHeader));
        Assert.That(await CopiesAsync(), Is.Empty, "the copy is dropped");
    }

    [Test]
    public async Task Drops_the_copy_when_the_backup_fails()
    {
        if (!ScratchIsPostgres)
            Assert.Ignore("A failing pg_dump stands in for a backup that fails part way.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(PgDump(fail: true)).CreateAsync(BackupFormat.Dump, CancellationToken.None));

        Assert.That(await CopiesAsync(), Is.Empty);
    }

    [Test]
    public async Task Starts_a_scratch_server_kept_stopped_and_stops_it_again()
    {
        var service = Service(PgDump(), ScratchScalerTests.Recording(out var calls));

        var backup = await service.CreateAsync(ScratchIsPostgres ? BackupFormat.Dump : BackupFormat.Bak, CancellationToken.None);
        await backup!.File.DisposeAsync();

        Assert.That(calls, Is.EqualTo(new[] { "start", "stop" }));
        Assert.That(await CopiesAsync(), Is.Empty);
    }

    [Test]
    public async Task Stops_the_scratch_server_when_the_backup_fails()
    {
        if (!ScratchIsPostgres)
            Assert.Ignore("A failing pg_dump stands in for a backup that fails part way.");
        var service = Service(PgDump(fail: true), ScratchScalerTests.Recording(out var calls));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(BackupFormat.Dump, CancellationToken.None));

        Assert.That(calls, Is.EqualTo(new[] { "start", "stop" }));
    }
}
