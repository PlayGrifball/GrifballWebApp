using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>The MakeSeasonPublicAtOptional migration keeps the seasons there are public, on whichever database the suite runs on.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MakeSeasonPublicAtOptionalMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task SeasonsSavedBefore_StayPublic_FromWhenTheyWereMade()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var index = migrations.IndexOf(migrations.Single(m => m.EndsWith("_MakeSeasonPublicAtOptional")));
        await migrator.MigrateAsync(migrations[index - 1]);

        var created = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var announced = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        _context.Seasons.AddRange(
            new Season { SeasonName = "Never set", PublicAt = DateTime.MinValue, CreatedAt = created },
            new Season { SeasonName = "No creation time", PublicAt = DateTime.MinValue },
            new Season { SeasonName = "Set", PublicAt = announced, CreatedAt = created });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var seasons = await _context.Seasons.AsNoTracking().ToDictionaryAsync(s => s.SeasonName, s => s.PublicAt);
        Assert.Multiple(() =>
        {
            Assert.That(seasons["Never set"], Is.EqualTo(created));
            Assert.That(seasons["No creation time"], Is.EqualTo(DateTime.MinValue), "Still public");
            Assert.That(seasons["Set"], Is.EqualTo(announced));
        });
    }

    [Test]
    public async Task Down_FillsNullsInTheHistoryToo_SoPublicAtCanBeRequiredAgain()
    {
        // A season made not public, then made public: its old version, in the history table, has no PublicAt
        var season = new Season { SeasonName = "Was hidden", PublicAt = null };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        season.PublicAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _context.SaveChangesAsync();
        var hidden = new Season { SeasonName = "Still hidden", PublicAt = null };
        _context.Seasons.Add(hidden);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        Assert.That(await HistoryNulls(), Is.GreaterThan(0), "The history has a null to fill");

        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var index = migrations.IndexOf(migrations.Single(m => m.EndsWith("_MakeSeasonPublicAtOptional")));
        await migrator.MigrateAsync(migrations[index - 1]);

        var publicAt = await _context.Database
            .SqlQueryRaw<DateTime>(Sql(
                "SELECT [PublicAt] AS [Value] FROM [Event].[Seasons] WHERE [SeasonName] = 'Still hidden'",
                """SELECT "PublicAt" AS "Value" FROM "Event"."Seasons" WHERE "SeasonName" = 'Still hidden'"""))
            .SingleAsync();
        Assert.That(publicAt, Is.GreaterThan(new DateTime(9999, 1, 1)), "Not public: the end of time");
        if (TestDatabase.Provider == DatabaseProvider.SqlServer)
        {
            Assert.That(await HistoryNulls(), Is.Zero);
            var versioned = await _context.Database
                .SqlQueryRaw<int>("SELECT CAST(temporal_type AS int) AS [Value] FROM sys.tables WHERE name = 'Seasons' AND SCHEMA_NAME(schema_id) = 'Event'")
                .SingleAsync();
            Assert.That(versioned, Is.EqualTo(2), "System versioning is back on");
        }

        // And up again
        await migrator.MigrateAsync();
    }

    private static string Sql(string sqlServer, string postgres) => TestDatabase.Provider == DatabaseProvider.SqlServer ? sqlServer : postgres;

    private Task<int> HistoryNulls() => _context.Database
        .SqlQueryRaw<int>(Sql(
            "SELECT COUNT(*) AS [Value] FROM [Event].[SeasonsHistory] WHERE [PublicAt] IS NULL",
            """SELECT CAST(COUNT(*) AS int) AS "Value" FROM "Event"."SeasonsHistory" WHERE "PublicAt" IS NULL"""))
        .SingleAsync();
}
