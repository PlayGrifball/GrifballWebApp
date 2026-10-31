using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalControllerTests
{
    private GrifballContext _context;
    private HistoricalController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new HistoricalController(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task Season_ReturnsNotFound_WhenSeasonDoesNotExist()
    {
        var result = await _controller.Season(999, CancellationToken.None);
        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Season_ReturnsCompleteHistoricalSeasonData_WithChampionsTeamsAndStats()
    {
        var league = await _context.Leagues.SingleAsync(l => l.Slug == "agla");
        var h3 = await _context.GameVersions.SingleAsync(g => g.Slug == "halo3");

        var season = new Season
        {
            SeasonName = "Summer League 2010 (Ammy)",
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            IsHistorical = true,
            HistoricalSourceID = "2010-sml10/Stats",
            HistoricalSourceUrl = "https://docs.google.com/spreadsheets/d/test",
            SeasonStart = new DateTime(2010, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2010, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var player1 = new User { UserName = "captain1", DisplayName = "Captain Relentless" };
        var player2 = new User { UserName = "player2", DisplayName = "Runner Relentless" };
        await _context.Users.AddRangeAsync(player1, player2);
        await _context.SaveChangesAsync();

        await _context.PlayerChampionships.AddRangeAsync(
            new PlayerChampionship
            {
                UserID = player1.Id,
                SeasonID = season.SeasonID,
                LeagueID = league.LeagueID,
                GameVersionID = h3.GameVesionID,
                Tier = "Amateur",
                TeamName = "Relentless",
                IsCaptain = true,
                GamertagUsed = "Captain Relentless"
            },
            new PlayerChampionship
            {
                UserID = player2.Id,
                SeasonID = season.SeasonID,
                LeagueID = league.LeagueID,
                GameVersionID = h3.GameVesionID,
                Tier = "Amateur",
                TeamName = "Relentless",
                IsCaptain = false,
                GamertagUsed = "Runner Relentless"
            }
        );

        await _context.PlayerSeasonStats.AddRangeAsync(
            new PlayerSeasonStat
            {
                UserID = player1.Id,
                SeasonID = season.SeasonID,
                GameVersionID = h3.GameVesionID,
                Gamertag = "Captain Relentless",
                TeamName = "Relentless",
                GamesPlayed = 12,
                Wins = 10,
                Losses = 2,
                Goals = 25,
                Kills = 150,
                Deaths = 75,
                CarrierKills = 35
            },
            new PlayerSeasonStat
            {
                UserID = player2.Id,
                SeasonID = season.SeasonID,
                GameVersionID = h3.GameVesionID,
                Gamertag = "Runner Relentless",
                TeamName = "Relentless",
                GamesPlayed = 12,
                Wins = 10,
                Losses = 2,
                Goals = 15,
                Kills = 110,
                Deaths = 80,
                CarrierKills = 20
            }
        );
        await _context.SaveChangesAsync();

        var actionResult = await _controller.Season(season.SeasonID, CancellationToken.None);
        var dto = actionResult.Value;

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(dto.SeasonName, Is.EqualTo("Summer League 2010 (Ammy)"));
            Assert.That(dto.Year, Is.EqualTo(2010));
            Assert.That(dto.LeagueName, Is.EqualTo("AGLA"));
            Assert.That(dto.GameVersionName, Is.EqualTo("Halo 3"));
            Assert.That(dto.HistoricalSourceUrl, Is.EqualTo("https://docs.google.com/spreadsheets/d/test"));

            Assert.That(dto.Champions, Has.Count.EqualTo(1));
            Assert.That(dto.Champions[0].TeamName, Is.EqualTo("Relentless"));
            Assert.That(dto.Champions[0].TeamSlug, Is.EqualTo("relentless"));
            Assert.That(dto.Champions[0].Roster, Has.Count.EqualTo(2));

            Assert.That(dto.Teams, Has.Count.EqualTo(1));
            Assert.That(dto.Teams[0].TeamName, Is.EqualTo("Relentless"));
            Assert.That(dto.Teams[0].TeamSlug, Is.EqualTo("relentless"));
            Assert.That(dto.Teams[0].WonChampionship, Is.True);
            Assert.That(dto.Teams[0].Goals, Is.EqualTo(40));
            Assert.That(dto.Teams[0].Roster, Has.Count.EqualTo(2));

            Assert.That(dto.PlayerStats, Has.Count.EqualTo(2));
            Assert.That(dto.PlayerStats[0].Gamertag, Is.EqualTo("Captain Relentless"));
            Assert.That(dto.PlayerStats[0].Goals, Is.EqualTo(25));
        });
    }

    [Test]
    public async Task Team_ReturnsNotFound_WhenTeamDoesNotExist()
    {
        var result = await _controller.Team("non-existent-team", CancellationToken.None);
        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Team_ReturnsCanonicalNameAndAppearances_MatchingSlugs()
    {
        var league = await _context.Leagues.SingleAsync(l => l.Slug == "agla");
        var h3 = await _context.GameVersions.SingleAsync(g => g.Slug == "halo3");

        var season1 = new Season
        {
            SeasonName = "Summer League 2014 (H4)",
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            IsHistorical = true,
            HistoricalSourceID = "2014-sml14/Stats",
            SeasonStart = new DateTime(2014, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2014, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var season2 = new Season
        {
            SeasonName = "Summer League 2017 (H5)",
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            IsHistorical = true,
            HistoricalSourceID = "2017-sml17/Stats",
            SeasonStart = new DateTime(2017, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2017, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        var player = new User { UserName = "benchwarmer", DisplayName = "Benchwarmer" };
        await _context.Users.AddAsync(player);
        await _context.SaveChangesAsync();

        // Season 1: Team name with spaces
        await _context.PlayerSeasonStats.AddAsync(new PlayerSeasonStat
        {
            UserID = player.Id,
            SeasonID = season1.SeasonID,
            GameVersionID = h3.GameVesionID,
            Gamertag = "Benchwarmer",
            TeamName = "B E N C H W A R M E R S",
            GamesPlayed = 10,
            Goals = 12
        });

        // Season 2: Championship with regular team name
        await _context.PlayerChampionships.AddAsync(new PlayerChampionship
        {
            UserID = player.Id,
            SeasonID = season2.SeasonID,
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            Tier = "Amateur",
            TeamName = "Benchwarmers",
            GamertagUsed = "Benchwarmer"
        });
        await _context.SaveChangesAsync();

        var actionResult = await _controller.Team("b-e-n-c-h-w-a-r-m-e-r-s", CancellationToken.None);
        var dto = actionResult.Value;

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.TeamSlug, Is.EqualTo("b-e-n-c-h-w-a-r-m-e-r-s"));
            Assert.That(dto.TotalChampionships, Is.EqualTo(1));
            Assert.That(dto.Championships, Has.Count.EqualTo(1));
            Assert.That(dto.Championships[0].SeasonName, Is.EqualTo("Summer League 2017 (H5)"));
            Assert.That(dto.Seasons, Has.Count.EqualTo(1));
            Assert.That(dto.Seasons[0].SeasonName, Is.EqualTo("Summer League 2014 (H4)"));
            Assert.That(dto.AllTimePlayers, Has.Count.EqualTo(1));
            Assert.That(dto.AllTimePlayers[0].Gamertag, Is.EqualTo("Benchwarmer"));
            Assert.That(dto.AllTimePlayers[0].ChampionshipsWon, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Catalog_ReturnsHistoricalSeasons_WithSummaryCountsAndChampions()
    {
        var league = await _context.Leagues.SingleAsync(l => l.Slug == "agla");
        var h3 = await _context.GameVersions.SingleAsync(g => g.Slug == "halo3");

        var season = new Season
        {
            SeasonName = "Summer League 2011",
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            IsHistorical = true,
            SeasonStart = new DateTime(2011, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2011, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var player = new User { UserName = "catalogplayer", DisplayName = "Catalog Player" };
        await _context.Users.AddAsync(player);
        await _context.SaveChangesAsync();

        await _context.PlayerSeasonStats.AddAsync(new PlayerSeasonStat
        {
            SeasonID = season.SeasonID,
            GameVersionID = h3.GameVesionID,
            UserID = player.Id,
            Gamertag = "CatalogChamp",
            TeamName = "Catalog Stars",
            GamesPlayed = 10,
            Goals = 15,
            Kills = 40,
            Deaths = 20,
            CarrierKills = 5
        });

        await _context.PlayerChampionships.AddAsync(new PlayerChampionship
        {
            UserID = player.Id,
            SeasonID = season.SeasonID,
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            Tier = "Pro",
            TeamName = "Catalog Stars",
            GamertagUsed = "CatalogChamp"
        });
        await _context.SaveChangesAsync();

        var actionResult = await _controller.Catalog(CancellationToken.None);
        var catalog = actionResult.Value;

        Assert.That(catalog, Is.Not.Null);
        var item = catalog!.FirstOrDefault(s => s.SeasonID == season.SeasonID);
        Assert.That(item, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(item!.SeasonName, Is.EqualTo("Summer League 2011"));
            Assert.That(item.Year, Is.EqualTo(2011));
            Assert.That(item.TeamCount, Is.EqualTo(1));
            Assert.That(item.PlayerCount, Is.EqualTo(1));
            Assert.That(item.Champions, Has.Count.EqualTo(1));
            Assert.That(item.Champions[0].TeamName, Is.EqualTo("Catalog Stars"));
        });
    }

    [Test]
    public async Task Leaderboards_ReturnsRingLeadersAndCoreMetricLeaders()
    {
        var league = await _context.Leagues.SingleAsync(l => l.Slug == "agla");
        var h3 = await _context.GameVersions.SingleAsync(g => g.Slug == "halo3");

        var season = new Season
        {
            SeasonName = "Summer League 2012",
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            IsHistorical = true,
            SeasonStart = new DateTime(2012, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2012, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var legend = new User { UserName = "legendaryspartan", DisplayName = "Legendary Spartan" };
        await _context.Users.AddAsync(legend);
        await _context.SaveChangesAsync();

        await _context.PlayerSeasonStats.AddAsync(new PlayerSeasonStat
        {
            SeasonID = season.SeasonID,
            GameVersionID = h3.GameVesionID,
            UserID = legend.Id,
            Gamertag = "LegendarySpartan",
            TeamName = "Dynasty",
            GamesPlayed = 20,
            Goals = 50,
            Kills = 120,
            Deaths = 60,
            CarrierKills = 15
        });

        await _context.PlayerChampionships.AddAsync(new PlayerChampionship
        {
            UserID = legend.Id,
            SeasonID = season.SeasonID,
            LeagueID = league.LeagueID,
            GameVersionID = h3.GameVesionID,
            Tier = "Pro",
            TeamName = "Dynasty",
            GamertagUsed = "LegendarySpartan"
        });
        await _context.SaveChangesAsync();

        var actionResult = await _controller.Leaderboards(CancellationToken.None);
        var leaderboards = actionResult.Value;

        Assert.That(leaderboards, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(leaderboards!.RingLeaders, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(leaderboards.RingLeaders[0].Gamertag, Is.EqualTo("LegendarySpartan"));
            Assert.That(leaderboards.RingLeaders[0].RingsCount, Is.EqualTo(1));

            Assert.That(leaderboards.FranchiseDynasties, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(leaderboards.FranchiseDynasties[0].TeamName, Is.EqualTo("Dynasty"));

            Assert.That(leaderboards.GoalLeaders, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(leaderboards.GoalLeaders[0].StatValue, Is.EqualTo(50));
            Assert.That(leaderboards.GoalLeaders[0].PerGame, Is.EqualTo(2.5m));

            Assert.That(leaderboards.KillLeaders, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(leaderboards.KillLeaders[0].StatValue, Is.EqualTo(120));

            Assert.That(leaderboards.CarrierStopLeaders, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(leaderboards.CarrierStopLeaders[0].StatValue, Is.EqualTo(15));
        });
    }
}
