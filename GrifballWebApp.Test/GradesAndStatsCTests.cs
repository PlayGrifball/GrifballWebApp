using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Controllers;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections;
using System.Reflection;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

internal static class GradesSeedC
{
    public static async Task SeedMedals(GrifballContext context)
    {
        context.MedalTypes.AddRange(
            new MedalType { MedalTypeID = 1, MedalTypeName = "Spree" },
            new MedalType { MedalTypeID = 2, MedalTypeName = "Mode" },
            new MedalType { MedalTypeID = 3, MedalTypeName = "Multikill" });
        context.MedalDifficulties.AddRange(
            new MedalDifficulty { MedalDifficultyID = 1, MedalDifficultyName = "Normal" },
            new MedalDifficulty { MedalDifficultyID = 2, MedalDifficultyName = "Heroic" },
            new MedalDifficulty { MedalDifficultyID = 3, MedalDifficultyName = "Legendary" },
            new MedalDifficulty { MedalDifficultyID = 4, MedalDifficultyName = "Mythic" });
        context.Medals.AddRange(
            M(1, "Killing Spree", 1, 1),
            M(2, "Killjoy", 1, 1),
            M(3, "Double Kill", 3, 1),
            M(4, "Triple Kill", 3, 2),
            M(5, "Overkill", 3, 4),
            M(6, "Grand Slam", 2, 1),
            M(7, "Pancake", 2, 1),
            M(8, "Whiplash", 2, 1),
            M(9, "Killtacular", 3, 3)); // multikill but difficulty 3 => not counted
        await context.SaveChangesAsync();

        static Medal M(long id, string name, int type, int difficulty) => new()
        {
            MedalID = id, MedalName = name, Description = name, MedalTypeID = type, MedalDifficultyID = difficulty,
        };
    }

    public static async Task<(Season season, SeasonMatch seasonMatch)> SeedSeason(GrifballContext context, string name = "Grades Season")
    {
        var season = new Season { SeasonName = name };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var sm = new SeasonMatch { SeasonID = season.SeasonID, BestOf = 5 };
        context.SeasonMatches.Add(sm);
        await context.SaveChangesAsync();
        return (season, sm);
    }

    public record P(long Xbox, int Score, int Kills, int Deaths, int PowerWeaponKills, Dictionary<long, int>? Medals = null);

