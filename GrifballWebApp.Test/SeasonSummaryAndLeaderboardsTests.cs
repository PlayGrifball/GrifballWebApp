using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.EventOrganizer;
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
        _service = new SeasonService(_context);
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
            IsHistorical = historical,
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

        var stats = await new SeasonController(new EventOrganizerService(_context), _service).GetSeasonPlayerStats(season.SeasonID, CancellationToken.None);

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
        var late = await _context.Leagues.SingleAsync(l => l.Slug == "late-league");
        var old = await AddSeason("Summer League 2010", 2010, historical: true);
        var current = await AddSeason("Now", DateTime.UtcNow.Year, historical: false);
        var other = await AddSeason("Late League Season 8", 2012, historical: true);
        other.LeagueID = late.LeagueID;
        other.GameVersionID = 2;
        await _context.SaveChangesAsync();
        var controller = new SeasonController(new EventOrganizerService(_context), _service);
        async Task<int[]> Ids(SeasonListFilter filter) => (await controller.GetSeasons(filter, CancellationToken.None)).Results.Select(s => s.SeasonID).ToArray();

        Assert.Multiple(async () =>
        {
            Assert.That(await Ids(new SeasonListFilter { Ended = true }), Is.EqualTo(new[] { other.SeasonID, old.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { Ended = false }), Is.EqualTo(new[] { current.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { LeagueID = late.LeagueID }), Is.EqualTo(new[] { other.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { GameVersionID = 1 }), Is.EqualTo(new[] { current.SeasonID, old.SeasonID }));
            Assert.That(await Ids(new SeasonListFilter { SortColumn = "name", SortDirection = GrifballWebApp.Server.Dtos.SortDirection.Asc }), Is.EqualTo(new[] { other.SeasonID, current.SeasonID, old.SeasonID }));
            var page = await controller.GetSeasons(new SeasonListFilter { PageSize = 1, PageNumber = 2 }, CancellationToken.None);
            Assert.That((page.TotalCount, page.Results.Single().SeasonID), Is.EqualTo((3, other.SeasonID)));
            var currentItem = (await controller.GetSeasons(new SeasonListFilter { Ended = false }, CancellationToken.None)).Results.Single();
            Assert.That(currentItem.IsCurrent, Is.True);
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

        var boards = await new LeaderboardsController(_context).GetLeaderboards(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(boards.RingLeaders.First().UserID, Is.EqualTo(captain.Id));
            Assert.That(boards.RingLeaders.First().RingsCount, Is.EqualTo(2));
            Assert.That(boards.RingLeaders.First().Titles, Is.EqualTo(new[] { "Summer League 2010 (Pro)", "Summer League 2011 (Open)" }));
            Assert.That(boards.RingLeaders.Single(r => r.UserID == runner.Id).Gamertag, Is.EqualTo("Runner Now"), "Their current gamertag");
            var dynasty = boards.Dynasties.Single();
            Assert.That((dynasty.TeamName, dynasty.LatestTeamID, dynasty.LatestSeasonID, dynasty.ChampionshipsCount, dynasty.SeasonsActive),
                Is.EqualTo(("relentless", relentless.TeamID, later.SeasonID, 2, 2)), "One name across seasons, linked to its latest title");
            Assert.That(boards.GoalLeaders.Select(g => (g.UserID, g.StatValue)), Is.EqualTo(new[] { (captain.Id, 17), (benchy.Id, 7), (runner.Id, 5) }),
                "A combined row is not counted twice");
            Assert.That(boards.GoalLeaders[0].PerGame, Is.EqualTo(0.85m));
            Assert.That(boards.GoalLeaders[0].SeasonsCount, Is.EqualTo(2));
            Assert.That(boards.GamesPlayedLeaders.First().PerGame, Is.Null);
            Assert.That(boards.KillLeaders.First().SecondaryStat, Does.EndWith("K/D"));
            Assert.That(boards.CarrierStopLeaders.Select(c => c.UserID), Does.Contain(benchy.Id));
        });
    }
}
