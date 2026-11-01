using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

/// <summary>
/// Recorded (non-Waypoint) games and season formats in the historical import: players resolved as the sheets' are, sides
/// mapped to the season's teams, series built the way Waypoint games' are, Waypoint duplicates skipped, re-runs idempotent.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportRecordedGamesTests
{
    private static readonly DateTime T = new(2015, 6, 1, 20, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context;
    private HistoricalImportService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new HistoricalImportService(_context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }), NullLogger<HistoricalImportService>.Instance,
            new GrifballWebApp.Server.Championships.ChampionshipService(_context), new GamertagMatching(_context), TimeProvider.System);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static ArchiveSeason Entry(string name, string game, string league, params (string Gamertag, string Team)[] players) => new()
    {
        SeasonName = name,
        GameVersion = game,
        League = league,
        SourceWorkbook = HistoricalKeys.Slug(name),
        SourceSheet = "Stats",
        Players = [.. players.Select(p => new ArchivePlayer { Gamertag = p.Gamertag, TeamName = p.Team, GamesPlayed = 1 })],
    };

    /// <summary>Side 1 (the first two players) beats side 2 (the other two) unless told otherwise.</summary>
    private static RecordedGameInput Game(string season, string key, DateTime? playedAt, string[] players, bool side1Won = true, bool playoffs = false, bool scrim = false, string? waypointMatchId = null, string game = "halo3") => new()
    {
        SeasonSourceId = season,
        SourceKey = key,
        GameVersion = game,
        PlayedAt = playedAt,
        Map = "Foundry",
        Gametype = "Grifball",
        IsPlayoffs = playoffs,
        IsScrim = scrim,
        WaypointMatchId = waypointMatchId,
        Teams =
        [
            new RecordedTeamInput { Side = 1, Name = "Red", Outcome = side1Won ? Outcomes.Won : Outcomes.Lost, Score = side1Won ? 3 : 1 },
            new RecordedTeamInput { Side = 2, Name = "Blue", Outcome = side1Won ? Outcomes.Lost : Outcomes.Won, Score = side1Won ? 1 : 3 },
        ],
        Players = [.. players.Select((p, i) => new RecordedPlayerInput { Gamertag = p, Side = i < 2 ? 1 : 2, Kills = 5, Deaths = 3, Assists = 1, Goals = i == 0 ? 2 : 0, CarrierKills = 1 })],
    };

    private static readonly string[] Alpha = ["Alpha One", "Alpha Two"];
    private static readonly string[] Bravo = ["Bravo One", "Bravo Two"];
    private const string Summer = "summer-league-2015/Stats";

    private static HistoricalArchiveInput Summer2015(params RecordedGameInput[] games) => new()
    {
        Seasons = new UnifiedArchive
        {
            Seasons = [Entry("Summer League 2015", "halo3", "AGLA", ("Alpha One", "Alpha"), ("Alpha Two", "Alpha"), ("Bravo One", "Bravo"), ("Bravo Two", "Bravo"))],
        },
        RecordedGames = [.. games],
    };

    [Test]
    public async Task ARecordedGameCorrectedInTheSheet_LosesTheSideAndPlayerNoLongerListed()
    {
        var first = Game(Summer, "s/edit", T, [.. Alpha, .. Bravo]);
        first.Teams.Add(new RecordedTeamInput { Side = 3, Name = "Spectators", Outcome = Outcomes.Lost, Score = 0 });
        await _service.Import(Summer2015(first));

        var corrected = Game(Summer, "s/edit", T, [.. Alpha, .. Bravo]);
        corrected.Players.RemoveAll(p => p.Gamertag == "Bravo Two");
        var report = await _service.Import(Summer2015(corrected));

        _context.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await _context.RecordedGameTeams.Where(t => t.RecordedGame.SourceKey == "s/edit").Select(t => t.Side).OrderBy(x => x).ToListAsync(), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(await _context.RecordedGameParticipants.Where(x => x.RecordedGame.SourceKey == "s/edit").Select(x => x.Gamertag).ToListAsync(),
                Is.EquivalentTo(new[] { "Alpha One", "Alpha Two", "Bravo One" }));
        });
    }

    [Test]
    public async Task RecordedGames_SidesMappedToTeams_SeriesBuilt_ScrimsUnlinked_AndAReRunChangesNothing()
    {
        var input = Summer2015(
            Game(Summer, "s/1", T, [.. Alpha, .. Bravo]),
            // Bravo on side 1 this time: still the same series, Alpha's second win
            Game(Summer, "s/2", T.AddMinutes(30), [.. Bravo, .. Alpha], side1Won: false),
            Game(Summer, "s/scrim", T.AddDays(1), [.. Alpha, .. Bravo], scrim: true),
            Game(Summer, "s/final", T.AddDays(7), [.. Alpha, .. Bravo], playoffs: true),
            Game("nowhere/Stats", "s/lost", T, [.. Alpha, .. Bravo]),
            Game(Summer, "s/unknown-game", T, [.. Alpha, .. Bravo], game: "halo2"));

        var report = await _service.Import(input);
        var ids = await _context.RecordedGames.OrderBy(g => g.RecordedGameID).Select(g => g.RecordedGameID).ToListAsync();
        var again = await _service.Import(input);
        _context.ChangeTracker.Clear();

        var teams = await _context.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamName, t => t.TeamID);
        var series = await _context.SeasonMatches.AsNoTracking().Include(m => m.SeriesGames).ThenInclude(g => g.RecordedGame).Include(m => m.BracketMatch).OrderBy(m => m.ScheduledTime).ToListAsync();
        var scrim = await _context.RecordedGames.AsNoTracking().Include(g => g.SeriesGame).SingleAsync(g => g.SourceKey == "s/scrim");
        var sides = await _context.RecordedGameTeams.AsNoTracking().Where(t => t.RecordedGame.SourceKey == "s/2").OrderBy(t => t.Side).Select(t => t.TeamID).ToListAsync();
        var alphaOne = (await new SeasonStatsService(_context).GetAsync()).Single(l => l.Gamertag == "Alpha One" && !l.IsPlayoffs);
        var line = await _context.RecordedGameParticipants.AsNoTracking()
            .Include(p => p.RecordedGame).ThenInclude(g => g.GameVersion)
            .Include(p => p.RecordedGame).ThenInclude(g => g.Season)
            .Include(p => p.RecordedGameTeam).ThenInclude(t => t!.Team)
            .Include(p => p.User)
            .SingleAsync(p => p.RecordedGame.SourceKey == "s/1" && p.Gamertag == "Alpha One");
        Assert.Multiple(() =>
        {
            Assert.That(line.RecordedGameID, Is.EqualTo(line.RecordedGame.RecordedGameID));
            Assert.That(line.RecordedGameTeamID, Is.EqualTo(line.RecordedGameTeam!.RecordedGameTeamID));
            Assert.That(line.RecordedGameTeam.RecordedGameID, Is.EqualTo(line.RecordedGameID));
            Assert.That(line.RecordedGameTeam.Team!.TeamName, Is.EqualTo("Alpha"), "A side is its season team");
            Assert.That((line.UserID, line.User.IsDummyUser), Is.EqualTo((line.User.Id, true)));
            Assert.That(line.RecordedGame.GameVersion.GameVesionID, Is.EqualTo(line.RecordedGame.GameVersionID));
            Assert.That(line.RecordedGame.Season!.SeasonID, Is.EqualTo(series[0].SeasonID));
            Assert.That(series[0].SeriesGames.Select(g => g.SeriesGameID), Has.All.GreaterThan(0));
        });
        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((report.RecordedGamesImported, report.SeasonMatchesCreated, report.GamesLinked), Is.EqualTo((4, 2, 3)));
            Assert.That(report.Skipped, Has.Some.Contains("nowhere/Stats"));
            Assert.That(report.Skipped, Has.Some.Contains("unknown game version"));
            Assert.That(sides, Is.EqualTo(new int?[] { teams["Bravo"], teams["Alpha"] }), "Each side is its players' team");
            Assert.That(series.Select(s => (s.HomeTeamID, s.AwayTeamID, s.HomeTeamScore, s.AwayTeamScore, s.SeriesGames.Count, s.BracketMatch != null)), Is.EqualTo(new[]
            {
                ((int?)teams["Alpha"], (int?)teams["Bravo"], (int?)2, (int?)0, 2, false),
                ((int?)teams["Alpha"], (int?)teams["Bravo"], (int?)1, (int?)0, 1, true),
            }), "Two games 30 minutes apart are one series; the playoff game a bracket match");
            Assert.That(series[0].SeriesGames.OrderBy(g => g.GameNumber).Select(g => g.RecordedGame!.SourceKey), Is.EqualTo(new[] { "s/1", "s/2" }));
            Assert.That((scrim.SeriesGame, scrim.SeasonID), Is.EqualTo(((SeriesGame?)null, (int?)series[0].SeasonID)), "A scrim is in no series but is the season's");
            Assert.That((alphaOne.FromMatches, alphaOne.GamesPlayed, alphaOne.Wins, alphaOne.Goals, alphaOne.TeamID), Is.EqualTo((true, 3, 3, 2 + 0 + 2, (int?)teams["Alpha"])), "Two series games and the scrim");
            Assert.That(await _context.Users.CountAsync(u => u.IsDummyUser), Is.EqualTo(4), "Recorded players are the sheets' placeholders");
            Assert.That(await _context.RecordedGames.OrderBy(g => g.RecordedGameID).Select(g => g.RecordedGameID).ToListAsync(), Is.EqualTo(ids), "Found again by their source keys");
            Assert.That((again.RecordedGamesImported, again.SeasonMatchesCreated, again.RecordedGamesRemoved), Is.EqualTo((4, 0, 0)));
            Assert.That(await _context.RecordedGameParticipants.CountAsync(), Is.EqualTo(16));
            Assert.That(await _context.SeasonMatches.CountAsync(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task RecordedGames_NoLongerInTheArchive_Go_AndAScrimNowLeavesItsSeries()
    {
        await _service.Import(Summer2015(Game(Summer, "s/1", T, [.. Alpha, .. Bravo]), Game(Summer, "s/2", T.AddDays(2), [.. Alpha, .. Bravo])));

        var report = await _service.Import(Summer2015(Game(Summer, "s/2", T.AddDays(2), [.. Alpha, .. Bravo], scrim: true)));

        Assert.Multiple(async () =>
        {
            Assert.That(report.RecordedGamesRemoved, Is.EqualTo(1));
            Assert.That(await _context.RecordedGames.Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(new[] { "s/2" }));
            Assert.That(await _context.SeriesGames.AnyAsync(), Is.False);
            Assert.That(await _context.SeasonMatches.AnyAsync(), Is.False, "Series left with no games go");
        });
    }

    [Test]
    public async Task ARecordedGame_ThatIsAWaypointGame_ByItsMatchID_OrTheSameTeamsAroundTheSameTime_IsSkipped()
    {
        var waypoint = Guid.NewGuid();
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 100, Gamertag = "Hells Angel cx" }, new XboxUser { XboxUserID = 200, Gamertag = "Inf3ktion" });
        var matchTeams = new (int TeamID, long Xuid, Outcomes Outcome)[] { (0, 100L, Outcomes.Won), (1, 200L, Outcomes.Lost) };
        _context.Matches.Add(new Match
        {
            MatchID = waypoint,
            StartTime = T,
            Duration = TimeSpan.FromMinutes(10),
            MatchTeams = [.. matchTeams.Select(t => new MatchTeam
            {
                MatchID = waypoint, TeamID = t.TeamID, Outcome = t.Outcome,
                MatchParticipants = [new MatchParticipant { MatchID = waypoint, TeamID = t.TeamID, XboxUserID = t.Xuid, Kills = 3, Deaths = 1, FirstJoinedTime = T }],
            })],
        });
        await _context.SaveChangesAsync();
        const string season = "winter-league-2023-da/Stats";
        string[] players = ["Hells Angel cx", "Ram Two", "Inf3ktion", "Bravo Two"];
        HistoricalArchiveInput Archive(bool withWaypoint) => new()
        {
            Seasons = new UnifiedArchive
            {
                Seasons = [Entry("Winter League 2023 (DA)", "infinite", "AGLA", ("Hells Angel cx", "Ram Ranch"), ("Ram Two", "Ram Ranch"), ("Inf3ktion", "Bravo"), ("Bravo Two", "Bravo"))],
            },
            WaypointMatches = withWaypoint ? new() { [season] = new WaypointSeasonMatches { Regular = [waypoint.ToString()] } } : null,
            RecordedGames =
            [
                Game(season, "w/by-id", T, players, waypointMatchId: $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{waypoint}", game: "infinite"),
                Game(season, "w/near", T.AddMinutes(10), players, game: "infinite"),
                Game(season, "w/later", T.AddHours(5), players, game: "infinite"),
            ],
        };
        // Before the Waypoint game is the season's, only its match ID gives the duplicate away
        var first = await _service.Import(Archive(withWaypoint: false));

        var second = await _service.Import(Archive(withWaypoint: true));

        Assert.Multiple(async () =>
        {
            Assert.That((first.RecordedGamesImported, first.RecordedGamesSkippedAsWaypoint), Is.EqualTo((2, 1)));
            Assert.That((second.RecordedGamesImported, second.RecordedGamesSkippedAsWaypoint, second.RecordedGamesRemoved), Is.EqualTo((1, 2, 1)),
                "Now the game ten minutes from the Waypoint game is its duplicate too, and the one the first run added goes");
            Assert.That(second.Warnings, Has.Some.Contains("w/by-id"));
            Assert.That(second.Warnings, Has.Some.Contains("w/near"));
            Assert.That(await _context.RecordedGames.Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(new[] { "w/later" }));
            Assert.That(await _context.SeriesGames.CountAsync(l => l.MatchID == waypoint), Is.EqualTo(1), "The Waypoint game wins");
            Assert.That(await _context.SeriesGames.CountAsync(l => l.RecordedGameID != null), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TheFormat_FollowsTheLeagueAndWorkbook_OrTheArchive()
    {
        var overridden = Entry("Spring Cup 2016", "halo3", "AGLA", ("A", "Team A"));
        await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons =
                [
                    Entry("Late League Season 8 (Halo 3)", "halo3", "Late League", ("A", "Team A")),
                    Entry("Respawn Fall Series 1", "halo4", "Community/Side", ("A", "Team A")),
                    Entry("Summer League 2015", "halo3", "AGLA", ("A", "Team A")),
                    new ArchiveSeason { SeasonName = overridden.SeasonName, GameVersion = "halo3", League = "AGLA", SourceWorkbook = overridden.SourceWorkbook, SourceSheet = "Stats", Format = "tournament", Players = overridden.Players },
                ],
            },
        });

        var formats = await _context.Seasons.AsNoTracking().ToDictionaryAsync(s => s.SeasonName, s => s.Format);

        Assert.That(formats, Is.EquivalentTo(new Dictionary<string, SeasonFormat>
        {
            ["Late League Season 8 (Halo 3)"] = SeasonFormat.LateLeague,
            ["Respawn Fall Series 1"] = SeasonFormat.Tournament,
            ["Summer League 2015"] = SeasonFormat.LeagueSeason,
            ["Spring Cup 2016"] = SeasonFormat.Tournament,
        }));
    }

    [Test]
    public async Task ALateLeagueSeason_GetsNoTitle_AndNoPlayoffSeries()
    {
        const string late = "late-league-season-8-halo-3/Stats";
        var entry = Entry("Late League Season 8 (Halo 3)", "halo3", "AGLA", ("Alpha One", "Alpha"), ("Alpha Two", "Alpha"), ("Bravo One", "Bravo"), ("Bravo Two", "Bravo"));
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons = [new ArchiveSeason { SeasonName = entry.SeasonName, GameVersion = "halo3", League = "AGLA", SourceWorkbook = entry.SourceWorkbook, SourceSheet = "Stats", Format = "LateLeague", Players = entry.Players }],
            },
            Championships = new ChampionsArchive
            {
                Champions = [new ArchiveChampionship { Season = "Late League Season 8 (Halo 3)", Tier = "Open", Team = "Alpha", Roster = ["Alpha One", "Alpha Two"] }],
            },
            RecordedGames = [Game(late, "l/final", T, [.. Alpha, .. Bravo], playoffs: true)],
        };

        var report = await _service.Import(input);

        Assert.Multiple(async () =>
        {
            Assert.That(report.Skipped, Has.Some.Contains("a Late League season has no champion"));
            Assert.That(await _context.PlayerChampionships.AnyAsync(), Is.False);
            Assert.That(await _context.MatchBracketInfo.AnyAsync(), Is.False, "No playoffs: the series is a regular one");
            Assert.That(await _context.RecordedGames.Select(g => g.IsPlayoffs).SingleAsync(), Is.False);
            Assert.That(await _context.SeasonMatches.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task OddRecordedGames_AreReported_AndStillCount()
    {
        var twiceSide = Game(Summer, "s/side-twice", T.AddDays(3), [.. Alpha, .. Bravo]);
        var report = await _service.Import(Summer2015(
            Game(Summer, "s/1", T, [.. Alpha, .. Bravo]),
            Game(Summer, "s/1", T, [.. Alpha, .. Bravo]),
            Game(Summer, "s/same-team", T.AddDays(1), ["Alpha One", "Bravo One", "Alpha Two", "Bravo Two"]),
            Game(Summer, "s/strangers", T.AddDays(2), [.. Alpha, "Nobody One", "nobody one"]),
            new RecordedGameInput
            {
                SeasonSourceId = Summer, SourceKey = twiceSide.SourceKey, GameVersion = "halo3", PlayedAt = twiceSide.PlayedAt,
                Teams = [.. twiceSide.Teams, new RecordedTeamInput { Side = 1, Outcome = Outcomes.Lost }], Players = twiceSide.Players,
            }));

        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Skipped, Has.Some.Contains("s/1: listed twice"));
            Assert.That(report.Skipped, Has.Some.Contains("nobody one is listed twice for one player"));
            Assert.That(report.Warnings, Has.Some.Contains("s/same-team: both sides are"));
            Assert.That(report.Warnings, Has.Some.Contains("s/strangers: a side is none of the season's teams"));
            Assert.That(report.RecordedGamesImported, Is.EqualTo(4));
            Assert.That(await _context.RecordedGameTeams.CountAsync(t => t.RecordedGame.SourceKey == "s/side-twice"), Is.EqualTo(2), "A side listed twice is taken once");
            Assert.That(await _context.RecordedGameTeams.Where(t => t.RecordedGame.SourceKey == "s/same-team").Select(t => t.TeamID).ToListAsync(), Is.All.Null);
            Assert.That(await _context.RecordedGameParticipants.CountAsync(p => p.RecordedGame.SourceKey == "s/strangers"), Is.EqualTo(3));
        });
    }

    [Test]
    public void Format_AnUnknownArchiveFormat_FallsBackToTheLeagueAndWorkbook()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HistoricalKeys.Format("99", "agla", "wb", "Summer League"), Is.EqualTo(SeasonFormat.LeagueSeason));
            Assert.That(HistoricalKeys.Format("bracket", "late-league", null, "Late League Season 5"), Is.EqualTo(SeasonFormat.LateLeague));
            Assert.That(HistoricalKeys.Format(null, "side", "2021-side-respawn-h4-1", "Fall Series 1"), Is.EqualTo(SeasonFormat.Tournament));
            Assert.That(HistoricalKeys.Format("LateLeague", "agla", null, "Respawn Fall Series"), Is.EqualTo(SeasonFormat.LateLeague));
        });
    }
}
