using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A season's result list in the historical import (GameResultsParser's games): recorded games without players, each side
/// the season's team of its name, in the series the list puts them in whenever they were played; a series of forfeits
/// is forfeited.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportResultListTests
{
    private const string Season = "fall-league-2020/Stats";

    private GrifballContext _context;
    private HistoricalImportService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _service = new HistoricalImportService(_context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }), NullLogger<HistoricalImportService>.Instance,
            new GamertagMatching(_context), TimeProvider.System);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static RecordedGameInput Result(string key, string series, string first, string second, Outcomes firstOutcome = Outcomes.Won, bool forfeit = false, bool playoffs = false,
        string? vod = null, string? host = null) => new()
    {
        SeasonSourceId = Season,
        SourceKey = key,
        GameVersion = "halo3",
        Series = series,
        IsForfeit = forfeit,
        IsPlayoffs = playoffs,
        Vod = vod,
        Host = host,
        Teams =
        [
            new RecordedTeamInput { Side = 1, Name = first, Outcome = firstOutcome, Score = forfeit ? null : firstOutcome == Outcomes.Won ? 5 : 2 },
            new RecordedTeamInput { Side = 2, Name = second, Outcome = firstOutcome == Outcomes.Won ? Outcomes.Lost : firstOutcome == Outcomes.Lost ? Outcomes.Won : firstOutcome, Score = forfeit ? null : firstOutcome == Outcomes.Won ? 2 : 5 },
        ],
    };

    private static HistoricalArchiveInput Fall2020(params RecordedGameInput[] games) => new()
    {
        Seasons = new UnifiedArchive
        {
            Seasons =
            [
                new ArchiveSeason
                {
                    SeasonName = "Fall League 2020",
                    GameVersion = "halo3",
                    League = "AGLA",
                    SourceWorkbook = "fall-league-2020",
                    SourceSheet = "Stats",
                    Players = [new ArchivePlayer { Gamertag = "Ace", TeamName = "Alpha", GamesPlayed = 1 }, new ArchivePlayer { Gamertag = "Bee", TeamName = "Bravo", GamesPlayed = 1 },
                        new ArchivePlayer { Gamertag = "Cee", TeamName = "Charlie", GamesPlayed = 1 }],
                },
            ],
        },
        RecordedGames = [.. games],
    };

    private sealed record SeriesRow(string? Home, string? Away, int? HomeScore, int? AwayScore, SeasonMatchResult? HomeResult, SeasonMatchResult? AwayResult, int Games, bool Bracket);

    private async Task<List<SeriesRow>> Series()
    {
        _context.ChangeTracker.Clear();
        return await _context.SeasonMatches.AsNoTracking().OrderBy(m => m.SeasonMatchID)
            .Select(m => new SeriesRow(m.HomeTeam!.TeamName, m.AwayTeam!.TeamName, m.HomeTeamScore, m.AwayTeamScore, m.HomeTeamResult, m.AwayTeamResult, m.SeriesGames.Count, m.BracketMatch != null))
            .ToListAsync();
    }

    [Test]
    public async Task AListsGames_AreItsSeries_BetweenTheTeamsItNames_WithTheirVodAndHost_AndAReRunChangesNothing()
    {
        var input = Fall2020(
            Result("r/1", "r/1", "Alpha", "Bravo", vod: "https://www.twitch.tv/videos/1", host: "Rage"),
            Result("r/2", "r/1", "Bravo", "Alpha"),
            Result("r/3", "r/3", "Alpha", "Bravo"),
            Result("r/4", "r/3", "Alpha", "Bravo"),
            Result("r/5", "r/5", "Charlie", "Alpha", playoffs: true));

        var report = await _service.Import(input);
        var again = await _service.Import(input);

        var series = await Series();
        var game = await _context.RecordedGames.AsNoTracking().SingleAsync(g => g.SourceKey == "r/1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(series, Is.EqualTo(new[]
            {
                new SeriesRow("Alpha", "Bravo", 1, 1, null, null, 2, false),
                new SeriesRow("Alpha", "Bravo", 2, 0, SeasonMatchResult.Won, SeasonMatchResult.Loss, 2, false),
                new SeriesRow("Charlie", "Alpha", 1, 0, SeasonMatchResult.Won, SeasonMatchResult.Loss, 1, true),
            }), "Two series of the same two teams with no dates stay two; the list's playoffs are the bracket's");
            Assert.That((game.Vod, game.Host, game.IsForfeit, game.PlayedAt), Is.EqualTo(("https://www.twitch.tv/videos/1", "Rage", false, (DateTime?)null)));
            Assert.That((again.SeasonMatchesCreated, again.RecordedGamesRemoved), Is.EqualTo((0, 0)), "A re-run finds the games and their series again");
        }
    }

    [Test]
    public async Task ASeriesOfForfeits_IsForfeited_ByTheSideThatWonNone_OrByBoth()
    {
        var report = await _service.Import(Fall2020(
            Result("f/1", "f/1", "Alpha", "Bravo", forfeit: true),
            Result("f/2", "f/1", "Alpha", "Bravo", forfeit: true),
            Result("f/3", "f/3", "Charlie", "Bravo", Outcomes.Lost, forfeit: true),
            Result("f/4", "f/4", "Alpha", "Charlie", Outcomes.Tie, forfeit: true),
            Result("f/5", "f/5", "Bravo", "Charlie"),
            Result("f/6", "f/5", "Bravo", "Charlie", forfeit: true)));

        var series = await Series();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(series.Select(s => (s.Home, s.Away, s.HomeResult, s.AwayResult)), Is.EqualTo(new[]
            {
                ("Alpha", "Bravo", (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Forfeit),
                ("Charlie", "Bravo", SeasonMatchResult.Forfeit, SeasonMatchResult.Won),
                ("Alpha", "Charlie", SeasonMatchResult.Forfeit, SeasonMatchResult.Forfeit),
                ("Bravo", "Charlie", SeasonMatchResult.Won, SeasonMatchResult.Loss),
            }), "A series with a game played is its games'");
            Assert.That(await _context.RecordedGames.CountAsync(g => g.IsForfeit), Is.EqualTo(5));
        }
    }
}
