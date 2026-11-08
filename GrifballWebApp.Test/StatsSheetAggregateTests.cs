using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The stats sheets are small aggregates (#335): a season's (<see cref="PlayerSeasonStat"/>) and a matchmaking season's
/// (<see cref="MatchmakingPlayerStat"/>) rows begin from their sheet, a re-run of the import restates them from the same
/// slot's row only, and a merge moves only the source's rows.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StatsSheetAggregateTests
{
    private static StatSheetTotals Sheet(string gamertag = "Shadow", int goals = 3) => new()
    {
        Gamertag = gamertag,
        GamesPlayed = 10,
        Wins = 7,
        Losses = 3,
        WinPercent = 0.7m,
        Goals = goals,
        Kills = 40,
        Deaths = 20,
        Assists = 5,
        KdRatio = 2m,
        KdSpread = 20,
        CarrierKills = 4,
        AverageLifeSeconds = 31.5m,
        TotalSecondsPlayed = 6000,
        Sprees = 2,
        MultiKills = 3,
        Exterminations = 1,
        Betrayals = 1,
        HammerKills = 30,
        SwordKills = 6,
        BallKills = 4,
        Punches = 2,
        Score = 900,
        DamageDealt = 5000,
        DamageTaken = 4000,
        ExtendedMetricsJson = """{"Assassinations":2}""",
    };

    [Test]
    public void ASeasonsRow_FromItsSheet_KeepsTheSheetsValues()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), teamID: 12, stintIndex: 2, isPlayoffs: true, isCombined: true, tradeNote: "From Hens", roleNote: "Sub");

        Assert.Multiple(() =>
        {
            Assert.That((row.SeasonID, row.UserID, row.GameVersionID, row.TeamID, row.StintIndex, row.IsPlayoffs, row.IsCombined, row.TradeNote, row.RoleNote),
                Is.EqualTo((4, 9, 6, (int?)12, 2, true, true, "From Hens", "Sub")));
            Assert.That(row.Totals, Is.EqualTo(Sheet()), "Every total, as the sheet gave it");
        });
    }

    [Test]
    public void ASeasonsRow_IsTheFirstStintOfTheRegularSeason_UnlessTheSheetSaysOtherwise()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet());

        Assert.That((row.TeamID, row.StintIndex, row.IsPlayoffs, row.IsCombined), Is.EqualTo(((int?)null, 1, false, false)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ASeasonsRow_RefusesAStintBelowOne(int stint)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), stintIndex: stint));
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void ARow_RefusesASheetWithoutAGamertag(string? gamertag)
    {
        Assert.Multiple(() =>
        {
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () => PlayerSeasonStat.FromSheet(4, 9, 6, Sheet() with { Gamertag = gamertag! }));
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () => MatchmakingPlayerStat.FromSheet(4, 9, 6, Sheet() with { Gamertag = gamertag! }));
            Assert.Throws<ArgumentNullException>(() => MatchmakingPlayerStat.FromSheet(4, 9, 6, null!));
        });
    }

    [Test]
    public void ASeasonsRow_IsRestated_ByTheSameStintsRow()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), teamID: 12, stintIndex: 2, isPlayoffs: true, tradeNote: "From Hens");
        var sheet = PlayerSeasonStat.FromSheet(4, 9, 7, Sheet(gamertag: "Shadow S16", goals: 8), teamID: null, stintIndex: 2, isPlayoffs: true, isCombined: true, roleNote: "Sub");

        Assert.That(row.IsSameStintAs(sheet), Is.True);
        row.Restate(sheet);

        Assert.Multiple(() =>
        {
            Assert.That((row.GameVersionID, row.TeamID, row.IsCombined, row.TradeNote, row.RoleNote), Is.EqualTo((7, (int?)null, true, (string?)null, "Sub")));
            Assert.That(row.Totals, Is.EqualTo(Sheet(gamertag: "Shadow S16", goals: 8)));
        });
    }

    [Test]
    public void ASeasonsRow_RefusesARestate_ByAnotherSlotsRow()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet());
        var others = new[]
        {
            PlayerSeasonStat.FromSheet(5, 9, 6, Sheet(goals: 8)),
            PlayerSeasonStat.FromSheet(4, 10, 6, Sheet(goals: 8)),
            PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(goals: 8), stintIndex: 2),
            PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(goals: 8), isPlayoffs: true),
        };

        Assert.Multiple(() =>
        {
            foreach (var other in others)
                Assert.Throws<InvalidOperationException>(() => row.Restate(other));
            Assert.Throws<InvalidOperationException>(() => row.Restate(null!));
            Assert.That(row.Goals, Is.EqualTo(3), "Nothing changed");
        });
    }

    [Test]
    public void AMatchmakingRow_IsRestated_ByTheSamePlayersRow_Only()
    {
        var row = MatchmakingPlayerStat.FromSheet(3, 9, 1, Sheet());

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => row.Restate(MatchmakingPlayerStat.FromSheet(4, 9, 1, Sheet(goals: 8))));
            Assert.Throws<InvalidOperationException>(() => row.Restate(MatchmakingPlayerStat.FromSheet(3, 10, 1, Sheet(goals: 8))));
            Assert.That(row.IsSameSlotAs(null!), Is.False);
        });
        row.Restate(MatchmakingPlayerStat.FromSheet(3, 9, 2, Sheet(goals: 8)));

        Assert.That((row.MatchmakingSeasonID, row.UserID, row.GameVersionID, row.Goals), Is.EqualTo((3, 9, 2, 8)));
    }

    [Test]
    public void AMerge_MovesOnlyTheSourcesRow()
    {
        var season = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet());
        var matchmaking = MatchmakingPlayerStat.FromSheet(3, 9, 1, Sheet());

        Assert.Multiple(() =>
        {
            Assert.That(season.ReassignUser(10, 11), Is.False, "Someone else's row");
            Assert.That(season.UserID, Is.EqualTo(9));
            Assert.That(season.ReassignUser(9, 11), Is.True);
            Assert.That(season.UserID, Is.EqualTo(11));
            Assert.That(matchmaking.ReassignUser(9, 11), Is.True);
            Assert.That(matchmaking.UserID, Is.EqualTo(11));
        });
    }

    [Test]
    public void AStintsTeam_CanGo_TheRowStays()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), teamID: 12);

        row.RemoveTeam();

        Assert.That((row.TeamID, row.Team, row.Goals), Is.EqualTo(((int?)null, (Team?)null, 3)));
    }

    [Test]
    public void ASeed_OfEntitiesNotSavedYet_KeepsTheNavigations()
    {
        var user = User.Seed(userName: "seeded");
        var late = MatchmakingSeason.Seed("Late League");
        var team = Team.Seed(teamName: "Reds", teamID: 5);

        var row = PlayerSeasonStat.Seed(Sheet(), gameVersionID: 1, user: user, team: team, stintIndex: 0);
        var lateRow = MatchmakingPlayerStat.Seed(Sheet(), gameVersionID: 1, matchmakingSeason: late, user: user);

        Assert.Multiple(() =>
        {
            Assert.That((row.User, row.Team, row.TeamID, row.StintIndex), Is.EqualTo((user, team, (int?)5, 0)), "A seed may be what an older model let be");
            Assert.That((lateRow.User, lateRow.MatchmakingSeason), Is.EqualTo((user, late)));
        });
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StatsSheetAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task TheDatabase_RefusesAStintBelowOne()
    {
        var season = await _context.AddSeason();
        var user = await _context.AddUser("stinted");
        // Past the factory, as only something outside the class could (raw SQL, a bad migration)
        await using var ctx = _context.NewContextLike();
        var entry = ctx.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "Stinted" }));
        entry.Property(s => s.StintIndex).CurrentValue = 0;

        await Assert.ThatAsync(() => ctx.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task ARestate_ByTheSameSheet_WritesNothing_AnotherSheet_OnlyWhatChanged()
    {
        var season = await _context.AddSeason();
        var user = await _context.AddUser("restated");
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "Restated", Goals = 3, WinPercent = 0.5m }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var stored = await _context.PlayerSeasonStats.SingleAsync();
        stored.Restate(PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "Restated", Goals = 3, WinPercent = 0.500m }));
        _context.ChangeTracker.DetectChanges();
        var same = _context.Entry(stored).State;
        stored.Restate(PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "Restated", Goals = 4, WinPercent = 0.5m }));
        _context.ChangeTracker.DetectChanges();
        var modified = _context.Entry(stored).Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        Assert.Multiple(async () =>
        {
            Assert.That(same, Is.EqualTo(EntityState.Unchanged), "The same sheet changes nothing");
            Assert.That(modified, Does.Contain(nameof(PlayerSeasonStat.Goals)).And.Not.Contain(nameof(PlayerSeasonStat.Gamertag)));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Goals).SingleAsync(), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task AStintsTeam_Going_LeavesTheSavedRow_WithNoTeam()
    {
        var season = await _context.AddSeason();
        var user = await _context.AddUser("teamless");
        var team = Team.Imported(season.SeasonID, "Gone");
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "Teamless" }, teamID: team.TeamID));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var row = await _context.PlayerSeasonStats.Include(s => s.Team).SingleAsync();
        row.RemoveTeam();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        Assert.That(await _context.PlayerSeasonStats.Select(s => s.TeamID).SingleAsync(), Is.Null);
    }

    [Test]
    public async Task AMatchmakingSeason_ReadsItsRows()
    {
        var user = await _context.AddUser("late");
        var late = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.MatchmakingSeasons.Add(late);
        await _context.SaveChangesAsync();
        _context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.FromSheet(late.MatchmakingSeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "Late", Kills = 4 }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var season = await _context.MatchmakingSeasons.Include(s => s.PlayerStats).SingleAsync();

        Assert.That(season.PlayerStats.Select(s => (s.UserID, s.Kills)), Is.EqualTo(new[] { (user.Id, 4) }));
    }
}

