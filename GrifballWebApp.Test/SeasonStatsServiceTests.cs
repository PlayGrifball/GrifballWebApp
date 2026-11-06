using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Leaderboards;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.TeamPage;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>
/// Season stats come from one place, the season's games where it has them, its stored totals otherwise: a season run
/// in the app has stats on every page with no import involved, and no player's phase of a season counts twice.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonStatsServiceTests
{
    private GrifballContext _context;
    private SeasonStatsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SeasonStatsService(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>A season run in the app: its match reported through SeasonMatchService, as the match page does.</summary>
    private async Task<SeasonMatchTestData.Seeded> PlayALiveSeason()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddMinutes(-20),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();
        var bracketService = new BracketService(_context, new TeamStandingsService(_context), new GrifballWebApp.Server.Championships.ChampionshipService(_context));
        await new SeasonMatchService(_context, Substitute.For<IDataPullService>(), bracketService, new GrifballWebApp.Server.Championships.ChampionshipService(_context)).ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);
        _context.ChangeTracker.Clear();
        return seeded;
    }

    [Test]
    public async Task ALiveSeason_HasStatsOnItsPages_WithNoImportInvolved()
    {
        var seeded = await PlayALiveSeason();
        var seasons = new SeasonService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context));

        var stats = await seasons.GetSeasonPlayerStats(seeded.Season.SeasonID);
        var summary = (await seasons.GetSeasonSummary(seeded.Season.SeasonID))!;
        var teamStats = await seasons.GetSeasonPlayerStats(seeded.Season.SeasonID, seeded.Home.TeamID);
        var boards = await new LeaderboardsController(new GrifballWebApp.Server.Leaderboards.LeaderboardsService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context), new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))).GetLeaderboards(null, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.PlayerSeasonStats.AnyAsync(), Is.False, "Nothing stored: the games are the stats");
            Assert.That(stats, Has.Count.EqualTo(8));
            Assert.That(stats.Select(s => s.Source), Is.All.EqualTo("Matches"));
            var top = stats[0];
            Assert.That((top.Gamertag, top.Goals, top.GamesPlayed, top.Wins, top.TeamID, top.TeamName), Is.EqualTo(("GT104", 4, 1, 1, (int?)seeded.Home.TeamID, "Home Team")));
            Assert.That(stats.Where(s => s.TeamID == seeded.Away.TeamID).Select(s => (s.Wins, s.Losses)), Is.All.EqualTo((0, 1)));
            Assert.That(summary.PlayerStatCount, Is.EqualTo(8));
            Assert.That(teamStats.Select(s => s.Gamertag), Is.EquivalentTo(new[] { "GT101", "GT102", "GT103", "GT104" }));
            Assert.That(boards.GoalLeaders.Select(g => g.Gamertag).First(), Is.EqualTo("GT104"));
            Assert.That(boards.KillLeaders, Has.Count.EqualTo(8));
        });
    }

    [Test]
    public async Task APlayersPhaseWithGames_IsNeverAlsoCountedFromStoredTotals()
    {
        var seeded = await PlayALiveSeason();
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 101);
        _context.PlayerSeasonStats.AddRange(
            // The same phase as the games: not counted
            new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 6, Gamertag = "Old", Goals = 99, GamesPlayed = 9 },
            // The playoffs, which have no games: counted
            new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 6, Gamertag = "Old", Goals = 7, GamesPlayed = 2, IsPlayoffs = true });
        await _context.SaveChangesAsync();

        var lines = (await _service.GetAsync(userID: user.Id)).Select(l => (l.IsPlayoffs, l.FromMatches, l.Goals, l.GamesPlayed));

        Assert.That(lines, Is.EquivalentTo(new[] { (false, true, 1, 1), (true, false, 7, 2) }));
    }

    [Test]
    public async Task ACombinedRow_CoversBothPhases_SoGamesInEitherReplaceIt()
    {
        var seeded = await PlayALiveSeason();
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 101);
        var other = await _context.Users.SingleAsync(u => u.XboxUserID == 102);
        var old = await _context.AddSeason("Old Season");
        _context.PlayerSeasonStats.AddRange(
            new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 6, Gamertag = "x", Goals = 50, IsCombined = true, IsPlayoffs = true },
            new PlayerSeasonStat { UserID = other.Id, SeasonID = old.SeasonID, GameVersionID = 6, Gamertag = "y", Goals = 9, IsCombined = true });
        await _context.SaveChangesAsync();

        var mine = await _service.GetAsync(userID: user.Id);
        var theirs = await _service.GetAsync(userID: other.Id);

        Assert.That(mine.Select(l => (l.FromMatches, l.Goals)), Is.EqualTo(new[] { (true, 1) }), "The regular season's games replace the combined row");
        Assert.That(theirs.Single(l => !l.FromMatches).Goals, Is.EqualTo(9), "Without games it counts");
    }

    [Test]
    public async Task APlayerWithoutAnAccount_IsLeftOut()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        // 301 is an Xbox user nobody's account is linked to
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, [101, 301], Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.SeriesGames.Add(new SeriesGame { MatchID = match.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 });
        await _context.SaveChangesAsync();

        var lines = await _service.GetAsync(seasonID: seeded.Season.SeasonID);

        Assert.That(lines.Select(l => l.Gamertag), Is.EquivalentTo(new[] { "GT101", "GT201", "GT202", "GT203", "GT204" }));
    }

    /// <summary>A recorded (MCC) game of the season: side 1 the home team's players, side 2 the away team's.</summary>
    private async Task<RecordedGame> AddRecordedGame(SeasonMatchTestData.Seeded seeded, string key, Outcomes homeOutcome, int? seasonMatchID = null, int gameNumber = 1, bool playoffs = false)
    {
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value);
        var game = new RecordedGame { SourceKey = key, GameVersionID = 1, SeasonID = seeded.Season.SeasonID, IsPlayoffs = playoffs, PlayedAt = DateTime.UtcNow };
        var home = new RecordedGameTeam { Side = 1, TeamID = seeded.Home.TeamID, Outcome = homeOutcome };
        var away = new RecordedGameTeam { Side = 2, TeamID = seeded.Away.TeamID, Outcome = homeOutcome == Outcomes.Won ? Outcomes.Lost : Outcomes.Won };
        game.Teams.Add(home);
        game.Teams.Add(away);
        foreach (var (xuids, side) in new[] { (SeasonMatchTestData.HomeXbox, home), (SeasonMatchTestData.AwayXbox, away) })
            foreach (var xuid in xuids)
                game.Participants.Add(new RecordedGameParticipant { UserID = users[xuid].Id, Gamertag = $"MCC{xuid}", RecordedGameTeam = side, Kills = 5, Deaths = 2, Assists = 1, Goals = 1, CarrierKills = 2, HammerKills = 3 });
        _context.RecordedGames.Add(game);
        if (seasonMatchID is not null)
            _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seasonMatchID.Value, RecordedGame = game, GameNumber = gameNumber });
        await _context.SaveChangesAsync();
        return game;
    }

    [Test]
    public async Task GoalsAndScoresPastAnInt_AreTotalledNotOverflowed()
    {
        // Custom modes keep their own counts in the score, in the hundreds of millions per game
        const int huge = 2_000_000_000;
        var seeded = await PlayALiveSeason();
        var series = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = seeded.Away.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(series);
        await _context.SaveChangesAsync();
        var recorded = await AddRecordedGame(seeded, "mcc/1", Outcomes.Won, series.SeasonMatchID);
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 101);
        foreach (var p in await _context.MatchParticipants.Where(p => p.XboxUserID == 101).ToListAsync())
            (p.Score, p.PersonalScore, p.Goals) = (huge, huge, huge);
        // Decoded (#217): the goals are the decoded ones, and a Plain game's PersonalScore is its points
        foreach (var m in await _context.Matches.ToListAsync())
            m.ScoreLayout = CustomScoreLayout.Plain;
        foreach (var p in await _context.RecordedGameParticipants.Where(p => p.RecordedGameID == recorded.RecordedGameID && p.UserID == user.Id).ToListAsync())
            (p.Goals, p.Score) = (huge, huge);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var line = (await _service.GetAsync(userID: user.Id)).Single();
        var boards = await new GrifballWebApp.Server.Leaderboards.LeaderboardsService(_context, _service, new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())).GetAsync(null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That((line.Goals, line.Score), Is.EqualTo((2L * huge, (long?)(2L * huge))));
            Assert.That(boards.GoalLeaders.First().StatValue, Is.EqualTo(2L * huge));
        });
    }

    [Test]
    public async Task PlayerGameStats_AreInfiniteAndRecordedGamesTogether()
    {
        var seeded = await PlayALiveSeason();
        var series = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = seeded.Away.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(series);
        await _context.SaveChangesAsync();
        await AddRecordedGame(seeded, "mcc/1", Outcomes.Won, series.SeasonMatchID);
        // A recorded game of no season and no series: counts nowhere
        _context.RecordedGames.Add(new RecordedGame { SourceKey = "loose", GameVersionID = 1 });
        await _context.SaveChangesAsync();
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 101);

        var games = await PlayerGameStatsQuery.Query(_context).Where(g => g.UserID == user.Id).OrderBy(g => g.Source).ToListAsync();
        var line = (await _service.GetAsync(userID: user.Id)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(games.Select(g => (g.Source, g.SeasonID, g.TeamID, g.Won, g.GameVersionID, g.MatchID.HasValue, g.RecordedGameID.HasValue, g.CarrierKills)), Is.EqualTo(new[]
            {
                (GameSource.Infinite, seeded.Season.SeasonID, (int?)seeded.Home.TeamID, true, PlayerGameStatsQuery.InfiniteGameVersionID, true, false, (int?)null),
                (GameSource.Recorded, seeded.Season.SeasonID, (int?)seeded.Home.TeamID, true, 1, false, true, (int?)2),
            }));
            Assert.That(games.Select(g => g.SeasonMatchID), Is.EqualTo(new int?[] { seeded.SeasonMatch.SeasonMatchID, series.SeasonMatchID }));
            Assert.That((line.GamesPlayed, line.Wins, line.Kills, line.CarrierKills, line.HammerKills, line.FromMatches), Is.EqualTo((2, 2, 1 + 5, 2, (int?)3, true)),
                "One line from both games (Infinite records no carrier stops or hammer kills, so they are the recorded game's)");
        });
    }

    [Test]
    public async Task ARecordedGame_InNoSeries_CountsForItsSeason()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        await AddRecordedGame(seeded, "mcc/scrim", Outcomes.Lost);
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 201);

        var lines = await _service.GetAsync(seasonID: seeded.Season.SeasonID);
        var mine = lines.Single(l => l.UserID == user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Count.EqualTo(8));
            Assert.That((mine.Gamertag, mine.GamesPlayed, mine.Wins, mine.Losses, mine.Goals, mine.TeamID, mine.TeamName, mine.GameVersionName),
                Is.EqualTo(("MCC201", 1, 1, 0, 1, (int?)seeded.Away.TeamID, (string?)"Away Team", (string?)"Halo 3")));
        });
    }

    [Test]
    public async Task RecordedGames_WinOverStoredTotals_ForThatPhase_AndACombinedRow()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        await AddRecordedGame(seeded, "mcc/regular", Outcomes.Won);
        var user = await _context.Users.SingleAsync(u => u.XboxUserID == 101);
        var other = await _context.Users.SingleAsync(u => u.XboxUserID == 102);
        _context.PlayerSeasonStats.AddRange(
            new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 1, Gamertag = "Sheet", Goals = 40, GamesPlayed = 10 },
            new PlayerSeasonStat { UserID = user.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 1, Gamertag = "Sheet", Goals = 4, GamesPlayed = 2, IsPlayoffs = true },
            new PlayerSeasonStat { UserID = other.Id, SeasonID = seeded.Season.SeasonID, GameVersionID = 1, Gamertag = "Sheet", Goals = 50, GamesPlayed = 12, IsCombined = true, IsPlayoffs = true });
        await _context.SaveChangesAsync();

        var mine = (await _service.GetAsync(userID: user.Id)).Select(l => (l.IsPlayoffs, l.FromMatches, l.Goals, l.GamesPlayed));
        var theirs = (await _service.GetAsync(userID: other.Id)).Select(l => (l.FromMatches, l.Goals));

        Assert.Multiple(() =>
        {
            Assert.That(mine, Is.EquivalentTo(new[] { (false, true, 1, 1), (true, false, 4, 2) }), "The regular season's game replaces the sheet's; the playoffs keep theirs");
            Assert.That(theirs, Is.EqualTo(new[] { (true, 1) }), "A combined row is replaced by games in either phase");
        });
    }
}
