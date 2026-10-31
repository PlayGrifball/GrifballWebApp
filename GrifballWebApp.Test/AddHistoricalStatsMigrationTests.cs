using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The AddHistoricalStats migration gives the seasons there are the app's league (AGLA), makes season names unique per
/// league, and keeps historical seasons' archive keys unique where set, alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AddHistoricalStatsMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<(IMigrator Migrator, string Previous)> BeforeTheMigration()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var index = migrations.IndexOf(migrations.Single(m => m.EndsWith("_AddHistoricalStats")));
        await migrator.MigrateAsync(migrations[index - 1]);
        return (migrator, migrations[index - 1]);
    }

    [Test]
    public async Task SeasonsThereAre_GetTheAppsLeague_AndStayTheAppsOwn()
    {
        var (migrator, _) = await BeforeTheMigration();
        // Written in SQL: the model has the columns this migration adds
        await _context.Database.ExecuteSqlRawAsync(Sql(
            """
            INSERT INTO [Event].[Seasons] ([SeasonName], [CaptainsLocked], [SignupsOpen], [SignupsClose], [DraftStart], [SeasonStart], [SeasonEnd], [CreatedAt], [ModifiedAt])
            VALUES ('Season 1', 0, '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01'),
                   ('Season 2', 0, '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01', '2025-01-01')
            """,
            """
            INSERT INTO "Event"."Seasons" ("SeasonName", "CaptainsLocked", "SignupsOpen", "SignupsClose", "DraftStart", "SeasonStart", "SeasonEnd", "CreatedAt", "ModifiedAt")
            VALUES ('Season 1', false, '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z'),
                   ('Season 2', false, '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z', '2025-01-01Z')
            """));

        await migrator.MigrateAsync();

        var seasons = await _context.Seasons.AsNoTracking().Include(s => s.League).OrderBy(s => s.SeasonName).ToListAsync();
        var gameVersions = await _context.GameVersions.AsNoTracking().ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(seasons.Select(s => s.League.LeagueName), Is.EqualTo(new[] { "AGLA", "AGLA" }));
            Assert.That(seasons.Select(s => s.LeagueID), Is.All.EqualTo(League.DefaultLeagueID));
            Assert.That(seasons.Select(s => s.IsHistorical), Is.All.False);
            Assert.That(seasons.Select(s => s.HistoricalSourceID), Is.All.Null);
            Assert.That(gameVersions.Select(g => g.Slug), Is.EquivalentTo(new[] { "halo3", "reach", "reach-dash", "halo4", "halo5", "infinite" }));
        });
    }

    [Test]
    public async Task SeasonNames_AreUniquePerLeague()
    {
        var late = await _context.Leagues.SingleAsync(l => l.Slug == "late-league");
        _context.Seasons.AddRange(
            NewSeason("Winter League 2019", League.DefaultLeagueID),
            NewSeason("Winter League 2019", late.LeagueID));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _context.Seasons.Add(NewSeason("winter league 2019", late.LeagueID));
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync(), "Case-insensitively, on both databases");
    }

    [Test]
    public async Task HistoricalSourceID_IsUniqueWhereSet()
    {
        // Any number of the app's own seasons have none
        _context.Seasons.AddRange(NewSeason("Season 1"), NewSeason("Season 2"),
            NewSeason("Old 1", sourceId: "agla/old"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _context.Seasons.Add(NewSeason("Old 2", sourceId: "agla/old"));
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Test]
    public async Task SeasonNames_TakeA100CharacterName()
    {
        var name = new string('x', 100);
        _context.Seasons.Add(NewSeason(name));
        await _context.SaveChangesAsync();
        Assert.That(await _context.Seasons.AnyAsync(s => s.SeasonName == name));
    }

    [Test]
    public async Task UsersWithHistoricalRows_CannotBeDeleted()
    {
        var user = new User { UserName = "historical:Someone", IsDummyUser = true };
        var season = NewSeason("Old", sourceId: "agla/old");
        _context.AddRange(user, season);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = user.Id, SeasonID = season.SeasonID, GameVersionID = 1, Gamertag = "Someone" });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var deleting = Sql("DELETE FROM [Auth].[Users] WHERE [Id] = {0}", """DELETE FROM "Auth"."Users" WHERE "Id" = {0}""");
        Assert.That(async () => await _context.Database.ExecuteSqlRawAsync(deleting, user.Id), Throws.Exception, "The foreign key is restricted");
    }

    [Test]
    public async Task Down_RemovesHistoricalSeasonsAndPlaceholders_MakesNamesFit_AndUpAgain()
    {
        var user = new User { UserName = "historical:Someone", NormalizedUserName = "HISTORICAL:SOMEONE", IsDummyUser = true };
        var other = new User { UserName = "dummy", IsDummyUser = true };
        var historical = NewSeason("Summer League 2025 (Classic Legacy) with a long name", sourceId: "agla/x");
        var own = NewSeason("Season of the app's own");
        var longName = NewSeason("An app season with a name longer than thirty");
        var late = await _context.Leagues.SingleAsync(l => l.Slug == "late-league");
        var shared = NewSeason("Shared");
        var sharedElsewhere = NewSeason("Shared", late.LeagueID);
        _context.AddRange(user, other, historical, own, longName, shared, sharedElsewhere);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = user.Id, SeasonID = historical.SeasonID, GameVersionID = 6, Gamertag = "Someone" });
        await _context.SaveChangesAsync();
        // An old version in the history too
        historical.Division = "Legacy";
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var index = migrations.IndexOf(migrations.Single(m => m.EndsWith("_AddHistoricalStats")));
        await migrator.MigrateAsync(migrations[index - 1]);

        var names = await _context.Database
            .SqlQueryRaw<string>(Sql("SELECT [SeasonName] AS [Value] FROM [Event].[Seasons]", """SELECT "SeasonName" AS "Value" FROM "Event"."Seasons" """))
            .ToListAsync();
        var users = await _context.Database
            .SqlQueryRaw<string>(Sql("SELECT [UserName] AS [Value] FROM [Auth].[Users]", """SELECT "UserName" AS "Value" FROM "Auth"."Users" """))
            .ToListAsync();
        Assert.Multiple(() =>
        {
            var id = longName.SeasonID.ToString();
            Assert.That(names, Is.EquivalentTo(new[] { "Season of the app's own", $"{longName.SeasonName[..(29 - id.Length)]}~{id}", "Shared", $"Shared~{sharedElsewhere.SeasonID}" }));
            Assert.That(names.Select(n => n.Length), Is.All.LessThanOrEqualTo(30));
            Assert.That(users, Has.Member("dummy").And.No.Member("historical:Someone"), "The import's placeholders go; other dummy users stay");
        });

        await migrator.MigrateAsync();
        Assert.That(await _context.Seasons.Select(s => s.LeagueID).Distinct().SingleAsync(), Is.EqualTo(League.DefaultLeagueID), "Up gives them the app's league again");
    }

    [Test]
    public async Task Up_RecordsTheRenamesTheXboxUsersHistoryHolds()
    {
        var (migrator, _) = await BeforeTheMigration();
        // Written in SQL: the model has columns this migration adds. Each statement its own transaction, so each rename
        // leaves a version in the history.
        await _context.Database.ExecuteSqlRawAsync(Sql(
            "INSERT INTO [Xbox].[XboxUsers] ([XboxUserID], [Gamertag], [CreatedAt], [ModifiedAt]) VALUES (1, N'First', '2025-01-01', '2025-01-01'), (2, N'Never Renamed', '2025-01-01', '2025-01-01')",
            """INSERT INTO "Xbox"."XboxUsers" ("XboxUserID", "Gamertag", "CreatedAt", "ModifiedAt") VALUES (1, 'First', '2025-01-01Z', '2025-01-01Z'), (2, 'Never Renamed', '2025-01-01Z', '2025-01-01Z')"""));
        // The last one only changes case and spacing: no rename of "Third", which isn't a past gamertag then
        foreach (var name in new[] { "Second", "Third", " THIRD  " })
            await _context.Database.ExecuteSqlRawAsync(Sql(
                "UPDATE [Xbox].[XboxUsers] SET [Gamertag] = {0} WHERE [XboxUserID] = 1",
                """UPDATE "Xbox"."XboxUsers" SET "Gamertag" = {0} WHERE "XboxUserID" = 1"""), name);

        await migrator.MigrateAsync();

        var history = await _context.XboxGamertagHistory.AsNoTracking().OrderBy(h => h.Gamertag).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(history.Select(h => (h.XboxUserID, h.Gamertag, h.NormalizedGamertag, h.Source)), Is.EqualTo(new[]
            {
                (1L, "First", "FIRST", GamertagHistorySource.RenameDetected),
                (1L, "Second", "SECOND", GamertagHistorySource.RenameDetected),
            }), "Not the current gamertag, and nothing for an account never renamed");
            Assert.That(history.Select(h => h.LastSeen), Is.All.Not.Null);
            Assert.That(history.All(h => h.FirstSeen is null || h.FirstSeen <= h.LastSeen));
        });
    }

    private static Season NewSeason(string name, int leagueId = League.DefaultLeagueID, string? sourceId = null) => new()
    {
        SeasonName = name,
        LeagueID = leagueId,
        HistoricalSourceID = sourceId,
        IsHistorical = sourceId is not null,
        SignupsOpen = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        SignupsClose = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DraftStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        SeasonStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        SeasonEnd = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static string Sql(string sqlServer, string postgres) => TestDatabase.Provider == DatabaseProvider.SqlServer ? sqlServer : postgres;
}
