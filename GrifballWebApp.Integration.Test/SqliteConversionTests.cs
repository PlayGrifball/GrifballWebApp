using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Domain.League;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The conversion from one SQLite file into another: SQLite on both sides, so these run on every leg of the suite.
/// The conversions to and from the other servers are DatabaseConversionTests', on the SQLite leg.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SqliteConversionTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), $"grif-sqlite-convert-{Environment.ProcessId}");
    private static readonly string[] expected = new[] { "Winter" };

    [OneTimeSetUp]
    public static void MakeFolder() => Directory.CreateDirectory(Folder);

    [OneTimeTearDown]
    public static void DeleteFolder()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(Folder, recursive: true);
    }

    private static DatabaseEndpoint File() => new(DatabaseProvider.Sqlite, $"Data Source={Path.Combine(Folder, $"{Guid.NewGuid():N}.db")}");

    private static string PathOf(DatabaseEndpoint endpoint) => new SqliteConnectionStringBuilder(endpoint.ConnectionString).DataSource;

    private static DatabaseConverter Converter() => new(NullLogger<DatabaseConverter>.Instance);

    private static async Task<DatabaseEndpoint> Source()
    {
        var source = File();
        await using var db = DatabaseConverter.NewContext(source);
        await db.Database.MigrateAsync();
        var season = Season.Seed(seasonName: "Winter").WithDates(publicAt: DateTime.UtcNow, signupsOpen: DateTime.UtcNow, signupsClose: DateTime.UtcNow, draftStart: DateTime.UtcNow, seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow);
        db.Seasons.Add(season);
        await db.SaveChangesAsync();
        season.Edited(seasonName: "Winter, renamed");
        await db.SaveChangesAsync();
        return source;
    }

    [Test]
    public async Task Converts_EveryRowAndItsHistory_IntoANewFile()
    {
        var source = await Source();
        var target = File();

        var tables = await Converter().ConvertAsync(source, target);

        await using var db = DatabaseConverter.NewContext(target);
        var history = await db.Database.SqlQueryRaw<string>("SELECT \"SeasonName\" AS \"Value\" FROM \"SeasonsHistory\"").ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tables.Single(t => t.Name == "Seasons").Rows, Is.EqualTo(1));
            Assert.That(await db.Seasons.Select(s => s.SeasonName).SingleAsync(), Is.EqualTo("Winter, renamed"));
            Assert.That(history, Is.EqualTo(expected), "the source's history, not the copy's own");
        }
    }

    // A table only the source has fails the copy once the target is migrated: a file the conversion made is deleted
    [Test]
    public async Task AFailedCopy_DeletesTheFileItMade()
    {
        var source = await Source();
        await using (var db = DatabaseConverter.NewContext(source))
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Extra\" (\"Id\" INTEGER PRIMARY KEY)");
        var target = File();

        var e = await Assert.ThrowsAsync<ConversionException>(() => Converter().ConvertAsync(source, target));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(e!.Message, Does.Contain("Extra is only in the source"));
            Assert.That(System.IO.File.Exists(PathOf(target)), Is.False);
        }
    }

    // The same with an empty file made beforehand: its migrations undone, so it has no tables and the next try takes it
    [Test]
    public async Task AFailedCopy_LeavesAnEmptyFileMadeBeforehandEmpty()
    {
        var source = await Source();
        await using (var db = DatabaseConverter.NewContext(source))
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Extra\" (\"Id\" INTEGER PRIMARY KEY)");
        var target = File();
        await using (var empty = new SqliteConnection(target.ConnectionString))
            await empty.OpenAsync();

        await Assert.ThrowsAsync<ConversionException>(() => Converter().ConvertAsync(source, target));

        await using var connection = new SqliteConnection(target.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite\\_%' ESCAPE '\\'";
        Assert.That(await command.ExecuteScalarAsync(), Is.Zero);
    }
}
