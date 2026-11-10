using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Test;

/// <summary>
/// Recorded playoff games whose sides aren't all the season's teams: each series is a bracket slot all the same, a side
/// of no team left empty, and a team's later series follow its earlier ones. A game with one side links that side alone.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportPlayoffSlotsTests
{
    private static readonly DateTime T = new(2015, 6, 1, 20, 0, 0, DateTimeKind.Utc);
    private const string Summer = "summer-league-2015/Stats";
    private static readonly string[] Alpha = ["Alpha One", "Alpha Two"];
    private static readonly string[] Bravo = ["Bravo One", "Bravo Two"];
    private static readonly string[] Strangers = ["Stranger One", "Stranger Two"];

    private GrifballContext _context;
    private HistoricalImportService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new HistoricalImportService(_context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }), NullLogger<HistoricalImportService>.Instance,
            new GamertagMatching(_context), TimeProvider.System);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>Side 1 (the first two players) beats side 2 (the rest).</summary>
    private static RecordedGameInput Game(string key, DateTime playedAt, string[] players, bool playoffs = true) => new()
    {
        SeasonSourceId = Summer,
        SourceKey = key,
        GameVersion = "halo3",
        PlayedAt = playedAt,
        Map = "Foundry",
        Gametype = "Grifball",
        IsPlayoffs = playoffs,
        Teams =
        [
            new RecordedTeamInput { Side = 1, Name = "Red", Outcome = Outcomes.Won, Score = 3 },
            new RecordedTeamInput { Side = 2, Name = "Blue", Outcome = Outcomes.Lost, Score = 1 },
        ],
        Players = [.. players.Select((p, i) => new RecordedPlayerInput { Gamertag = p, Side = i < 2 ? 1 : 2, Kills = 5, Deaths = 3 })],
    };

    private static HistoricalArchiveInput Summer2015(params RecordedGameInput[] games) => new()
    {
        Seasons = new UnifiedArchive
        {
            Seasons =
            [
                new ArchiveSeason
                {
                    SeasonName = "Summer League 2015",
                    GameVersion = "halo3",
                    League = "AGLA",
                    SourceWorkbook = HistoricalKeys.Slug("Summer League 2015"),
                    SourceSheet = "Stats",
                    Players = [.. Alpha.Select(p => (p, "Alpha")).Concat(Bravo.Select(p => (p, "Bravo")))
                        .Select(p => new ArchivePlayer { Gamertag = p.Item1, TeamName = p.Item2, GamesPlayed = 1 })],
                },
            ],
        },
        RecordedGames = [.. games],
    };

    [Test]
    public async Task PlayoffSeries_WithASideOfNoTeam_AreBracketSlots_ThatTheTeamsLaterSeriesFollow()
    {
        var report = await _service.Import(Summer2015(
            Game("p/1", T, [.. Alpha, .. Strangers]),
            Game("p/2", T.AddDays(1), [.. Alpha, .. Bravo]),
            Game("p/3", T.AddDays(2), [.. Bravo, .. Alpha]),
            Game("p/4", T.AddDays(3), [.. Strangers, .. Alpha])));

        _context.ChangeTracker.Clear();
        var teams = await _context.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamName, t => t.TeamID);
        var series = await _context.SeasonMatches.AsNoTracking().Include(m => m.BracketMatch)
            .OrderBy(m => m.ScheduledTime).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Has.Some.Contains("with a side that is none of the season's teams"));
            Assert.That(series.Select(m => (m.HomeTeamID, m.AwayTeamID)), Is.EqualTo(new (int?, int?)[]
            {
                (teams["Alpha"], null), (teams["Alpha"], teams["Bravo"]), (teams["Bravo"], teams["Alpha"]), (null, teams["Alpha"]),
            }));
            Assert.That(series.Select(m => m.BracketMatch!.RoundNumber), Is.EqualTo(new[] { 1, 2, 3, 4 }), "Each one round past its teams' last");
            // Alpha's second series follows its first; Bravo's first is a seed
            Assert.That(series[1].BracketMatch!.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(series[0].BracketMatch!.MatchBracketInfoID));
            Assert.That(series[1].BracketMatch!.AwayTeamSeedNumber, Is.Not.Null);
            Assert.That(series[2].BracketMatch!.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(series[1].BracketMatch!.MatchBracketInfoID));
            Assert.That(series[2].BracketMatch!.AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(series[1].BracketMatch!.MatchBracketInfoID));
        }
    }

    [Test]
    public async Task ARecordedGameWithOneSide_IsLinkedWithThatSideAlone()
    {
        var oneSided = Game("o/1", T, Alpha, playoffs: false);
        oneSided.Teams.RemoveAt(1);

        var report = await _service.Import(Summer2015(oneSided));

        _context.ChangeTracker.Clear();
        var alpha = await _context.Teams.AsNoTracking().SingleAsync(t => t.TeamName == "Alpha");
        var series = await _context.SeasonMatches.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((series.HomeTeamID, series.AwayTeamID), Is.EqualTo(((int?)alpha.TeamID, (int?)null)));
            Assert.That(report.Warnings, Has.Some.Contains("with a side that is none of the season's teams"));
        }
    }
}