    public static async Task<Match> AddLinkedMatch(GrifballContext context, int? seasonMatchID, int matchNumber, TimeSpan duration, params P[] players)
    {
        foreach (var p in players)
        {
            if (await context.XboxUsers.FindAsync(p.Xbox) is null)
                context.XboxUsers.Add(new XboxUser { XboxUserID = p.Xbox, Gamertag = $"GT{p.Xbox}" });
        }
        var id = Guid.NewGuid();
        var match = new Match
        {
            MatchID = id,
            Duration = duration,
            StartTime = DateTime.UtcNow,
            MatchTeams = new List<MatchTeam>
            {
                new()
                {
                    MatchID = id, TeamID = 0, Outcome = Outcomes.Won,
                    MatchParticipants = players.Select(p => new MatchParticipant
                    {
                        MatchID = id, TeamID = 0, XboxUserID = p.Xbox, Score = p.Score, Kills = p.Kills, Deaths = p.Deaths, PowerWeaponKills = p.PowerWeaponKills,
                        MedalEarned = (p.Medals ?? new()).Select(m => new MedalEarned { MedalID = m.Key, MatchID = id, XboxUserID = p.Xbox, Count = m.Value }).ToList(),
                    }).ToList(),
                },
            },
        };
        context.Matches.Add(match);
        if (seasonMatchID is not null)
            context.MatchLinks.Add(new MatchLink { MatchID = id, SeasonMatchID = seasonMatchID.Value, MatchNumber = matchNumber });
        await context.SaveChangesAsync();
        return match;
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class GradesServiceCoverageCTests
{
    private GrifballContext _context;
    private GradesService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new GradesService(_context);
        await GradesSeedC.SeedMedals(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<int> SeedThreePlayers()
    {
        var (season, sm) = await GradesSeedC.SeedSeason(_context);
        await GradesSeedC.AddLinkedMatch(_context, sm.SeasonMatchID, 1, TimeSpan.FromMinutes(10),
            new GradesSeedC.P(1, Score: 6, Kills: 20, Deaths: 2, PowerWeaponKills: 2, new() { [1] = 3, [2] = 2, [3] = 3, [4] = 3, [5] = 3, [6] = 1, [7] = 3, [9] = 4 }),
            new GradesSeedC.P(2, Score: 3, Kills: 10, Deaths: 5, PowerWeaponKills: 2, new() { [1] = 1, [3] = 1, [4] = 1, [5] = 1, [7] = 1 }),
            new GradesSeedC.P(3, Score: 0, Kills: 5, Deaths: 10, PowerWeaponKills: 2));
        return season.SeasonID;
    }

    [Test]
    public async Task GetGrades_ComputesTotals()
    {
        var seasonID = await SeedThreePlayers();

        var result = await _service.GetGrades(seasonID, CancellationToken.None);

        Assert.That(result.Totals.Select(x => x.Gamertag), Is.EqualTo(new[] { "GT1", "GT2", "GT3" }), "ordered by gamertag");
        var a = result.Totals[0];
        var b = result.Totals[1];
        var c = result.Totals[2];
        Assert.Multiple(() =>
        {
            Assert.That(a.XboxUserID, Is.EqualTo(1));
            Assert.That(a.TotalGoals, Is.EqualTo(6));
            Assert.That(a.TotalKDSpread, Is.EqualTo(18));
            Assert.That(a.TotalPunches, Is.EqualTo(18));
            Assert.That(a.TotalSprees, Is.EqualTo(3), "Killjoy is a spree-type medal but excluded");
            Assert.That(a.TotalDoubleKills, Is.EqualTo(3));
            Assert.That(a.TotalTripleKills, Is.EqualTo(3));
            Assert.That(a.TotalMultiKills, Is.EqualTo(4), "Overkill (difficulty 4) + Grand Slam; Killtacular (difficulty 3) excluded");
            Assert.That(a.TotalXFactor, Is.EqualTo(5), "Killjoy + Pancake");
            Assert.That(a.TotalKills, Is.EqualTo(20));
            Assert.That(a.TotalGameTime, Is.EqualTo(10d));

            Assert.That(b.TotalKDSpread, Is.EqualTo(5));
            Assert.That(b.TotalPunches, Is.EqualTo(8));
            Assert.That(b.TotalSprees, Is.EqualTo(1));
            Assert.That(b.TotalMultiKills, Is.EqualTo(1));
            Assert.That(b.TotalXFactor, Is.EqualTo(1));

            Assert.That(c.TotalKDSpread, Is.EqualTo(-5));
            Assert.That(c.TotalSprees, Is.EqualTo(0));
            Assert.That(c.TotalXFactor, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetGrades_ComputesPerMinuteStats()
    {
        var seasonID = await SeedThreePlayers();

        var result = await _service.GetGrades(seasonID, CancellationToken.None);

        var a = result.PerMinutes.Single(x => x.XboxUserID == 1);
        Assert.Multiple(() =>
        {
            Assert.That(result.PerMinutes, Has.Count.EqualTo(3));
            Assert.That(a.Gamertag, Is.EqualTo("GT1"));
            Assert.That(a.GoalsPM, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(a.KDSpreadPM, Is.EqualTo(1.8).Within(1e-9));
            Assert.That(a.PunchesPM, Is.EqualTo(1.8).Within(1e-9));
            Assert.That(a.SpreesPM, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(a.DoubleKillsPM, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(a.TripleKillsPM, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(a.MultiKillsPM, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(a.XFactorPM, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(a.KillsPM, Is.EqualTo(2.0).Within(1e-9));
        });
    }

    [Test]
    public async Task GetGrades_AssignsLetterGradesByPercentile()
    {
        var seasonID = await SeedThreePlayers();

        var result = await _service.GetGrades(seasonID, CancellationToken.None);

        var best = result.Letters.Single(x => x.XboxUserID == 1);
        var mid = result.Letters.Single(x => x.XboxUserID == 2);
        var worst = result.Letters.Single(x => x.XboxUserID == 3);

        // Best player is at the top of every category => S+ (value 11). Middle of 3 sits at the 50th percentile => "C" (value 6).
        // Worst player is the minimum => falls through to F- (value 1).
        Assert.Multiple(() =>
        {
            foreach (var (grade, letter) in new[] { (best, "S+"), (mid, "C"), (worst, "F-") })
            {
                Assert.That(new[] { grade.Goals, grade.KDSpread, grade.Punches, grade.Sprees, grade.DoubleKills, grade.TripleKills, grade.MultiKills, grade.XFactor, grade.Kills },
                    Is.All.EqualTo(letter), grade.Gamertag);
                Assert.That(grade.GradeAvg, Is.EqualTo(letter), grade.Gamertag);
            }
            const double weights = .40 + .55 + .15 + .35 + .20 + .25 + .35 + .10 + .25;
            Assert.That(best.GradeAvgMath, Is.EqualTo(11 * weights).Within(1e-9));
            Assert.That(mid.GradeAvgMath, Is.EqualTo(6 * weights).Within(1e-9));
            Assert.That(worst.GradeAvgMath, Is.EqualTo(1 * weights).Within(1e-9));
        });
    }

    [Test]
    public async Task GetGrades_SinglePlayer_GetsTopGrade()
    {
        var (season, sm) = await GradesSeedC.SeedSeason(_context);
        await GradesSeedC.AddLinkedMatch(_context, sm.SeasonMatchID, 1, TimeSpan.FromMinutes(12), new GradesSeedC.P(9, 1, 1, 1, 0));

        var result = await _service.GetGrades(season.SeasonID, CancellationToken.None);

        var only = result.Letters.Single();
        Assert.Multiple(() =>
        {
            Assert.That(only.Goals, Is.EqualTo("S+"));
            Assert.That(only.Kills, Is.EqualTo("S+"));
            Assert.That(only.GradeAvg, Is.EqualTo("S+"));
            Assert.That(result.Totals.Single().TotalGameTime, Is.EqualTo(12d));
        });
    }

    [Test]
    public async Task GetGrades_SumsGameTimeAcrossMatches_AndIgnoresOtherSeasonsAndUnlinkedMatches()
    {
        var (season, sm) = await GradesSeedC.SeedSeason(_context);
        var (_, otherSm) = await GradesSeedC.SeedSeason(_context, "Other");
        await GradesSeedC.AddLinkedMatch(_context, sm.SeasonMatchID, 1, TimeSpan.FromMinutes(10), new GradesSeedC.P(1, 2, 4, 1, 0));
        await GradesSeedC.AddLinkedMatch(_context, sm.SeasonMatchID, 2, TimeSpan.FromMinutes(15), new GradesSeedC.P(1, 3, 6, 2, 1));
        // Same player in another season and an unlinked game: must not count
        await GradesSeedC.AddLinkedMatch(_context, otherSm.SeasonMatchID, 1, TimeSpan.FromMinutes(30), new GradesSeedC.P(1, 100, 100, 0, 0));
        await GradesSeedC.AddLinkedMatch(_context, null, 0, TimeSpan.FromMinutes(30), new GradesSeedC.P(1, 100, 100, 0, 0), new GradesSeedC.P(2, 1, 1, 1, 1));

        var result = await _service.GetGrades(season.SeasonID, CancellationToken.None);

        var total = result.Totals.Single();
        var pm = result.PerMinutes.Single();
        Assert.Multiple(() =>
        {
            Assert.That(total.XboxUserID, Is.EqualTo(1));
            Assert.That(total.TotalGoals, Is.EqualTo(5));
            Assert.That(total.TotalKills, Is.EqualTo(10));
            Assert.That(total.TotalKDSpread, Is.EqualTo(7));
            Assert.That(total.TotalPunches, Is.EqualTo(9));
            Assert.That(total.TotalGameTime, Is.EqualTo(25d));
            Assert.That(pm.GoalsPM, Is.EqualTo(5 / 25d).Within(1e-9));
            Assert.That(pm.KillsPM, Is.EqualTo(10 / 25d).Within(1e-9));
        });
    }

    [Test]
    public async Task GradesController_ReturnsServiceResult()
    {
        var seasonID = await SeedThreePlayers();
        var controller = new GradesController(_service);

        var result = await controller.GetGrades(seasonID, CancellationToken.None);

        Assert.That(result.Letters, Has.Count.EqualTo(3));
        Assert.That(typeof(GradesController).GetMethod(nameof(GradesController.GetGrades))!.GetCustomAttribute<AuthorizeAttribute>(), Is.Null, "grades are public");
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class StatsControllerCTests
{
    private GrifballContext _context;
    private StatsController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new StatsController(Substitute.For<ILogger<StatsController>>(), _context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static List<(int Rank, string Gamertag, int Kills)> Read(IActionResult result)
    {
        var value = (result as OkObjectResult)?.Value as IEnumerable;
        Assert.That(value, Is.Not.Null);
        return value!.Cast<object>().Select(x =>
        {
            var t = x.GetType();
            return ((int)t.GetProperty("Rank")!.GetValue(x)!, (string)t.GetProperty("Gamertag")!.GetValue(x)!, (int)t.GetProperty("Kills")!.GetValue(x)!);
        }).ToList();
    }

    [Test]
    public async Task TopKills_ReturnsEmpty_When_NoKills()
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 1, Gamertag = "Nobody" });
        await _context.SaveChangesAsync();

        var result = Read(await _controller.TopKills());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task TopKills_ReturnsTop10_RankedBySummedKills()
    {
        // 12 players with kills split across two matches, one player with zero kills, one with no games
        var players = Enumerable.Range(1, 12).Select(i => new GradesSeedC.P(i, 0, i * 2, 0, 0)).ToArray();
        await GradesSeedC.AddLinkedMatch(_context, null, 0, TimeSpan.FromMinutes(10), players.Append(new GradesSeedC.P(50, 0, 0, 0, 0)).ToArray());
        await GradesSeedC.AddLinkedMatch(_context, null, 0, TimeSpan.FromMinutes(10), new GradesSeedC.P(1, 0, 100, 0, 0));
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 60, Gamertag = "NoGames" });
        await _context.SaveChangesAsync();

        var result = Read(await _controller.TopKills());

        Assert.That(result, Has.Count.EqualTo(10));
        Assert.Multiple(() =>
        {
            Assert.That(result.Select(x => x.Rank), Is.EqualTo(Enumerable.Range(1, 10)));
            Assert.That(result[0], Is.EqualTo((1, "GT1", 102)));
            Assert.That(result[1], Is.EqualTo((2, "GT12", 24)));
            Assert.That(result[9], Is.EqualTo((10, "GT4", 8)));
            Assert.That(result.Select(x => x.Gamertag), Has.None.EqualTo("GT50").And.None.EqualTo("NoGames"));
        });
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonMatchControllerCTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _controller = new SeasonMatchController(new SeasonMatchService(_context, _dataPullService, Substitute.For<IBracketService>()));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetSeasonMatchPage_ReturnsNull_When_Missing()
    {
        Assert.That(await _controller.GetSeasonMatchPage(1234, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task GetSeasonMatchPage_ReturnsPage()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var page = await _controller.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        Assert.That(page!.HomeTeamName, Is.EqualTo("Home Team"));
    }

    [Test]
    public async Task ReportMatch_ReturnsBadRequest_ForEmptyGuid_WithoutCallingService()
    {
        var result = await _controller.ReportMatch(1, Guid.Empty, CancellationToken.None);

        Assert.That((result as BadRequestObjectResult)?.Value, Is.EqualTo("Provide valid Guid"));
        await _dataPullService.DidNotReceiveWithAnyArgs().GetAndSaveMatch(default);
    }

    [Test]
    public async Task ReportMatch_ReturnsOk_AndLinksMatch()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var result = await _controller.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkResult>());
        Assert.That(await _context.MatchLinks.AnyAsync(x => x.MatchID == match.MatchID), Is.True);
    }

    [Test]
    public async Task Forfeits_ReturnOk_AndSetResults()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var second = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = seeded.Away.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(second);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var home = await _controller.HomeForfeit(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        var away = await _controller.AwayForfeit(second.SeasonMatchID, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var r1 = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID);
        var r2 = await _context.SeasonMatches.SingleAsync(x => x.SeasonMatchID == second.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(home, Is.InstanceOf<OkResult>());
            Assert.That(away, Is.InstanceOf<OkResult>());
            Assert.That(r1.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(r2.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
        });
    }

    [Test]
    public async Task GetPossibleMatches_DelegatesToService()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var result = await _controller.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID, CancellationToken.None);
        Assert.That(result, Is.Empty);
        await _dataPullService.ReceivedWithAnyArgs(1).DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public void Authorization_IsConfigured()
    {
        var type = typeof(SeasonMatchController);
        Assert.Multiple(() =>
        {
            foreach (var name in new[] { nameof(SeasonMatchController.ReportMatch), nameof(SeasonMatchController.HomeForfeit), nameof(SeasonMatchController.AwayForfeit) })
                Assert.That(type.GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo("Commissioner"), name);
            foreach (var name in new[] { nameof(SeasonMatchController.GetSeasonMatchPage), nameof(SeasonMatchController.GetPossibleMatches) })
                Assert.That(type.GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>(), Is.Null, name);
            Assert.That(type.GetCustomAttribute<AuthorizeAttribute>(), Is.Null);
        });
    }
}
