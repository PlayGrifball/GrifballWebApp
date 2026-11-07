using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The MoveLateLeagueToMatchmaking migration takes away the Late League seasons an import made (the next import makes them
/// matchmaking seasons), keeping their recorded games, and keeps one made in the app as an AGLA league season, renamed when
/// AGLA has a season of its name; alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MoveLateLeagueToMatchmakingMigrationTests
{
    private const int LateLeagueID = 2;
    private const SeasonFormat LateLeagueFormat = (SeasonFormat)3;

    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static Season NewSeason(string name, int leagueId, SeasonFormat format, string? sourceId = null) => new Season()
    {
        SeasonName = name,
        LeagueID = leagueId,
        Format = format,
        HistoricalSourceID = sourceId,
    }.WithDates(signupsOpen: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), signupsClose: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), draftStart: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonStart: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));

    [Test]
    public async Task ImportedLateLeagueSeasons_Go_KeepingTheirGames_AndTheAppsOwn_AreKeptAsAGLASeasons()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_MoveLateLeagueToMatchmaking"))) - 1]);

        // Before the migration the Late League league and format exist, and the tables are as the model has them
        var imported = NewSeason("Late League Season 8 (Halo 3)", LateLeagueID, LateLeagueFormat, "late-league-aspence-main/S8Halo 3 Stats");
        var own = NewSeason("Late Night", LateLeagueID, LateLeagueFormat);
        var clash = NewSeason("Summer Cup", LateLeagueID, SeasonFormat.LeagueSeason);
        var agla = NewSeason("Summer Cup", League.DefaultLeagueID, SeasonFormat.LeagueSeason);
        _context.Seasons.AddRange(imported, own, clash, agla);
        await _context.SaveChangesAsync();
        var player = await _context.AddUser("player");
        var team = await _context.AddTeam(imported, "Alpha", player, 1);
        // Through the model as it was then: RecordedGame.Origin comes with a later migration
        await using (var before = new ContextBeforeOrigin(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).Options))
        {
            var game = new RecordedGame { SourceKey = "l/1", GameVersionID = 1, SeasonID = imported.SeasonID };
            game.Teams.Add(new RecordedGameTeam { Side = 1, TeamID = team.TeamID, Outcome = Outcomes.Won });
            before.RecordedGames.Add(game);
            await before.SaveChangesAsync();
        }
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { SeasonID = imported.SeasonID, UserID = player.Id, GameVersionID = 1, Gamertag = "Player", TeamID = team.TeamID });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var seasons = await _context.Seasons.AsNoTracking().OrderBy(s => s.SeasonName).Select(s => new { s.SeasonName, s.LeagueID, s.Format }).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(seasons.Select(s => (s.SeasonName, s.LeagueID, s.Format)), Is.EqualTo(new[]
            {
                ("Late Night", League.DefaultLeagueID, SeasonFormat.LeagueSeason),
                ("Summer Cup", League.DefaultLeagueID, SeasonFormat.LeagueSeason),
                ("Summer Cup (Late League)", League.DefaultLeagueID, SeasonFormat.LeagueSeason),
            }));
            Assert.That(await _context.Teams.AnyAsync(), Is.False, "The imported season's team went with it");
            Assert.That(await _context.PlayerSeasonStats.AnyAsync(), Is.False);
            Assert.That(await _context.RecordedGames.Select(g => new { g.SourceKey, g.SeasonID, Sides = g.Teams.Count(t => t.TeamID == null) }).SingleAsync(),
                Is.EqualTo(new { SourceKey = "l/1", SeasonID = (int?)null, Sides = 1 }), "Kept, in no season, for the next import");
            Assert.That(await _context.Leagues.AnyAsync(l => l.LeagueID == LateLeagueID), Is.False);
        });
    }

    /// <summary>The model before RecordedGame.Origin (AddRecordedGameOrigin), for a database migrated to before it.</summary>
    private sealed class ContextBeforeOrigin(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<RecordedGame>().Ignore(g => g.Origin);
        }
    }
}
