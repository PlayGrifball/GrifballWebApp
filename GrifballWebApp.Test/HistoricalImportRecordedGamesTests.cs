using GrifballWebApp.Database;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Match = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Test;

/// <summary>
/// Recorded (non-Waypoint) games and season formats in the historical import: players resolved as the sheets' are, sides
/// mapped to the season's teams, series built the way Waypoint games' are, Waypoint duplicates skipped, re-runs idempotent.
/// Late League goes to matchmaking: a matchmaking season, its games matched matches.
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
            new GamertagMatching(_context), TimeProvider.System);
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
            Assert.That(await _context.Set<RecordedGameTeam>().Where(t => t.RecordedGame.SourceKey == "s/edit").Select(t => t.Side).OrderBy(x => x).ToListAsync(), Is.EqualTo(new List<int> { 1, 2 }));
            Assert.That(await _context.Set<RecordedGameParticipant>().Where(x => x.RecordedGame.SourceKey == "s/edit").Select(x => x.Gamertag).ToListAsync(),
                Is.EquivalentTo(new[] { "Alpha One", "Alpha Two", "Bravo One" }));
        });
    }

    [Test]
    public async Task ATeamGoneFromTheArchive_LeavesItsRecordedSides_ForTheTeamTheyAreNow()
    {
        await _service.Import(Summer2015(Game(Summer, "s/1", T, [.. Alpha, .. Bravo])));
        var bravo = await _context.Teams.AsNoTracking().SingleAsync(t => t.TeamName == "Bravo");
        // Bravo renamed in the sheet: the old team goes, and the game's side that was it is the new one's
        var renamed = Summer2015(Game(Summer, "s/1", T, [.. Alpha, .. Bravo]));
        foreach (var player in renamed.Seasons!.Seasons[0].Players.Where(p => p.TeamName == "Bravo"))
            player.TeamName = "Charlie";
        _context.ChangeTracker.Clear();

        var report = await _service.Import(renamed);

        _context.ChangeTracker.Clear();
        var teams = await _context.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamName, t => t.TeamID);
        var sides = await _context.Set<RecordedGameTeam>().AsNoTracking().OrderBy(t => t.Side).Select(t => t.TeamID).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.TeamsRemoved, Is.EqualTo(1));
            Assert.That(teams.Keys, Is.EquivalentTo(new[] { "Alpha", "Charlie" }));
            Assert.That(sides, Is.EqualTo(new int?[] { teams["Alpha"], teams["Charlie"] }), $"No side is team {bravo.TeamID} any more");
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
        var sides = await _context.Set<RecordedGameTeam>().AsNoTracking().Where(t => t.RecordedGame.SourceKey == "s/2").OrderBy(t => t.Side).Select(t => t.TeamID).ToListAsync();
        var alphaOne = (await new SeasonStatsService(_context).GetAsync()).Single(l => l.Gamertag == "Alpha One" && !l.IsPlayoffs);
        var line = await _context.Set<RecordedGameParticipant>().AsNoTracking()
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
            Assert.That(await _context.Set<RecordedGameParticipant>().CountAsync(), Is.EqualTo(16));
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
            Assert.That(await _context.Set<SeriesGame>().AnyAsync(), Is.False);
            Assert.That(await _context.SeasonMatches.AnyAsync(), Is.False, "Series left with no games go");
        });
    }

    /// <summary>A 2-1 series: Alpha wins games 1 and 2, Bravo (on side 1, so the sides are swapped) game 3.</summary>
    private static RecordedGameInput[] TwoOne(bool scrim3 = false) =>
    [
        Game(Summer, "s/1", T, [.. Alpha, .. Bravo]),
        Game(Summer, "s/2", T.AddMinutes(20), [.. Alpha, .. Bravo]),
        Game(Summer, "s/3", T.AddMinutes(40), [.. Bravo, .. Alpha], scrim: scrim3),
    ];

    private async Task<(int? Home, int? Away, SeasonMatchResult? HomeResult, SeasonMatchResult? AwayResult, int BestOf, string[] Games)> TheSeries()
    {
        _context.ChangeTracker.Clear();
        var series = await _context.SeasonMatches.AsNoTracking().Include(m => m.SeriesGames).ThenInclude(g => g.RecordedGame).SingleAsync();
        return (series.HomeTeamScore, series.AwayTeamScore, series.HomeTeamResult, series.AwayTeamResult, series.BestOf,
            [.. series.SeriesGames.OrderBy(g => g.GameNumber).Select(g => g.RecordedGame!.SourceKey)]);
    }

    [Test]
    public async Task ADecidedSeries_LosingAWinnersGame_OnReImport_IsADraw_Undecided()
    {
        await _service.Import(Summer2015(TwoOne()));
        Assert.That(await TheSeries(), Is.EqualTo(((int?)2, (int?)1, (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Loss, 3, new[] { "s/1", "s/2", "s/3" })));

        var report = await _service.Import(Summer2015(TwoOne()[0], TwoOne()[2]));

        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.RecordedGamesRemoved, Is.EqualTo(1));
            Assert.That(await TheSeries(), Is.EqualTo(((int?)1, (int?)1, (SeasonMatchResult?)null, (SeasonMatchResult?)null, 2, new[] { "s/1", "s/3" })),
                "Its score is its games', and an even count is undecided, as an import of the games left gives");
        });
    }

    [Test]
    public async Task ADecidedSeries_LosingTheLosersGame_OnReImport_KeepsItsWinner([Values] bool asScrim)
    {
        await _service.Import(Summer2015(TwoOne()));

        // The loser's win goes: gone from the archive, or a scrim now (kept, in no series)
        var report = await _service.Import(Summer2015(asScrim ? TwoOne(scrim3: true) : TwoOne()[..2]));

        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await TheSeries(), Is.EqualTo(((int?)2, (int?)0, (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Loss, 3, new[] { "s/1", "s/2" })),
                "Bravo's win comes off Bravo's score, though Bravo was on side 1");
            Assert.That(await _context.RecordedGames.AnyAsync(g => g.SourceKey == "s/3"), Is.EqualTo(asScrim));
        });
    }

    [Test]
    public async Task ARecordedGame_ThatIsAWaypointGame_ByItsMatchID_OrTheSameTeamsAroundTheSameTime_IsSkipped()
    {
        var waypoint = Guid.NewGuid();
        _context.XboxUsers.AddRange(XboxUser.FromXbox(100, "Hells Angel cx"), XboxUser.FromXbox(200, "Inf3ktion"));
        var matchTeams = new (int TeamID, long Xuid, Outcomes Outcome)[] { (0, 100L, Outcomes.Won), (1, 200L, Outcomes.Lost) };
        var waypointMatch = Match.Seed(waypoint, startTime: T, duration: TimeSpan.FromMinutes(10));
        foreach (var t in matchTeams)
        {
            waypointMatch.AddTeam(t.TeamID, 0, t.Outcome);
            waypointMatch.AddParticipant(t.TeamID, t.Xuid, new MatchParticipantStats { Kills = 3, Deaths = 1, FirstJoinedTime = T });
        }
        _context.Matches.Add(waypointMatch);
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
            Assert.That(await _context.Set<SeriesGame>().CountAsync(l => l.MatchID == waypoint), Is.EqualTo(1), "The Waypoint game wins");
            Assert.That(await _context.Set<SeriesGame>().CountAsync(l => l.RecordedGameID != null), Is.EqualTo(1));
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

        // Late League is no season at all: a matchmaking season
        Assert.That(await _context.MatchmakingSeasons.Select(s => s.Name).ToListAsync(), Is.EqualTo(new[] { "Late League Season 8 (Halo 3)" }));
        Assert.That(formats, Is.EquivalentTo(new Dictionary<string, SeasonFormat>
        {
            ["Respawn Fall Series 1"] = SeasonFormat.Tournament,
            ["Summer League 2015"] = SeasonFormat.LeagueSeason,
            ["Spring Cup 2016"] = SeasonFormat.Tournament,
        }));
    }

    private const string Late = "late-league-season-8-halo-3/Stats";

    /// <summary>Late League Season 8 (Halo 3), filed under AGLA but LateLeague by its format, with a title the record gives it.</summary>
    private static HistoricalArchiveInput LateLeague8(params RecordedGameInput[] games)
    {
        var entry = Entry("Late League Season 8 (Halo 3)", "halo3", "AGLA", ("Alpha One", "Alpha"), ("Alpha Two", "Alpha"), ("Bravo One", "Bravo"), ("Bravo Two", "Bravo"));
        return new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons = [new ArchiveSeason { SeasonName = entry.SeasonName, GameVersion = "halo3", League = "AGLA", SourceWorkbook = entry.SourceWorkbook, SourceSheet = "Stats", Format = "LateLeague", Players = entry.Players }],
            },
            Championships = new ChampionsArchive
            {
                Champions = [new ArchiveChampionship { Season = "Late League Season 8 (Halo 3)", Tier = "Open", Team = "Alpha", Roster = ["Alpha One", "Alpha Two"] }],
            },
            RecordedGames = [.. games],
        };
    }

    [Test]
    public async Task ALateLeagueSeason_IsAMatchmakingSeason_ItsGamesMatchedMatches_WithNoTitle()
    {
        var report = await _service.Import(LateLeague8(
            Game(Late, "l/1", T, [.. Alpha, .. Bravo], playoffs: true),
            Game(Late, "l/2", T.AddDays(1), ["Alpha One", "Bravo One", "Alpha Two", "Bravo Two"], side1Won: false)));

        var season = await _context.MatchmakingSeasons.AsNoTracking().SingleAsync();
        var matches = await _context.MatchedMatches.AsNoTracking()
            .Include(m => m.HomeTeam).ThenInclude(t => t.Players).ThenInclude(p => p.User)
            .Include(m => m.AwayTeam).ThenInclude(t => t.Players).ThenInclude(p => p.User)
            .Include(m => m.RecordedGame)
            .Include(m => m.MatchmakingSeason!).ThenInclude(s => s.GameVersion)
            .OrderBy(m => m.StartedAt).ToListAsync();
        string[] Names(MatchedTeam side) => [.. side.Players.Select(p => p.User.DisplayName).Order()];

        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.MatchmakingSeasonsCreated, Is.EqualTo(1));
            Assert.That((season.Name, season.HistoricalSourceID, season.StartsAt.Date, season.EndsAt.Date), Is.EqualTo(("Late League Season 8 (Halo 3)", Late, T.Date, T.Date.AddDays(1))));
            Assert.That(await _context.Seasons.AnyAsync(), Is.False, "Not a season");
            Assert.That(await _context.SeasonMatches.AnyAsync(), Is.False, "No series");
            Assert.That(report.Skipped, Has.Some.Contains("Late League is matchmaking and has no champion"));
            Assert.That(await _context.Set<PlayerChampionship>().AnyAsync(), Is.False);
            Assert.That(matches.Select(m => (m.RecordedGame!.SourceKey, m.MatchmakingSeasonID, m.Status, m.StartedAt)),
                Is.EqualTo(new[] { ("l/1", (int?)season.MatchmakingSeasonID, MatchedMatchStatus.Reported, T), ("l/2", (int?)season.MatchmakingSeasonID, MatchedMatchStatus.Reported, T.AddDays(1)) }));
            Assert.That((Names(matches[0].HomeTeam), Names(matches[0].AwayTeam)), Is.EqualTo((Alpha, Bravo)));
            Assert.That((matches[0].RecordedGameID, matches[0].MatchmakingSeason!.GameVersion!.Slug), Is.EqualTo(((int?)matches[0].RecordedGame!.RecordedGameID, "halo3")));
            Assert.That((Names(matches[1].HomeTeam), Names(matches[1].AwayTeam)), Is.EqualTo((new[] { "Alpha One", "Bravo One" }, new[] { "Alpha Two", "Bravo Two" })), "Random sides each game");
            Assert.That(await _context.RecordedGames.Select(g => new { g.SeasonID, g.IsPlayoffs }).ToListAsync(), Is.All.EqualTo(new { SeasonID = (int?)null, IsPlayoffs = false }));
            Assert.That(await _context.MatchmakingPlayerStats.CountAsync(s => s.MatchmakingSeasonID == season.MatchmakingSeasonID), Is.EqualTo(4), "The sheet's rows");
        });
    }

    [Test]
    public async Task ALateLeagueSeason_ReImported_ChangesNothing_AndLosesWhatTheArchiveDropped()
    {
        await _service.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo]), Game(Late, "l/2", T.AddDays(1), [.. Alpha, .. Bravo])));
        var again = await _service.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo]), Game(Late, "l/2", T.AddDays(1), [.. Alpha, .. Bravo])));
        var counts = (Matches: await _context.MatchedMatches.CountAsync(), Players: await _context.Set<MatchedPlayer>().CountAsync(), Stats: await _context.MatchmakingPlayerStats.CountAsync());

        // l/2 is gone from the archive, and Bravo Two left l/1
        var droppedInput = LateLeague8(Game(Late, "l/1", T, [.. Alpha, "Bravo One"]));
        droppedInput.Seasons.Seasons[0].Players.RemoveAt(3);
        var dropped = await _service.Import(droppedInput);

        Assert.Multiple(async () =>
        {
            Assert.That((again.MatchmakingSeasonsCreated, again.MatchmakingSeasonsUpdated, again.RecordedGamesRemoved, again.StatsRemoved), Is.EqualTo((0, 1, 0, 0)));
            Assert.That(counts, Is.EqualTo((2, 8, 4)));
            Assert.That(dropped.RecordedGamesRemoved, Is.EqualTo(1));
            Assert.That(dropped.SeasonRemovals.Select(r => (r.SeasonKey, r.StatsBefore, r.StatsRemoved)), Is.EqualTo(new[] { (Late, 4, 1) }), "Bravo Two left the sheet");
            Assert.That(await _context.MatchedMatches.CountAsync(), Is.EqualTo(1));
            Assert.That(await _context.Set<MatchedTeam>().CountAsync(), Is.EqualTo(2));
            Assert.That(await _context.Set<MatchedPlayer>().CountAsync(), Is.EqualTo(3));
            Assert.That(await _context.RecordedGames.Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(new[] { "l/1" }));
        });
    }

    [Test]
    public async Task LateLeaguePages_ListItsSeasons_AndASeasonsPlayers()
    {
        await _service.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo]), Game(Late, "l/2", T.AddDays(1), [.. Alpha, .. Bravo])));
        var controller = new GrifballWebApp.Server.LateLeague.LateLeagueController(new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context));

        var seasons = await controller.GetSeasons(CancellationToken.None);
        var season = (await controller.GetSeason(seasons.Single().MatchmakingSeasonID, CancellationToken.None)).Value!;
        var missing = await controller.GetSeason(seasons.Single().MatchmakingSeasonID + 1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(seasons.Select(s => (s.Name, s.GameVersionSlug, s.GameCount, s.PlayerCount, s.IsHistorical)),
                Is.EqualTo(new[] { ("Late League Season 8 (Halo 3)", (string?)"halo3", 2, 4, true) }));
            Assert.That(season.Players.Select(p => (p.Gamertag, p.Source, p.GamesPlayed, p.Wins)).OrderBy(p => p.Gamertag),
                Is.EqualTo(new[] { ("Alpha One", "Matches", 2, 2), ("Alpha Two", "Matches", 2, 2), ("Bravo One", "Matches", 2, 0), ("Bravo Two", "Matches", 2, 0) }));
            Assert.That(missing.Result, Is.TypeOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());
        });
    }

    [Test]
    public async Task ALateLeagueSeason_DryRun_ChangesNothing_AndTheGuardrailStopsBigRemovals()
    {
        var dry = await _service.Run(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo])), new HistoricalImportRunOptions { DryRun = true });
        Assert.That((dry.MatchmakingSeasonsCreated, await _context.MatchmakingSeasons.CountAsync(), await _context.MatchedMatches.CountAsync()), Is.EqualTo((1, 0, 0)), "Said, not done");

        await _service.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo])));
        // The sheet loses three of its four players: more than the guardrail lets go
        var shrunk = LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo]));
        shrunk.Seasons.Seasons[0].Players.RemoveRange(1, 3);
        var guarded = await _service.Run(shrunk, new HistoricalImportRunOptions { Guardrail = new HistoricalGuardrailOptions() });

        Assert.Multiple(async () =>
        {
            Assert.That(guarded.SeasonRemovals.Single().Stopped, Is.True);
            Assert.That(guarded.Warnings, Has.Some.Contains("stopped for review"));
            Assert.That(await _context.MatchmakingPlayerStats.CountAsync(), Is.EqualTo(4), "Left as it was");
        });
    }

    [Test]
    public async Task ALateLeagueSeasonThatFails_IsRolledBack_AndReported()
    {
        var failing = new HistoricalImportService(_context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions()), NullLogger<HistoricalImportService>.Instance,
            new GamertagMatching(_context), new BrokenClock());

        var report = await failing.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo])));

        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.EqualTo(new[] { $"{Late}: The clock broke" }));
            Assert.That(await _context.MatchmakingSeasons.AnyAsync(), Is.False);
            Assert.That(await _context.RecordedGames.AnyAsync(), Is.False);
        });
    }

    /// <summary>A clock that fails when read: the import fails partway through a season.</summary>
    private sealed class BrokenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("The clock broke");
    }

    [Test]
    public async Task OddLateLeagueGames_AreReported_AndLeftOut()
    {
        var oneSide = Game(Late, "l/one-side", T.AddDays(2), [.. Alpha, .. Bravo]);
        var input = LateLeague8(
            Game(Late, "l/1", T, [.. Alpha, .. Bravo]),
            Game(Late, "l/1", T, [.. Alpha, .. Bravo]),
            Game(Late, "l/halo9", T.AddDays(1), [.. Alpha, .. Bravo], game: "halo9"),
            new RecordedGameInput { SeasonSourceId = Late, SourceKey = oneSide.SourceKey, GameVersion = "halo3", PlayedAt = oneSide.PlayedAt, Teams = [oneSide.Teams[0]], Players = oneSide.Players });
        input.Seasons.Seasons[0].Players.Add(new ArchivePlayer { Gamertag = "alpha one", GamesPlayed = 3 });
        var report = await _service.Import(input);

        Assert.Multiple(async () =>
        {
            Assert.That(report.Skipped, Has.Some.Contains("l/1: listed twice"));
            Assert.That(report.Skipped, Has.Some.Contains("alpha one is a second row for the same player"));
            Assert.That(report.Skipped, Has.Some.Contains("l/halo9: unknown game version 'halo9'"));
            Assert.That(report.Skipped, Has.Some.Contains("l/one-side: a matchmaking game has two sides, it has 1"));
            Assert.That(await _context.MatchedMatches.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AGameASeasonHadBefore_LeavesItsSeries_ForLateLeague()
    {
        // The same game imported before as a season's (as Late League was), in a series
        await _service.Import(Summer2015(Game(Summer, "l/1", T, [.. Alpha, .. Bravo])));
        Assert.That(await _context.Set<SeriesGame>().CountAsync(), Is.EqualTo(1));

        await _service.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo])));

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.Set<SeriesGame>().AnyAsync(), Is.False);
            Assert.That(await _context.SeasonMatches.AnyAsync(), Is.False, "Its series had no other game");
            Assert.That(await _context.RecordedGames.Select(g => new { g.SourceKey, g.SeasonID, Matched = g.MatchedMatch != null }).SingleAsync(),
                Is.EqualTo(new { SourceKey = "l/1", SeasonID = (int?)null, Matched = true }));
        });
    }

    [Test]
    public async Task TheDiscordQueuesReportedMatches_AreLateLeague_InNoMatchmakingSeason()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, T, [101, 102], Outcomes.Won, [201, 202], Outcomes.Lost);
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value, u => u.Id);
        MatchedTeam Side(params long[] xuids) => new([.. xuids.Select(x => new MatchedPlayer(users[x]))]);
        _context.MatchedMatches.Add(MatchedMatch.Start(Side(101, 102), Side(201, 202)).Reported(match.MatchID));
        await _context.SaveChangesAsync();

        var lines = await new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context).GetAsync(userID: users[101]);

        var queue = await new GrifballWebApp.Server.LateLeague.LateLeagueController(new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context)).GetQueue(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(lines.Select(l => (l.MatchmakingSeasonID, l.MatchmakingSeasonName, l.SeasonKey, l.FromMatches, l.GamesPlayed, l.Wins, l.GameVersionSlug)),
                Is.EqualTo(new[] { ((int?)null, (string?)null, (string?)null, true, 1, 1, (string?)"infinite") }));
            Assert.That((queue.GameCount, queue.Players.Count, queue.Players.All(p => p.MatchmakingSeasonID == null)), Is.EqualTo((1, 4, true)), "Late League as it is played now");
        });
    }

    [Test]
    public async Task ALateLeagueGame_ASeasonTakes_LeavesMatchmaking_AndIsCountedOnce()
    {
        await _service.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo])));

        // The archive files the game under a season now
        await _service.Import(Summer2015(Game(Summer, "l/1", T, [.. Alpha, .. Bravo])));
        var lateLeague = await new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context).GetAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.MatchedMatches.AnyAsync(), Is.False);
            Assert.That(await _context.Set<MatchedPlayer>().AnyAsync(), Is.False);
            Assert.That(await _context.Set<SeriesGame>().CountAsync(l => l.RecordedGameID != null), Is.EqualTo(1));
            Assert.That(lateLeague.Where(l => l.FromMatches), Is.Empty, "Its sheet's rows are all Late League has of it");
        });

        // And a season's game the archive drops, once a Late League game, goes cleanly
        await _service.Import(LateLeague8(Game(Late, "l/2", T, [.. Alpha, .. Bravo])));
        _context.ChangeTracker.Clear();
        var game = await _context.RecordedGames.SingleAsync(g => g.SourceKey == "l/2");
        _context.SetRaw(game, g => g.SeasonID, (int?)(await _context.Seasons.SingleAsync()).SeasonID);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var report = await _service.Import(Summer2015(Game(Summer, "l/1", T, [.. Alpha, .. Bravo])));
        Assert.That(report.Errors, Is.Empty);
        Assert.That((await _context.RecordedGames.AnyAsync(g => g.SourceKey == "l/2"), await _context.MatchedMatches.AnyAsync()), Is.EqualTo((false, false)));
    }

    [Test]
    public async Task ALateLeagueSeasons_WaypointMatches_AreReported_NotPulled()
    {
        var input = LateLeague8(new RecordedGameInput
        {
            SeasonSourceId = Late, SourceKey = "l/wp", GameVersion = "halo3", PlayedAt = T, WaypointMatchId = Guid.NewGuid().ToString(),
            Teams = Game(Late, "l/wp", T, [.. Alpha, .. Bravo]).Teams, Players = Game(Late, "l/wp", T, [.. Alpha, .. Bravo]).Players,
        });
        input = new HistoricalArchiveInput
        {
            Seasons = input.Seasons, Championships = input.Championships, RecordedGames = input.RecordedGames,
            WaypointMatches = new() { [Late] = new WaypointSeasonMatches { Regular = [Guid.NewGuid().ToString()] } },
        };

        var report = await _service.Import(input);

        Assert.Multiple(async () =>
        {
            Assert.That(report.Skipped, Has.Some.Contains("a Late League season's games are its recorded games"));
            Assert.That(report.Warnings, Has.Some.Contains("l/wp: has Waypoint match"));
            Assert.That(report.WaypointMatchesPulled, Is.Zero);
            Assert.That(await _context.MatchedMatches.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task LateLeagueStats_AreTheGames_WhereThereAreAny_ElseTheSheets_AndCountInTheCareer()
    {
        await _service.Import(LateLeague8(Game(Late, "l/1", T, [.. Alpha, .. Bravo])));
        var alphaOne = await _context.Users.SingleAsync(u => u.DisplayName == "Alpha One");
        // A player with no game of the season: the sheet's row is theirs
        var stats = new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context);
        _context.Set<MatchedPlayer>().RemoveRange(_context.Set<MatchedPlayer>().Where(p => p.User.DisplayName == "Bravo Two"));
        _context.Set<RecordedGameParticipant>().RemoveRange(_context.Set<RecordedGameParticipant>().Where(p => p.Gamertag == "Bravo Two"));
        await _context.SaveChangesAsync();

        var lines = await stats.GetAsync();
        var profile = await new GrifballWebApp.Server.Profile.ProfileStatsService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context), stats).GetAsync(alphaOne.Id);

        Assert.Multiple(() =>
        {
            Assert.That(lines.Select(l => (l.Gamertag, l.FromMatches, l.GamesPlayed, l.Kills)).OrderBy(l => l.Gamertag),
                Is.EqualTo(new[] { ("Alpha One", true, 1, 5), ("Alpha Two", true, 1, 5), ("Bravo One", true, 1, 5), ("Bravo Two", false, 1, 0) }));
            Assert.That(lines.Select(l => l.MatchmakingSeasonName).Distinct(), Is.EqualTo(new[] { "Late League Season 8 (Halo 3)" }));
            Assert.That(profile!.LateLeague.Single().Wins, Is.EqualTo(1));
            Assert.That(profile.Seasons, Is.Empty);
            Assert.That((profile.OverallCareer!.GamesPlayed, profile.OverallCareer.Kills, profile.OverallCareer.SeasonsCount), Is.EqualTo((1, 5, 1)), "Late League counts in the career");
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
            Assert.That(report.Warnings, Has.One.Contains("recorded game(s) with a side that is none of the season's teams").And.Contains("s/strangers"), "Once for the season");
            Assert.That(report.RecordedGamesImported, Is.EqualTo(4));
            Assert.That(await _context.Set<RecordedGameTeam>().CountAsync(t => t.RecordedGame.SourceKey == "s/side-twice"), Is.EqualTo(2), "A side listed twice is taken once");
            Assert.That(await _context.Set<RecordedGameTeam>().Where(t => t.RecordedGame.SourceKey == "s/same-team").Select(t => t.TeamID).ToListAsync(), Is.All.Null);
            Assert.That(await _context.Set<RecordedGameParticipant>().CountAsync(p => p.RecordedGame.SourceKey == "s/strangers"), Is.EqualTo(3));
        });
    }

    [Test]
    public void Format_AnUnknownArchiveFormat_FallsBackToTheLeagueAndWorkbook()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HistoricalKeys.Format("99", "agla", "wb", "Summer League"), Is.EqualTo(SeasonFormat.LeagueSeason));
            Assert.That(HistoricalKeys.Format(null, "side", "2021-side-respawn-h4-1", "Fall Series 1"), Is.EqualTo(SeasonFormat.Tournament));
            Assert.That(HistoricalKeys.Format("LateLeague", "agla", null, "Summer League"), Is.EqualTo(SeasonFormat.LeagueSeason), "No such format: Late League is matchmaking");
        });
    }

    [Test]
    public void IsLateLeague_ByTheLeague_TheFormat_OrTheName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HistoricalKeys.IsLateLeague("Late League", null, "Season 5"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("late-league", null, "Season 5"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("AGLA", "LateLeague", "S8"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("AGLA", null, "Late League Season 8 (Halo 3)"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("AGLA", "Tournament", "Summer League 2015"), Is.False);
        });
    }
}
