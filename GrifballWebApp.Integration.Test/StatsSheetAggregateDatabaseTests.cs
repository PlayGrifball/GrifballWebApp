using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

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