/// <summary>
/// The StatsSheetsAggregate migration settles the rows its constraint (CK_Event_PlayerSeasonStats_StintFromOne) would
/// refuse before adding it: a stint below 1 becomes the player's next stint of that season's phase, after their last
/// one, so none clashes with a row already there (the unique index). Alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StatsSheetAggregateMigrationTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task AStintBelowOne_IsSettled_BeforeTheConstraint()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_StatsSheetsAggregate"))) - 1]);

        var season = await _context.AddSeason();
        var traded = await _context.AddUser("traded");
        var alone = await _context.AddUser("alone");
        // As the old model's public setters could leave them
        var first = PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Traded" }, season.SeasonID, traded.Id, 1, stintIndex: 1);
        var zero = PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Traded" }, season.SeasonID, traded.Id, 1, stintIndex: 0);
        var negative = PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Traded" }, season.SeasonID, traded.Id, 1, stintIndex: -1);
        var playoffs = PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Traded" }, season.SeasonID, traded.Id, 1, stintIndex: 0, isPlayoffs: true);
        var lone = PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Alone" }, season.SeasonID, alone.Id, 1, stintIndex: 0);
        _context.PlayerSeasonStats.AddRange(first, zero, negative, playoffs, lone);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var stints = await _context.PlayerSeasonStats.AsNoTracking().ToDictionaryAsync(s => s.PlayerSeasonStatID, s => s.StintIndex);
        Assert.Multiple(() =>
        {
            Assert.That(stints[first.PlayerSeasonStatID], Is.EqualTo(1), "A stint from 1 stays");
            Assert.That(new[] { stints[zero.PlayerSeasonStatID], stints[negative.PlayerSeasonStatID] }, Is.Unique.And.All.GreaterThan(1), "After the player's last stint, each its own");
            Assert.That(stints[playoffs.PlayerSeasonStatID], Is.EqualTo(1), "The playoffs' stints are their own");
            Assert.That(stints[lone.PlayerSeasonStatID], Is.EqualTo(1), "The only stint is the first");
        });
    }
}
