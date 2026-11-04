using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Leaderboards;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// The season pages' summary and stats, the archive's catalog and the all-time leaderboards: one set of endpoints over
/// the app's tables, for seasons imported from the historical archive and played in the app alike.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonSummaryAndLeaderboardsTests
{
    private GrifballContext _context;
    private SeasonService _service;
    private const string Source = "https://docs.google.com/spreadsheets/d/test";
    private static long _nextXuid = 900_000;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SeasonService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<Season> AddSeason(string name, int year, bool historical, string? division = null)
    {
        var season = new Season
        {
            SeasonName = name,
            GameVersionID = 1,
            Division = division,
            HistoricalSourceID = historical ? "wb/" + name : null,
            HistoricalSourceUrl = historical ? Source : null,
            SignupsOpen = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SignupsClose = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DraftStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private async Task<User> AddUser(string gamertag, bool xbox = false)
    {
        var user = new User { UserName = "u-" + gamertag.Replace(" ", ""), DisplayName = gamertag };
        if (xbox)
            user.XboxUser = new XboxUser { XboxUserID = Interlocked.Increment(ref _nextXuid), Gamertag = gamertag + " Now" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Team> AddTeam(Season season, string name, int? wins, int? losses, params User[] players)
    {
        var team = new Team { SeasonID = season.SeasonID, TeamName = name, RecordedWins = wins, RecordedLosses = losses };
        foreach (var player in players)
            team.TeamPlayers.Add(new TeamPlayer { UserID = player.Id });
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private PlayerSeasonStat Stat(Season season, User user, Team? team, int goals, bool playoffs = false, bool combined = false, int stint = 1) => new()
    {
        SeasonID = season.SeasonID,
        UserID = user.Id,
        TeamID = team?.TeamID,
        GameVersionID = 1,
        Gamertag = user.DisplayName!,
        IsPlayoffs = playoffs,
        IsCombined = combined,
        StintIndex = stint,
        GamesPlayed = 10,
        Goals = goals,
        Kills = goals * 10,
        Deaths = 20,
        CarrierKills = goals,
    };

    /// <summary>Summer League 2010: two teams, Relentless the Pro champions (captained by Captain R), and their stats.</summary>
    private async Task<(Season Season, Team Champs, Team Bench, User Captain, User Runner, User Benchy)> SeedHistorical()
    {
        var season = await AddSeason("Summer League 2010", 2010, historical: true, division: "Pro");
        var captain = await AddUser("Captain R");
        var runner = await AddUser("Runner", xbox: true);
        var benchy = await AddUser("Benchy");
        var champs = await AddTeam(season, "Relentless", 9, 1, captain, runner);
        var bench = await AddTeam(season, "Benchwarmers", 1, 9, benchy);
        _context.PlayerSeasonStats.AddRange(Stat(season, captain, champs, 12), Stat(season, runner, champs, 3), Stat(season, benchy, bench, 7),
            Stat(season, runner, champs, 2, playoffs: true));
        await _context.SaveChangesAsync();
        await new ChampionshipService(_context).SetChampionsAsync(season.SeasonID, "Pro", champs.TeamID,
            [new ChampionMember(runner.Id, "Runner", false), new ChampionMember(captain.Id, "Captain R", true)], 1);
        _context.ChangeTracker.Clear();
        return (season, champs, bench, captain, runner, benchy);
    }

    [Test]
    public async Task SeasonSummary_OfAHistoricalSeason_HasItsSource_Champions_AndWhatItLacks()
    {
        var (season, champs, _, captain, runner, _) = await SeedHistorical();

        var summary = (await new SeasonController(new EventOrganizerService(_context), _service).GetSeasonSummary(season.SeasonID, CancellationToken.None)).Value!;

        Assert.Multiple(() =>
        {
            Assert.That((summary.SeasonName, summary.Year, summary.LeagueName, summary.GameVersionName, summary.Division),
                Is.EqualTo(("Summer League 2010", 2010, "AGLA", "Halo 3", "Pro")));
            Assert.That((summary.IsHistorical, summary.HistoricalSourceUrl), Is.EqualTo((true, Source)));
            Assert.That((summary.TeamCount, summary.SignupCount, summary.MatchCount, summary.BracketMatchCount, summary.PlayerStatCount), Is.EqualTo((2, 0, 0, 0, 4)));
            var champion = summary.Champions.Single();
            Assert.That((champion.Tier, champion.TeamID, champion.TeamName), Is.EqualTo(("Pro", champs.TeamID, "Relentless")));
            Assert.That(champion.Roster.Select(p => (p.UserID, p.Gamertag, p.IsCaptain)), Is.EqualTo(new[] { (captain.Id, "Captain R", true), (runner.Id, "Runner", false) }), "Captain first");
        });
    }

    [Test]
    public async Task SeasonSummary_NotFound()
    {
        var result = await new SeasonController(new EventOrganizerService(_context), _service).GetSeasonSummary(404, CancellationToken.None);
        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task SeasonPlayerStats_MostGoalsFirst_WithTheirTeams()
    {
        var (season, champs, bench, _, _, _) = await SeedHistorical();

        var stats = await new SeasonController(new EventOrganizerService(_context), _service).GetSeasonPlayerStats(season.SeasonID, null, CancellationToken.None);

        Assert.That(stats.Select(s => (s.Gamertag, s.Goals, s.TeamID, s.TeamName, s.IsPlayoffs, s.Source)), Is.EqualTo(new[]
        {
            ("Captain R", 12, (int?)champs.TeamID, "Relentless", false, "Recorded"),
            ("Benchy", 7, (int?)bench.TeamID, "Benchwarmers", false, "Recorded"),
            ("Runner", 3, (int?)champs.TeamID, "Relentless", false, "Recorded"),
            ("Runner", 2, (int?)champs.TeamID, "Relentless", true, "Recorded"),
        }));
    }

    [Test]
    public async Task SeasonList_EverySeasonStartedSoFar_HistoricalOrNot_LatestFirst()
    {
        var (historical, champs, _, _, _, _) = await SeedHistorical();
        var live = await AddSeason("Autumn Brawl", DateTime.UtcNow.Year - 1, historical: false);
        var player = await AddUser("Live Player");
        await AddTeam(live, "Live Team", null, null, player);
        var future = await AddSeason("Next Year", DateTime.UtcNow.Year + 1, historical: false);

        var catalog = (await new SeasonController(new EventOrganizerService(_context), _service).GetSeasons(new SeasonListFilter(), CancellationToken.None)).Results;

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Select(c => c.SeasonID), Is.EqualTo(new[] { live.SeasonID, historical.SeasonID }), "Not one that has not started");
            Assert.That(catalog.Select(c => (c.IsHistorical, c.HistoricalSourceUrl)), Is.EqualTo(new[] { (false, (string?)null), (true, Source) }));
            Assert.That(catalog.Select(c => (c.TeamCount, c.PlayerCount)), Is.EqualTo(new[] { (1, 1), (2, 3) }));
            Assert.That(catalog[1].Champions.Single().TeamID, Is.EqualTo(champs.TeamID));
            Assert.That(catalog[0].Champions, Is.Empty);
        });
    }

    [Test]
    public async Task SeasonList_FiltersByLeagueGameAndWhetherEnded_AndPages()
    {
        var gwl = await _context.Leagues.SingleAsync(l => l.Slug == "gwl");
        var old = await AddSeason("Summer League 2010", 2010, historical: true);
        var current = await AddSeason("Now", DateTime.UtcNow.Year, historical: false);
        var other = await AddSeason("GWL Season 8", 2012, historical: true);
        other.LeagueID = gwl.LeagueID;
        other.GameVersionID = 2;
        await _context.SaveChangesAsync();
        var controller = new SeasonController(new EventOrganizerService(_context), _service);
        async Task<int[]> Ids(SeasonListFilter filter) => (await controller.GetSeasons(filter, CancellationToken.None)).Results.Select(s => s.SeasonID).ToArray();

        Assert.Multiple(async () =>
        {
            Assert.That(await Ids(new SeasonListFilter { Ended = true }), Is.EqualTo(new[] { other.SeasonID, old.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { Ended = false }), Is.EqualTo(new[] { current.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { LeagueID = gwl.LeagueID }), Is.EqualTo(new[] { other.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { GameVersionID = 1 }), Is.EqualTo(new[] { current.SeasonID, old.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { SortColumn = "seasonName", SortDirection = GrifballWebApp.Server.Dtos.SortDirection.Asc }), Is.EqualTo(new[] { other.SeasonID, current.SeasonID, old.SeasonID }));
            var page = await controller.GetSeasons(new SeasonListFilter { PageSize = 1, PageNumber = 2 }, CancellationToken.None);
            Assert.That((page.TotalCount, page.Results.Single().SeasonID), Is.EqualTo((3, other.SeasonID)));
            var currentItem = (await controller.GetSeasons(new SeasonListFilter { Ended = false }, CancellationToken.None)).Results.Single();
            Assert.That(currentItem.HasEnded, Is.False);
        });
    }

    [Test]
    public async Task Leaderboards_TitlesAndCareerTotals_OverEverySeason()
    {
        var (first, _, _, captain, runner, benchy) = await SeedHistorical();
        // Relentless again, a later season (the app's own), won by a new roster
        var later = await AddSeason("Summer League 2011", 2011, historical: false);
        var newPlayer = await AddUser("Newcomer");
        var relentless = await AddTeam(later, "relentless", null, null, captain, newPlayer);
        _context.PlayerSeasonStats.AddRange(Stat(later, captain, relentless, 5), Stat(later, benchy, null, 30, combined: true));
        await _context.SaveChangesAsync();
        await new ChampionshipService(_context).SetChampionsAsync(later.SeasonID, "Open", relentless.TeamID, null, null);
        _context.ChangeTracker.Clear();

        var boards = await new LeaderboardsController(new GrifballWebApp.Server.Leaderboards.LeaderboardsService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context), new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))).GetLeaderboards(null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(boards.RingLeaders.First().UserID, Is.EqualTo(captain.Id));
            Assert.That(boards.RingLeaders.First().RingsCount, Is.EqualTo(2));
            Assert.That(boards.RingLeaders.First().Titles.Select(t => $"{t.SeasonName} ({t.Tier})"), Is.EqualTo(new[] { "Summer League 2010 (Pro)", "Summer League 2011 (Open)" }));
            Assert.That(boards.RingLeaders.Single(r => r.UserID == runner.Id).Gamertag, Is.EqualTo("Runner Now"), "Their current gamertag");
            var dynasty = boards.Dynasties.Single();
            Assert.That((dynasty.TeamName, dynasty.LatestTeamID, dynasty.LatestSeasonID, dynasty.ChampionshipsCount, dynasty.SeasonsActive),
                Is.EqualTo(("relentless", relentless.TeamID, later.SeasonID, 2, 2)), "One name across seasons, linked to its latest title");
            Assert.That(boards.GoalLeaders.Select(g => (g.UserID, g.StatValue)), Is.EqualTo(new[] { (benchy.Id, 37), (captain.Id, 17), (runner.Id, 5) }),
                "A combined row (a sheet with the regular season and playoffs together, the season's only row) counts");
            Assert.That(boards.GoalLeaders[1].PerGame, Is.EqualTo(0.85m));
            Assert.That(boards.GoalLeaders[1].SeasonsCount, Is.EqualTo(2));
            Assert.That(boards.GamesPlayedLeaders.First().PerGame, Is.Null);
            Assert.That(boards.KillLeaders.First().SecondaryStat, Does.EndWith("K/D"));
            Assert.That(boards.CarrierStopLeaders.Select(c => c.UserID), Does.Contain(benchy.Id));
        });
    }

    [Test]
    public async Task SeasonList_AndSummary_CarryTheFormat_AndFilterByIt()
    {
        var league = await AddSeason("League 2015", 2015, historical: false);
        var respawn = await AddSeason("Respawn 2016", 2016, historical: false);
        respawn.Format = SeasonFormat.Tournament;
        await _context.SaveChangesAsync();
        var controller = new SeasonController(new EventOrganizerService(_context), _service);
        async Task<int[]> Ids(SeasonFormat? format) => (await controller.GetSeasons(new SeasonListFilter { Format = format }, CancellationToken.None)).Results.Select(s => s.SeasonID).ToArray();

        var all = (await controller.GetSeasons(new SeasonListFilter(), CancellationToken.None)).Results;
        var summary = (await controller.GetSeasonSummary(respawn.SeasonID, CancellationToken.None)).Value!;

        Assert.Multiple(async () =>
        {
            Assert.That(all.Select(s => (s.SeasonID, s.Format)), Is.EqualTo(new[]
            {
                (respawn.SeasonID, SeasonFormat.Tournament), (league.SeasonID, SeasonFormat.LeagueSeason),
            }));
            Assert.That(await Ids(SeasonFormat.Tournament), Is.EqualTo(new[] { respawn.SeasonID }));
            Assert.That(await Ids(SeasonFormat.LeagueSeason), Is.EqualTo(new[] { league.SeasonID }));
            Assert.That(await Ids(null), Has.Length.EqualTo(2));
            Assert.That(summary.Format, Is.EqualTo(SeasonFormat.Tournament));
        });
    }

    [Test]
    public async Task Leaderboards_TitleCounts_FilterByFormat()
    {
        var (_, _, _, captain, _, _) = await SeedHistorical();
        var respawn = await AddSeason("Respawn 2012", 2012, historical: false);
        respawn.Format = SeasonFormat.Tournament;
        await _context.SaveChangesAsync();
        var winners = await AddTeam(respawn, "Respawners", null, null, captain);
        await new ChampionshipService(_context).SetChampionsAsync(respawn.SeasonID, "Open", winners.TeamID, null, null);
        _context.ChangeTracker.Clear();
        var controller = new LeaderboardsController(new GrifballWebApp.Server.Leaderboards.LeaderboardsService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context), new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));

        var every = await controller.GetLeaderboards(null, CancellationToken.None);
        var tournaments = await controller.GetLeaderboards(SeasonFormat.Tournament, CancellationToken.None);
        var leagues = await controller.GetLeaderboards(SeasonFormat.LeagueSeason, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(every.RingLeaders.Single(r => r.UserID == captain.Id).RingsCount, Is.EqualTo(2), "Pro 2010 and Respawn");
            Assert.That(tournaments.RingLeaders.Select(r => (r.UserID, r.RingsCount)), Is.EqualTo(new[] { (captain.Id, 1) }));
            Assert.That(tournaments.Dynasties.Select(d => d.TeamName), Is.EqualTo(new[] { "Respawners" }));
            Assert.That(leagues.RingLeaders.Single(r => r.UserID == captain.Id).Titles.Select(t => $"{t.SeasonName} ({t.Tier})"), Is.EqualTo(new[] { "Summer League 2010 (Pro)" }));
        });
    }

    [Test]
    public async Task Leaderboards_GoalsArePerGame_KillsAndGamesAcrossGames()
    {
        var player = await AddUser("Two Games");
        var halo3 = await AddSeason("Halo 3 2010", 2010, historical: true);
        var reach = await AddSeason("Reach 2011", 2011, historical: true);
        reach.GameVersionID = 2;
        var reachStat = Stat(reach, player, null, 4);
        reachStat.GameVersionID = 2;
        _context.PlayerSeasonStats.AddRange(Stat(halo3, player, null, 6), reachStat);
        await _context.SaveChangesAsync();

        var boards = await new LeaderboardsController(new GrifballWebApp.Server.Leaderboards.LeaderboardsService(_context, new GrifballWebApp.Server.Stats.SeasonStatsService(_context), new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))).GetLeaderboards(null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(boards.GoalLeaders.Select(g => (g.StatValue, g.GameVersionName)), Is.EqualTo(new[] { (6, (string?)"Halo 3"), (4, (string?)"Halo Reach") }));
            Assert.That(boards.KillLeaders.Select(k => (k.StatValue, k.GameVersionName)), Is.EqualTo(new[] { (100, (string?)null) }), "60 + 40 kills, one career");
            Assert.That(boards.GamesPlayedLeaders.Single().StatValue, Is.EqualTo(20));
        });
    }

    private LeaderboardsController NewLeaderboardsController() =>
        new(new GrifballWebApp.Server.Leaderboards.LeaderboardsService(
            _context,
            new GrifballWebApp.Server.Stats.SeasonStatsService(_context),
            new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));

    [Test]
    public async Task Leaderboards_AssignRanks_WithTies_StandardCompetitionRanking()
    {
        var season = await AddSeason("Winter League 2011", 2011, historical: true);
        var p1 = await AddUser("Player One");
        var p2 = await AddUser("Player Two");
        var p3 = await AddUser("Player Three");

        // p1 and p2 tied with 10 goals, p3 has 5 goals
        _context.PlayerSeasonStats.AddRange(
            Stat(season, p1, null, 10),
            Stat(season, p2, null, 10),
            Stat(season, p3, null, 5)
        );
        await _context.SaveChangesAsync();

        var controller = NewLeaderboardsController();
        var boards = await controller.GetLeaderboards(null, CancellationToken.None);

        var halo3Goals = boards.GoalLeaders.Where(g => g.GameVersionName == "Halo 3").ToList();
        Assert.Multiple(() =>
        {
            Assert.That(halo3Goals.Count, Is.EqualTo(3));
            Assert.That(halo3Goals[0].Rank, Is.EqualTo(1));
            Assert.That(halo3Goals[1].Rank, Is.EqualTo(1), "Tied for 1st place");
            Assert.That(halo3Goals[2].Rank, Is.EqualTo(3), "Standard competition ranking skips to 3rd");
        });
    }

    [Test]
    public async Task Leaderboards_GetPlayerRank_RanksAmongThoseOnTheBoard_AndLeavesZeroUnranked()
    {
        var season = await AddSeason("Fall League 2012", 2012, historical: true);
        var star = await AddUser("Star Player");
        var active = await AddUser("Active Player");
        var zeroUser = await AddUser("Zero User");

        _context.PlayerSeasonStats.AddRange(
            Stat(season, star, null, 15),
            Stat(season, active, null, 5)
        );
        await _context.SaveChangesAsync();

        var team = await AddTeam(season, "Champions", 10, 0, star);
        await new ChampionshipService(_context).SetChampionsAsync(season.SeasonID, "Pro", team.TeamID, null, null);

        var controller = NewLeaderboardsController();

        var starRank = await controller.GetPlayerRank("Star Player", null, null, CancellationToken.None);
        Assert.That(starRank, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(starRank!.Gamertag, Is.EqualTo("Star Player"));
            Assert.That(starRank.Goals.TotalRanked, Is.EqualTo(2), "The players with a goal, not every user");
            Assert.That(starRank.Rings.TotalRanked, Is.EqualTo(1), "The players with a title");
            Assert.That(starRank.Kills.TotalRanked, Is.EqualTo(2));
            Assert.That(starRank.Goals.SecondaryText, Is.EqualTo("1.5 / G in Halo 3"));
            Assert.That(starRank.Rings.StatValue, Is.EqualTo(1));
            Assert.That(starRank.Rings.Rank, Is.EqualTo(1));
            Assert.That(starRank.Goals.StatValue, Is.EqualTo(15));
            Assert.That(starRank.Goals.Rank, Is.EqualTo(1));
            Assert.That(starRank.Kills.StatValue, Is.EqualTo(150));
            Assert.That(starRank.Kills.Rank, Is.EqualTo(1));
        });

        // Zero User has no stats and no title: on no board, so unranked, of the 2 on the goals board
        var zeroRank = await controller.GetPlayerRank("Zero User", null, null, CancellationToken.None);
        Assert.That(zeroRank, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(zeroRank!.Gamertag, Is.EqualTo("Zero User"));
            Assert.That(zeroRank.Goals.TotalRanked, Is.EqualTo(2));
            Assert.That(zeroRank.Goals.StatValue, Is.EqualTo(0));
            Assert.That(zeroRank.Goals.Rank, Is.Null, "Not on the board");
            Assert.That(zeroRank.Goals.SecondaryText, Is.Null);
            Assert.That(zeroRank.Rings.StatValue, Is.EqualTo(0));
            Assert.That((zeroRank.Rings.Rank, zeroRank.Rings.TotalRanked), Is.EqualTo(((int?)null, 1)));
            Assert.That((zeroRank.Kills.Rank, zeroRank.GamesPlayed.Rank, zeroRank.CarrierStops.Rank), Is.EqualTo(((int?)null, (int?)null, (int?)null)));
            Assert.That(zeroRank.GamesPlayed.SecondaryText, Is.Null);
        });
    }

    [Test]
    public async Task Leaderboards_GetPlayerRank_AndSearch_MatchesPastAliases()
    {
        var season = await AddSeason("Spring League 2013", 2013, historical: true);
        var user = await AddUser("CurrentTag", xbox: true);

        // Add past alias in XboxGamertagHistory
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory
        {
            XboxUserID = user.XboxUser!.XboxUserID,
            Gamertag = "OldLegend",
            NormalizedGamertag = "OLDLEGEND",
            Source = GamertagHistorySource.Manual,
        });

        _context.PlayerSeasonStats.Add(new PlayerSeasonStat
        {
            SeasonID = season.SeasonID,
            UserID = user.Id,
            GameVersionID = 1,
            Gamertag = "OldLegend",
            GamesPlayed = 12,
            Goals = 8,
            Kills = 40,
            Deaths = 20,
        });
        await _context.SaveChangesAsync();

        var controller = NewLeaderboardsController();

        // Autocomplete search
        var searchResults = await controller.SearchPlayers("OldLeg", CancellationToken.None);
        Assert.That(searchResults.Count, Is.GreaterThanOrEqualTo(1));
        var match = searchResults.First(r => r.UserID == user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(match.Gamertag, Is.EqualTo("CurrentTag Now"));
            Assert.That(match.MatchedAlias, Is.EqualTo("OldLegend"));
        });

        // PlayerRank lookup by alias
        var rank = await controller.GetPlayerRank("OldLegend", null, null, CancellationToken.None);
        Assert.That(rank, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(rank!.Gamertag, Is.EqualTo("CurrentTag Now"));
            Assert.That(rank.MatchedAlias, Is.EqualTo("OldLegend"));
            Assert.That(rank.Goals.StatValue, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task Leaderboards_GetPlayerRank_WithNumericGamertag_DoesNotTreatAsUserId()
    {
        var season = await AddSeason("Numeric Season 2014", 2014, historical: true);
        var player1337 = await AddUser("1337", xbox: true);

        _context.PlayerSeasonStats.Add(new PlayerSeasonStat
        {
            SeasonID = season.SeasonID,
            UserID = player1337.Id,
            GameVersionID = 1,
            Gamertag = "1337",
            GamesPlayed = 10,
            Goals = 15,
            Kills = 25,
            Deaths = 10,
        });
        await _context.SaveChangesAsync();

        var controller = NewLeaderboardsController();

        // 1. Querying for string "1337" matches player whose gamertag is "1337", even if their User.Id != 1337
        var rankByQuery = await controller.GetPlayerRank("1337", null, null, CancellationToken.None);
        Assert.That(rankByQuery, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(rankByQuery!.UserID, Is.EqualTo(player1337.Id));
            Assert.That(rankByQuery.Goals.StatValue, Is.EqualTo(15));
        });

        // 2. Querying with explicit userId matches that exact user
        var rankByUserId = await controller.GetPlayerRank(null, player1337.Id, null, CancellationToken.None);
        Assert.That(rankByUserId, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(rankByUserId!.UserID, Is.EqualTo(player1337.Id));
            Assert.That(rankByUserId.Goals.StatValue, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task Leaderboards_GetPlayerRank_RanksGoalsAndStopsByBestGame_AsTheBoardsDo()
    {
        var halo3 = await AddSeason("Halo 3 2010", 2010, historical: true);
        var reach = await AddSeason("Reach 2011", 2011, historical: true);
        reach.GameVersionID = 2;
        var twoGames = await AddUser("Two Games");
        var oneGame = await AddUser("One Game");
        PlayerSeasonStat RStat(User user, int goals) { var s = Stat(reach, user, null, goals); s.GameVersionID = 2; return s; }
        _context.PlayerSeasonStats.AddRange(Stat(halo3, twoGames, null, 6), RStat(twoGames, 4), Stat(halo3, oneGame, null, 8));
        await _context.SaveChangesAsync();
        var controller = NewLeaderboardsController();

        var boards = await controller.GetLeaderboards(null, CancellationToken.None);
        var rank = (await controller.GetPlayerRank(null, twoGames.Id, null, CancellationToken.None))!;

        Assert.Multiple(() =>
        {
            var row = boards.GoalLeaders.Single(g => g.UserID == twoGames.Id && g.GameVersionName == "Halo 3");
            Assert.That((rank.Goals.StatValue, rank.Goals.Rank, rank.Goals.TotalRanked), Is.EqualTo((6L, (int?)row.Rank, boards.GoalLeaders.Count)),
                "Their best game (6 in Halo 3, not 6 + 4), ranked as its row on the board, of every player's game on it");
            Assert.That(rank.Goals.Rank, Is.EqualTo(2));
            Assert.That((rank.Goals.PerGame, rank.Goals.SecondaryText), Is.EqualTo(((decimal?)0.6m, (string?)"0.6 / G in Halo 3")));
            var stops = boards.CarrierStopLeaders.Single(g => g.UserID == twoGames.Id && g.GameVersionName == "Halo 3");
            Assert.That((rank.CarrierStops.StatValue, rank.CarrierStops.Rank, rank.CarrierStops.PerGame), Is.EqualTo((6L, (int?)stops.Rank, stops.PerGame)));
            Assert.That((rank.Kills.StatValue, rank.Kills.Rank, rank.Kills.PerGame), Is.EqualTo((100L, (int?)1, (decimal?)5m)), "Kills across games: 60 + 40");
            Assert.That((rank.GamesPlayed.StatValue, rank.GamesPlayed.Rank, rank.GamesPlayed.SecondaryText), Is.EqualTo((20L, (int?)1, (string?)"2 Seasons")));
        });
    }

    [Test]
    public async Task Leaderboards_GetPlayerRank_CountsTitlesOfTheFormat_AmongTheirHolders()
    {
        var (_, _, _, captain, runner, _) = await SeedHistorical();
        var respawn = await AddSeason("Respawn 2012", 2012, historical: false);
        respawn.Format = SeasonFormat.Tournament;
        await _context.SaveChangesAsync();
        var winners = await AddTeam(respawn, "Respawners", null, null, captain);
        await new ChampionshipService(_context).SetChampionsAsync(respawn.SeasonID, "Open", winners.TeamID, null, null);
        _context.ChangeTracker.Clear();
        var controller = NewLeaderboardsController();

        var every = (await controller.GetPlayerRank(null, captain.Id, null, CancellationToken.None))!.Rings;
        var tournaments = (await controller.GetPlayerRank(null, captain.Id, SeasonFormat.Tournament, CancellationToken.None))!.Rings;
        var runnerTournaments = (await controller.GetPlayerRank(null, runner.Id, SeasonFormat.Tournament, CancellationToken.None))!.Rings;

        Assert.Multiple(() =>
        {
            Assert.That((every.StatValue, every.Rank, every.TotalRanked, every.SecondaryText), Is.EqualTo((2L, (int?)1, 2, (string?)"2 Titles")));
            Assert.That((tournaments.StatValue, tournaments.Rank, tournaments.TotalRanked), Is.EqualTo((1L, (int?)1, 1)), "Only the tournament's title holder");
            Assert.That((runnerTournaments.StatValue, runnerTournaments.Rank), Is.EqualTo((0L, (int?)null)));
        });
    }

    [Test]
    public async Task Leaderboards_GetPlayerRank_FindsTheNameOnAStatLine_OrATitle_OrBySearch()
    {
        var season = await AddSeason("Alias League 2015", 2015, historical: true);
        var sheet = await AddUser("Real Name");
        var titled = await AddUser("Champ Now");
        var fuzzy = await AddUser("Partial Match Guy");
        var line = Stat(season, sheet, null, 3);
        line.Gamertag = "Sheet Name";
        _context.PlayerSeasonStats.Add(line);
        var team = await AddTeam(season, "Winners", 1, 0, titled);
        await _context.SaveChangesAsync();
        await new ChampionshipService(_context).SetChampionsAsync(season.SeasonID, "Pro", team.TeamID, [new ChampionMember(titled.Id, "Old Champ Tag", true)], 1);
        _context.ChangeTracker.Clear();
        var controller = NewLeaderboardsController();

        var bySheet = await controller.GetPlayerRank("Sheet Name", null, null, CancellationToken.None);
        var byTitle = await controller.GetPlayerRank("Old Champ Tag", null, null, CancellationToken.None);
        var bySearch = await controller.GetPlayerRank("Partial Match", null, null, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That((bySheet!.UserID, bySheet.Gamertag, bySheet.MatchedAlias), Is.EqualTo((sheet.Id, "Real Name", (string?)"Sheet Name")));
            Assert.That((byTitle!.UserID, byTitle.MatchedAlias, byTitle.Rings.StatValue), Is.EqualTo((titled.Id, (string?)"Old Champ Tag", 1L)));
            Assert.That((bySearch!.UserID, bySearch.MatchedAlias), Is.EqualTo((fuzzy.Id, (string?)null)));
            Assert.That(bySearch.PastGamertags, Is.Empty);
            Assert.That(await controller.GetPlayerRank("Nobody At All", null, null, CancellationToken.None), Is.Null);
            Assert.That(await controller.GetPlayerRank("  ", null, null, CancellationToken.None), Is.Null);
            Assert.That(await controller.GetPlayerRank(null, 987_654, null, CancellationToken.None), Is.Null, "A user ID that doesn't exist");
        });
    }

    [Test]
    public async Task Leaderboards_SearchAndRank_MatchAnArchivedPlayersGamertag_NotTheirPlaceholderPrefix()
    {
        var archived = new User { UserName = HistoricalKeys.DummyUserName("Zed"), IsDummyUser = true };
        var plain = new User { UserName = "plainname" };
        _context.Users.AddRange(archived, plain);
        await _context.SaveChangesAsync();
        var controller = NewLeaderboardsController();

        Assert.Multiple(async () =>
        {
            Assert.That(await controller.SearchPlayers("is", CancellationToken.None), Is.Empty, "Not \"historical:\"");
            Assert.That(await controller.SearchPlayers("al:", CancellationToken.None), Is.Empty);
            Assert.That(await controller.GetPlayerRank("ical", null, null, CancellationToken.None), Is.Null);
            Assert.That((await controller.SearchPlayers("Ze", CancellationToken.None)).Select(r => (r.UserID, r.Gamertag)), Is.EqualTo(new[] { (archived.Id, "Zed") }));
            Assert.That((await controller.SearchPlayers("ainna", CancellationToken.None)).Select(r => r.UserID), Is.EqualTo(new[] { plain.Id }));
            Assert.That(await controller.SearchPlayers("Z", CancellationToken.None), Is.Empty, "Two characters at least");
            var rank = await controller.GetPlayerRank("Zed", null, null, CancellationToken.None);
            Assert.That((rank!.UserID, rank.Gamertag, rank.IsDummyUser, rank.MatchedAlias), Is.EqualTo((archived.Id, "Zed", true, (string?)null)));
        });
    }

    [Test]
    public async Task Leaderboards_Search_FindsANameOnlyOnAStatLine_AsAnAlias()
    {
        var season = await AddSeason("Stats Only 2016", 2016, historical: true);
        var renamed = await AddUser("Someone Else");
        var same = await AddUser("Quartz Tag");
        var line = Stat(season, renamed, null, 2);
        line.Gamertag = "Quartz Sheet";
        _context.PlayerSeasonStats.Add(line);
        var sameLine = Stat(season, same, null, 1);
        sameLine.Gamertag = "Quartz Tag";
        _context.PlayerSeasonStats.Add(sameLine);
        await _context.SaveChangesAsync();
        var controller = NewLeaderboardsController();

        var results = await controller.SearchPlayers("Quartz", CancellationToken.None);

        Assert.That(results.Select(r => (r.UserID, r.Gamertag, r.MatchedAlias)), Is.EquivalentTo(new[]
        {
            (same.Id, "Quartz Tag", (string?)null),
            (renamed.Id, "Someone Else", (string?)"Quartz Sheet"),
        }), "The user named so first, then the stat line's player under their own name");
    }

    [Test]
    public async Task Leaderboards_Search_ShowsTen_FromNamesFirst_ThenPastGamertags()
    {
        for (var i = 0; i < 11; i++)
            await AddUser($"Clone {i}");
        for (var i = 0; i < 11; i++)
        {
            var user = await AddUser($"Ghost {i}", xbox: true);
            _context.XboxGamertagHistory.Add(new XboxGamertagHistory
            {
                XboxUserID = user.XboxUser!.XboxUserID,
                Gamertag = $"Spectre {i}",
                NormalizedGamertag = $"SPECTRE {i}",
                Source = GamertagHistorySource.Manual,
            });
        }
        await _context.SaveChangesAsync();
        var controller = NewLeaderboardsController();

        var clones = await controller.SearchPlayers("Clone", CancellationToken.None);
        var spectres = await controller.SearchPlayers("Spectre", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(clones, Has.Count.EqualTo(10));
            Assert.That(spectres, Has.Count.EqualTo(10));
            Assert.That(spectres.All(r => r.MatchedAlias!.StartsWith("Spectre") && r.Gamertag.StartsWith("Ghost")), Is.True);
        });
    }
}
