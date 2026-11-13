using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// HistoricalImportService paths HistoricalImportServiceUnitTests leaves, through Import/Run against the fake SQLite
/// context: what grouping the archive warns or skips, pulling Waypoint matches (retried, failed, pulled), a gamertag more
/// than one account's history has, a titles-only season adopted or retired (kept, stopped, dry run), a renamed team, a
/// second row or title, recorded games (refused, a Waypoint game's, one-team, teamless, playoffs, gone, a scrim now),
/// a Waypoint match of three teams, and the roster matches suggested for a sheet row no account matched.
/// The season whose save fails (rolled back, the run going on) and the schedule's title rosters are left to
/// GrifballWebApp.Integration.Test: SQLite enforces no column length to make a save fail, and the schedule needs the
/// Waypoint scenarios there.
/// </summary>
[TestFixture]
internal sealed class HistoricalImportServiceMoreUnitTests : ApplicationDbContextTests
{
    private const string CupKey = "mcc-cup-2016/Stats";
    private static readonly DateTime CupPlayedAt = new(2016, 5, 1, 20, 0, 0, DateTimeKind.Utc);

    private IDataPullService _dataPull = null!;
    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _dataPull = Substitute.For<IDataPullService>();
        _service = new HistoricalImportService(Context, _dataPull, Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System);
    }

    private static HistoricalArchiveInput Input(params ArchiveSeason[] entries) => new() { Seasons = new UnifiedArchive { Seasons = [.. entries] } };

    private static ArchiveSeason Entry(string name, string game, string league, bool playoffs, params ArchivePlayer[] players) => new()
    {
        SeasonName = name,
        GameVersion = game,
        League = league,
        Division = "Open",
        IsPlayoffs = playoffs,
        SourceWorkbook = HistoricalKeys.Slug(name),
        SourceSheet = playoffs ? "Playoff Stats" : "Stats",
        Players = [.. players],
    };

    private static ArchivePlayer Player(string gamertag, string? team = null, int stint = 1) => new()
    {
        Gamertag = gamertag,
        Kills = 1,
        Deaths = 1,
        GamesPlayed = 1,
        StintIndex = stint,
        TeamName = team,
    };

    /// <summary>MCC Cup 2016: Alpha One and Alpha Two on Alpha, Beta One on Beta.</summary>
    private static ArchiveSeason Cup() => Entry("MCC Cup 2016", "halo3", "AGLA", false,
        Player("Alpha One", team: "Alpha"), Player("Alpha Two", team: "Alpha"), Player("Beta One", team: "Beta"));

    private static RecordedGameInput Game(string key, (string Gamertag, int Side)[] players, string gameVersion = "halo3", bool isPlayoffs = false,
        bool isScrim = false, string? waypointMatchId = null, int sides = 2, string seasonKey = CupKey) => new()
    {
        SeasonSourceId = seasonKey,
        SourceKey = key,
        GameVersion = gameVersion,
        PlayedAt = CupPlayedAt,
        IsPlayoffs = isPlayoffs,
        IsScrim = isScrim,
        WaypointMatchId = waypointMatchId,
        Teams = [.. new[] { new RecordedTeamInput { Side = 1, Outcome = Outcomes.Won }, new RecordedTeamInput { Side = 2, Outcome = Outcomes.Lost } }.Take(sides)],
        Players = [.. players.Select(p => new RecordedPlayerInput { Gamertag = p.Gamertag, Side = p.Side, Kills = 5, Deaths = 3 })],
    };

    private static HistoricalArchiveInput CupWith(params RecordedGameInput[] games) => new() { Seasons = new UnifiedArchive { Seasons = [Cup()] }, RecordedGames = [.. games] };

    /// <summary>A Waypoint match already in the database (GetAndSaveMatch's result): a team per outcome.</summary>
    private async Task AddMatch(Guid matchId, params (long Xuid, string Gamertag, Outcomes Outcome)[] players)
    {
        var match = GrifballWebApp.Domain.Halo.Match.Seed(matchId, duration: TimeSpan.FromMinutes(10));
        var teams = new Dictionary<Outcomes, int>();
        foreach (var player in players)
        {
            var xbox = await Context.XboxUsers.FindAsync(player.Xuid) ?? Context.XboxUsers.Add(XboxUser.FromXbox(player.Xuid, player.Gamertag)).Entity;
            if (!teams.TryGetValue(player.Outcome, out var teamID))
            {
                teamID = teams.Count;
                teams.Add(player.Outcome, teamID);
                match.AddTeam(teamID, 0, player.Outcome);
            }
            match.AddParticipant(teamID, player.Xuid, new MatchParticipantStats
            {
                Kills = 5,
                Deaths = 3,
                PersonalScore = 500,
                TimePlayed = TimeSpan.FromMinutes(10),
                AverageLife = TimeSpan.FromSeconds(12),
                DamageDealt = 1000,
                DamageTaken = 900,
                FirstJoinedTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            }, xboxUser: xbox);
        }
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();
    }

    private static ArchiveSeason InfiniteSeason(params ArchivePlayer[] players) => new()
    {
        SeasonName = "Infinite Cup 2024",
        GameVersion = "infinite",
        League = "AGLA",
        SourceWorkbook = "infinite-cup-2024",
        SourceSheet = "Stats",
        Players = [.. players],
    };

    private static HistoricalArchiveInput WithWaypoint(ArchiveSeason season, WaypointSeasonMatches matches) => new()
    {
        Seasons = new UnifiedArchive { Seasons = [season] },
        WaypointMatches = new() { ["infinite-cup-2024/Stats"] = matches },
    };

    // ---------- Grouping: what the archive's entries say of their season ----------

    [Test]
    public async Task Import_WarnsOfASeasonWithoutASource_AndOfEntriesOfMoreThanOneGameOrDivision()
    {
        var unsourced = new ArchiveSeason { SeasonName = "Spring League 2020", GameVersion = "halo3", League = "AGLA", Players = [Player("P1")] };
        var regular = Entry("Winter League 2019", "halo3", "AGLA", false, Player("P2"));
        var playoffs = new ArchiveSeason
        {
            SeasonName = "Winter League 2019", GameVersion = "reach", League = "AGLA", Division = "Pro", IsPlayoffs = true,
            SourceWorkbook = "winter-league-2019", SourceSheet = "Playoff Stats", Players = [Player("P2")],
        };

        var report = await _service.Import(Input(unsourced, regular, playoffs));

        var seasons = await Context.Seasons.AsNoTracking().OrderBy(s => s.SeasonName).Select(s => new { s.SeasonName, s.HistoricalSourceID, s.Division }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Has.Some.EqualTo("Season Spring League 2020: no source workbook and sheet, keyed agla/spring-league-2020"));
            Assert.That(report.Warnings, Has.Some.EqualTo("Season Winter League 2019: its entries are of more than one game (halo3, reach); the season is halo3, each stat row its entry's"));
            Assert.That(report.Warnings, Has.Some.EqualTo("Season Winter League 2019: its entries are of more than one division; the season is Open"));
            Assert.That(seasons.Select(s => (s.SeasonName, s.HistoricalSourceID, s.Division)), Is.EqualTo(SeasonsKeyed));
        }
    }

    private static readonly (string, string?, string?)[] SeasonsKeyed =
        [("Spring League 2020", "agla/spring-league-2020", null), ("Winter League 2019", "winter-league-2019/Stats", "Open")];

    [Test]
    public async Task Import_SkipsGamesWithNoSeasonToGoTo()
    {
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons = [Entry("Winter League 2019", "halo3", "AGLA", false, Player("P1")), Entry("Late League Season 8 (Halo 3)", "halo3", "Late League", false, Player("P2"))],
            },
            WaypointMatches = new()
            {
                ["late-league-season-8-halo-3/Stats"] = new WaypointSeasonMatches { Regular = [Guid.Empty.ToString()] },
                ["winter-league-2019/Stats"] = new WaypointSeasonMatches { Regular = [Guid.Empty.ToString()] },
            },
            RecordedGames = [Game("nowhere/Stats#1", [("P1", 1), ("P2", 2)], seasonKey: "nowhere/Stats")],
        };

        var report = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.Contains("Waypoint matches for late-league-season-8-halo-3/Stats: a Late League season's games are its recorded games"));
            Assert.That(report.Skipped, Has.Some.EqualTo("Waypoint matches for winter-league-2019/Stats: no Halo Infinite season with that key in the archive"));
            Assert.That(report.Skipped, Has.Some.EqualTo("Recorded game nowhere/Stats#1: no season with the key nowhere/Stats in the archive"));
            Assert.That(await Context.RecordedGames.CountAsync(), Is.Zero);
            await _dataPull.DidNotReceiveWithAnyArgs().GetAndSaveMatch(default);
        }
    }

    [Test]
    public async Task Import_ALateLeagueTitle_MakesNoSeason()
    {
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Championships = new ChampionsArchive { Champions = [new ArchiveChampionship { Season = "Late League Season 3", Tier = "Open", Team = "Queue", Roster = ["P1"] }] },
        };

        var report = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.EqualTo("Late League Season 3 Open champions: Late League is matchmaking and has no champion"));
            Assert.That(await Context.Seasons.CountAsync(), Is.Zero);
            Assert.That(report.SeasonsRetired, Is.Zero);
        }
    }

    [Test]
    public async Task Import_ATitleTheCatalogNamesOtherwise_GoesToTheSeasonThatSaysSo()
    {
        var entry = new ArchiveSeason
        {
            SeasonName = "Winter League 2019", GameVersion = "halo3", League = "AGLA", SourceWorkbook = "wl19", SourceSheet = "Stats", Titles = "WL19 Grand Finals",
            Players = [Player("Silva", team: "Rooster")],
        };
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [entry] },
            Championships = new ChampionsArchive { Champions = [new ArchiveChampionship { Season = "WL19 Grand Finals", Tier = "Open", Team = "Rooster", Roster = ["Silva"] }] },
        };

        var report = await _service.Import(input);

        var title = await Context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(title.Season.SeasonName, Is.EqualTo("Winter League 2019"));
            Assert.That(await Context.Seasons.CountAsync(), Is.EqualTo(1), "No titles-only season");
        }
    }

    // ---------- Pulling Waypoint matches ----------

    [Test]
    public async Task Import_AMatchThatCantBePulled_IsTriedAgain_ThenSkipped()
    {
        var matchId = new Guid("11111111-2222-3333-4444-555555555555");
        _dataPull.GetAndSaveMatch(matchId).Returns(Task.FromException(new HttpRequestException("API down")));
        var stages = new List<string>();

        var report = await _service.Import(WithWaypoint(InfiniteSeason(Player("Alpha One")), new WaypointSeasonMatches { Regular = [matchId.ToString()] }), p =>
        {
            stages.Add(p.Stage);
            return Task.CompletedTask;
        });

        using (Assert.EnterMultipleScope())
        {
            await _dataPull.Received(3).GetAndSaveMatch(matchId);
            Assert.That(report.Skipped, Has.Some.EqualTo($"Waypoint match {matchId}: could not be pulled (API down)"));
            Assert.That(stages.Count(s => s.StartsWith("Pulling Waypoint matches: retrying in")), Is.EqualTo(2), "A heartbeat before each retry");
            Assert.That(report.WaypointMatchesPulled, Is.Zero);
            Assert.That(report.MissingMatches, Has.Count.EqualTo(1), "Its phase keeps the sheet's totals");
        }
    }

    [Test]
    public async Task Import_AMatchPulledNow_IsCounted()
    {
        var matchId = new Guid("aaaaaaaa-2222-3333-4444-555555555555");
        _dataPull.GetAndSaveMatch(matchId).Returns(_ => AddMatch(matchId, (1, "Alpha One", Outcomes.Won), (2, "Beta One", Outcomes.Lost)));

        var report = await _service.Import(WithWaypoint(InfiniteSeason(Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta")),
            new WaypointSeasonMatches { Regular = [matchId.ToString()] }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.WaypointMatchesPulled, Is.EqualTo(1));
            Assert.That(report.MissingMatches, Is.Empty);
            Assert.That(report.GamesLinked, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_AValueThatIsNoMatchId_IsSkipped()
    {
        var report = await _service.Import(WithWaypoint(InfiniteSeason(Player("Alpha One")), new WaypointSeasonMatches { Regular = ["not-a-match"] }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.EqualTo("infinite-cup-2024/Stats: 'not-a-match' is not a match ID or Waypoint match URL"));
            await _dataPull.DidNotReceiveWithAnyArgs().GetAndSaveMatch(default);
        }
    }

    [Test]
    public async Task Import_AWaypointMatchOfThreeTeams_IsNotLinked()
    {
        var matchId = new Guid("bbbbbbbb-2222-3333-4444-555555555555");
        await AddMatch(matchId, (1, "Alpha One", Outcomes.Won), (2, "Beta One", Outcomes.Lost), (3, "Gamma One", Outcomes.Tie));

        var report = await _service.Import(WithWaypoint(InfiniteSeason(Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta"), Player("Gamma One", team: "Gamma")),
            new WaypointSeasonMatches { Regular = [matchId.ToString()] }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Has.Some.EqualTo($"infinite-cup-2024/Stats: Waypoint match {matchId} has more than two teams; not linked to the season"));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Import_Again_ALinkedWaypointGamesPlayers_StillReplaceTheirSheetRows()
    {
        var matchId = new Guid("cccccccc-2222-3333-4444-555555555555");
        await AddMatch(matchId, (1, "Alpha One", Outcomes.Won), (2, "Beta One", Outcomes.Lost));
        var input = WithWaypoint(InfiniteSeason(Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta")), new WaypointSeasonMatches { Regular = [matchId.ToString()] });
        await _service.Import(input);
        Context.ChangeTracker.Clear();

        var again = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.Errors, Is.Empty);
            Assert.That(again.ManualReplacedByWaypoint, Is.EqualTo(2));
            Assert.That((again.SeasonMatchesCreated, again.GamesLinked), Is.EqualTo((0, 0)), "Linked by the first run already");
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.PlayerSeasonStats.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Import_ASheetRowNoAccountMatched_IsWarnedOf_AndSuggestedForItsTeamsUnmatchedAccount()
    {
        var matchId = new Guid("dddddddd-2222-3333-4444-555555555555");
        // Alpha Newname played with Alpha One; the sheet knows him as Alpha Oldname
        await AddMatch(matchId, (1, "Alpha One", Outcomes.Won), (3, "Alpha Newname", Outcomes.Won), (2, "Beta One", Outcomes.Lost));

        var report = await _service.Import(WithWaypoint(
            InfiniteSeason(Player("Alpha One", team: "Alpha"), Player("Alpha Oldname", team: "Alpha"), Player("Beta One", team: "Beta")),
            new WaypointSeasonMatches { Regular = [matchId.ToString()] }));

        var claim = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        var oldname = await Context.Users.AsNoTracking().SingleAsync(u => u.UserName == "historical:Alpha Oldname");
        var alpha = await Context.Teams.AsNoTracking().SingleAsync(t => t.TeamName == "Alpha");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Has.Some.EqualTo("infinite-cup-2024/Stats regular season: Alpha Oldname matches no Xbox account of the season's matches"));
            Assert.That(report.SuggestionsCreated, Is.EqualTo(1));
            Assert.That((claim.Kind, claim.XboxUserID, claim.Gamertag, claim.PlaceholderUserID), Is.EqualTo((GamertagClaimKind.RosterMatch, (long?)3, "Alpha Oldname", (int?)oldname.Id)));
            Assert.That((claim.SeasonID, claim.TeamID), Is.EqualTo(((int?)alpha.SeasonID, (int?)alpha.TeamID)));
        }
    }

    // ---------- Players, rows and teams ----------

    [Test]
    public async Task Import_AGamertagTwoAccountsHistoriesHave_IsWarnedOf_AndLeftToItsPlaceholder()
    {
        Context.XboxUsers.AddRange(XboxUser.FromXbox(1, "First Now"), XboxUser.FromXbox(2, "Second Now"));
        Context.Set<XboxGamertagHistory>().AddRange(new XboxGamertagHistory(1, "Shared Tag"), new XboxGamertagHistory(2, "Shared Tag"));
        await Context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shared Tag"))));

        var placeholder = await Context.Users.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo("winter-league-2019/Stats: Shared Tag is in more than one Xbox account's gamertag history for the season; left to its placeholder"));
            Assert.That((placeholder.UserName, placeholder.XboxUserID), Is.EqualTo(("historical:Shared Tag", (long?)null)));
        }
    }

    [Test]
    public async Task Import_ASecondRowForTheSamePlayerAndStint_IsSkipped()
    {
        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva"), Player("silva")),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("Silva"), Player("Silva"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.EqualTo("winter-league-2019/Stats: silva is a second row for the same player, stint 1"));
            Assert.That(report.Skipped, Has.Some.EqualTo("winter-league-2019/Stats: Silva is a second row for the same player, stint 1 (playoffs)"));
            Assert.That(await Context.PlayerSeasonStats.CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_ATeamNoLongerInTheArchive_IsRemoved_AndItsRowsMoveToTheNewOne()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"))));
        Context.ChangeTracker.Clear();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Chickens"))));

        Context.ChangeTracker.Clear();
        var teams = await Context.Teams.AsNoTracking().ToListAsync();
        var row = await Context.PlayerSeasonStats.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.TeamsRemoved, Is.EqualTo(1));
            Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(ChickensOnly));
            Assert.That(row.TeamID, Is.EqualTo(teams[0].TeamID));
            Assert.That(report.SeasonRemovals.Single().TeamsRemoved, Is.EqualTo(1));
        }
    }

    private static readonly string[] ChickensOnly = ["Chickens"];

    [Test]
    public async Task Import_ASecondTitleForATier_AndATitleOfNoTeam_AreSkipped()
    {
        var input = new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Hen", team: "Hens"))).Seasons,
            Championships = new ChampionsArchive
            {
                Champions =
                [
                    new ArchiveChampionship { Season = "Winter League 2019", Tier = "Pro", Team = "Rooster", Roster = ["Silva"] },
                    new ArchiveChampionship { Season = "Winter League 2019", Tier = "Pro", Team = "Hens", Roster = ["Hen"] },
                    new ArchiveChampionship { Season = "Winter League 2019", Tier = "Amateur", Team = "Subs", Roster = ["Hen"] },
                ],
            },
        };

        var report = await _service.Import(input);

        var titles = await Context.Set<PlayerChampionship>().AsNoTracking().Select(c => new { c.Tier, c.GamertagUsed }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.EqualTo("winter-league-2019/Stats Pro champions: a second title for the tier (Hens)"));
            Assert.That(report.Skipped, Has.Some.EqualTo("winter-league-2019/Stats Amateur champions: no team named"));
            Assert.That(titles.Select(t => (t.Tier, t.GamertagUsed)), Is.EqualTo(ProSilvaOnly));
        }
    }

    private static readonly (string, string)[] ProSilvaOnly = [("Pro", "Silva")];

    // ---------- A titles-only season: adopted by its season, or retired ----------

    [Test]
    public async Task Import_ASeasonOfATitlesOnlySeasonsName_AdoptsIt()
    {
        var champions = new ChampionsArchive { Champions = [new ArchiveChampionship { Season = "Winter League 2011", Game = "Halo Reach", Tier = "Pro", Team = "Cabs", Roster = ["halfbreed105"] }] };
        await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive(), Championships = champions });
        var titlesOnly = await Context.Seasons.AsNoTracking().SingleAsync();
        Context.ChangeTracker.Clear();

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2011", "reach", "AGLA", false, Player("halfbreed105", team: "Cabs"))).Seasons,
            Championships = champions,
        });

        var season = await Context.Seasons.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((report.SeasonsCreated, report.SeasonsUpdated, report.SeasonsRetired), Is.EqualTo((0, 1, 0)));
            Assert.That(season.SeasonID, Is.EqualTo(titlesOnly.SeasonID), "The same season, now keyed by its sheet");
            Assert.That(titlesOnly.HistoricalSourceID, Is.EqualTo("history-agla-champions/Winter League 2011"));
            Assert.That(season.HistoricalSourceID, Is.EqualTo("winter-league-2011/Stats"));
            Assert.That(await Context.Set<PlayerChampionship>().CountAsync(), Is.EqualTo(1));
        }
    }

    private static ChampionsArchive LegacyAndRookieChampions() => new()
    {
        Champions =
        [
            new ArchiveChampionship { Season = "Winter League 2021", Game = "Halo 3", Tier = "Legacy", Team = "Wolves", Captain = "Alpha", Roster = ["Alpha"] },
            new ArchiveChampionship { Season = "Winter League 2021", Game = "Halo 3", Tier = "Rookie", Team = "Cubs", Captain = "Beta", Roster = ["Beta"] },
        ],
    };

    private static HistoricalArchiveInput LegacyAndRookieStats(ChampionsArchive champions) => new()
    {
        Seasons = Input(
            Entry("Winter League 2021 (Legacy)", "halo3", "AGLA", false, Player("Alpha", team: "Wolves")),
            Entry("Winter League 2021 (Rookie)", "halo3", "AGLA", false, Player("Beta", team: "Cubs"))).Seasons,
        Championships = champions,
    };

    /// <summary>The champions record alone: one titles-only season holding both tiers' titles.</summary>
    private async Task<Season> TitlesOnlySeason(ChampionsArchive champions)
    {
        await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive(), Championships = champions });
        Context.ChangeTracker.Clear();
        return await Context.Seasons.AsNoTracking().SingleAsync(s => s.SeasonName == "Winter League 2021");
    }

    [Test]
    public async Task Import_ATitlesOnlySeasonWhoseTitlesMoved_IsRetired_WithItsTeamsAndTitles()
    {
        var champions = LegacyAndRookieChampions();
        var titlesOnly = await TitlesOnlySeason(champions);

        var report = await _service.Import(LegacyAndRookieStats(champions));

        Context.ChangeTracker.Clear();
        var titles = await Context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.SeasonsRetired, Is.EqualTo(1));
            Assert.That(report.SeasonRemovals.Single(r => r.SeasonKey == titlesOnly.HistoricalSourceID).Summary, Is.EqualTo("0 of 0 stat rows, 2 of 2 teams, 2 of 2 titles"));
            Assert.That(report.ChampionshipsRemoved, Is.EqualTo(2));
            Assert.That(await Context.Seasons.AnyAsync(s => s.SeasonID == titlesOnly.SeasonID), Is.False);
            Assert.That(await Context.Teams.CountAsync(t => t.SeasonID == titlesOnly.SeasonID), Is.Zero);
            Assert.That(titles.Select(t => (t.GamertagUsed, t.Season.SeasonName)), Is.EquivalentTo(TitlesInTheirTiers));
        }
    }

    private static readonly (string, string)[] TitlesInTheirTiers = [("Alpha", "Winter League 2021 (Legacy)"), ("Beta", "Winter League 2021 (Rookie)")];

    [Test]
    public async Task Import_ATitlesOnlySeasonAVodPointsAt_IsKept_AndSaysWhy()
    {
        var champions = LegacyAndRookieChampions();
        var titlesOnly = await TitlesOnlySeason(champions);
        Context.Vods.Add(Vod.Seed("wl21video00", "WL21 Final", new DateTime(2021, 3, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.Confirmed, titlesOnly.SeasonID));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var report = await _service.Import(LegacyAndRookieStats(champions));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.SeasonsRetired, Is.Zero);
            Assert.That(report.Warnings, Has.Some.EqualTo($"{titlesOnly.HistoricalSourceID}: its titles are another season's now, but it has VODs; left for a sysadmin"));
            Assert.That(await Context.Seasons.AnyAsync(s => s.SeasonID == titlesOnly.SeasonID), Is.True);
        }
    }

    [Test]
    public async Task Run_TheGuardrail_StopsRetiringATitlesOnlySeason()
    {
        var champions = LegacyAndRookieChampions();
        var titlesOnly = await TitlesOnlySeason(champions);

        var report = await _service.Run(LegacyAndRookieStats(champions), new HistoricalImportRunOptions { Guardrail = new HistoricalGuardrailOptions() });

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SeasonsRetired, Is.Zero);
            Assert.That(report.SeasonRemovals.Single(r => r.SeasonKey == titlesOnly.HistoricalSourceID).Stopped, Is.True);
            Assert.That(report.Warnings, Has.Some.EqualTo($"{titlesOnly.HistoricalSourceID}: stopped for review, retiring it would remove 0 of 0 stat rows, 2 of 2 teams, 2 of 2 titles"));
            Assert.That(await Context.Seasons.AnyAsync(s => s.SeasonID == titlesOnly.SeasonID), Is.True);
            Assert.That(await Context.Set<PlayerChampionship>().CountAsync(c => c.SeasonID == titlesOnly.SeasonID), Is.EqualTo(2), "Rolled back");
        }
    }

    [Test]
    public async Task Run_ADryRun_ReportsRetiringATitlesOnlySeason_ButKeepsIt()
    {
        var champions = LegacyAndRookieChampions();
        var titlesOnly = await TitlesOnlySeason(champions);

        var report = await _service.Run(LegacyAndRookieStats(champions), new HistoricalImportRunOptions { DryRun = true });

        Context.ChangeTracker.Clear();
        var removal = report.SeasonRemovals.Single(r => r.SeasonKey == titlesOnly.HistoricalSourceID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SeasonsRetired, Is.Zero);
            Assert.That((removal.Stopped, removal.TitlesRemoved, removal.TeamsRemoved), Is.EqualTo((false, 2, 2)));
            Assert.That(await Context.Seasons.CountAsync(), Is.EqualTo(1), "Rolled back: neither tier's season was kept, nor the retirement");
        }
    }

    // ---------- Recorded games ----------

    [Test]
    public async Task Import_RecordedGames_OfAnUnknownGame_ListedTwice_OrAPulledWaypointMatch_AreSkipped()
    {
        var matchId = new Guid("eeeeeeee-2222-3333-4444-555555555555");
        await AddMatch(matchId, (1, "Waypoint One", Outcomes.Won), (2, "Waypoint Two", Outcomes.Lost));
        var input = CupWith(
            Game("cup#1", [("Alpha One", 1), ("Beta One", 2)], gameVersion: "nope"),
            Game("cup#2", [("Alpha One", 1), ("Beta One", 2)]),
            Game("CUP#2", [("Alpha One", 1), ("Beta One", 2)]),
            Game("cup#3", [("Alpha One", 1), ("Beta One", 2)], waypointMatchId: matchId.ToString()));

        var report = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Skipped, Has.Some.EqualTo("Recorded game cup#1: unknown game version 'nope'"));
            Assert.That(report.Skipped, Has.Some.EqualTo("Recorded game CUP#2: listed twice"));
            Assert.That(report.Warnings, Has.Some.EqualTo($"Recorded game cup#3: skipped, it is Waypoint match {matchId}"));
            Assert.That((report.RecordedGamesImported, report.RecordedGamesSkippedAsWaypoint), Is.EqualTo((1, 1)));
            Assert.That(await Context.RecordedGames.Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(Cup2Only));
        }
    }

    private static readonly string[] Cup2Only = ["cup#2"];

    [Test]
    public async Task Import_ARecordedGameBothOfWhoseSidesAreOneTeam_IsLinkedWithoutTeams()
    {
        var report = await _service.Import(CupWith(Game("cup#1", [("Alpha One", 1), ("Alpha Two", 2)])));

        var game = await Context.RecordedGames.AsNoTracking().Include(g => g.Teams).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Has.Some.EqualTo("Recorded game cup#1: both sides are Alpha; linked without teams"));
            Assert.That(game.Teams.Select(t => t.TeamID), Is.All.Null);
            Assert.That(report.RecordedGamesImported, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_RecordedGamesWithASideOfNoSeasonTeam_AreWarnedOfOnce()
    {
        var report = await _service.Import(CupWith(
            Game("cup#1", [("Gamma", 1), ("Delta", 2)]),
            Game("cup#2", [("Alpha One", 1), ("Epsilon", 2)])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings.Count(w => w.Contains("recorded game(s) with a side that is none of the season's teams")), Is.EqualTo(1));
            Assert.That(report.Warnings, Has.Some.EqualTo("mcc-cup-2016/Stats: 2 recorded game(s) with a side that is none of the season's teams (by its players' teams or its name): cup#1, cup#2"));
        }
    }

    [Test]
    public async Task Import_APlayoffRecordedGame_IsABracketMatch_SeededForItsTeamsFirstSeries()
    {
        var report = await _service.Import(CupWith(Game("cup#1", [("Alpha One", 1), ("Beta One", 2)], isPlayoffs: true)));

        var match = await Context.SeasonMatches.AsNoTracking().Include(m => m.BracketMatch).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((match.BracketMatch!.RoundNumber, match.BracketMatch.MatchNumber), Is.EqualTo((1, 1)));
            Assert.That((match.BracketMatch.HomeTeamSeedNumber, match.BracketMatch.AwayTeamSeedNumber), Is.EqualTo(((int?)1, (int?)2)));
            Assert.That((match.HomeTeamScore, match.AwayTeamScore), Is.EqualTo(((int?)1, (int?)0)));
        }
    }

    [Test]
    public async Task Import_ARecordedGameNoLongerInTheArchive_IsRemoved_WithItsSeries()
    {
        // cup#2's sides are no season team's: a game of the season in no series. It stays in the archive, so the season
        // still has recorded games (with none at all, HistoricalImportService leaves its earlier games alone).
        await _service.Import(CupWith(Game("cup#1", [("Alpha One", 1), ("Beta One", 2)]), Game("cup#2", [("Gamma", 1), ("Delta", 2)])));
        Context.ChangeTracker.Clear();
        Assert.That(await Context.SeasonMatches.CountAsync(), Is.EqualTo(1));

        var report = await _service.Import(CupWith(Game("cup#2", [("Gamma", 1), ("Delta", 2)])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.RecordedGamesRemoved, Is.EqualTo(1));
            Assert.That(await Context.RecordedGames.Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(Cup2Only));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero, "Its series had no other game");
        }
    }

    [Test]
    public async Task Import_NoRecordedGamesForTheSeason_LeavesItsEarlierGames()
    {
        await _service.Import(CupWith(Game("cup#1", [("Alpha One", 1), ("Beta One", 2)])));
        Context.ChangeTracker.Clear();

        var report = await _service.Import(CupWith());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecordedGamesRemoved, Is.Zero);
            Assert.That(await Context.RecordedGames.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_ARecordedGameThatIsAScrimNow_LeavesItsSeries()
    {
        await _service.Import(CupWith(Game("cup#1", [("Alpha One", 1), ("Beta One", 2)])));
        Context.ChangeTracker.Clear();

        var report = await _service.Import(CupWith(Game("cup#1", [("Alpha One", 1), ("Beta One", 2)], isScrim: true)));

        var game = await Context.RecordedGames.AsNoTracking().Include(g => g.SeriesGame).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.RecordedGamesImported, Is.EqualTo(1));
            Assert.That(game.SeriesGame, Is.Null, "Out of the series an earlier import put it in");
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero, "A scrim is in no series");
        }
    }
}
