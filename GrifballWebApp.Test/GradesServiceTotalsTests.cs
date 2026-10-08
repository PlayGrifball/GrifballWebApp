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

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class GradesServiceTotalsTests
{
    private GrifballContext _context;
    private GradesService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new GradesService(_context);
        await GradesTestData.SeedMedals(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<int> SeedThreePlayers()
    {
        var (season, sm) = await GradesTestData.SeedSeason(_context);
        await GradesTestData.AddLinkedMatch(_context, sm.SeasonMatchID, 1, TimeSpan.FromMinutes(10),
            new GradesTestData.P(1, Score: 6, Kills: 20, Deaths: 2, PowerWeaponKills: 2, new() { [1] = 3, [2] = 2, [3] = 3, [4] = 3, [5] = 3, [6] = 1, [7] = 3, [9] = 4 }),
            new GradesTestData.P(2, Score: 3, Kills: 10, Deaths: 5, PowerWeaponKills: 2, new() { [1] = 1, [3] = 1, [4] = 1, [5] = 1, [7] = 1 }),
            new GradesTestData.P(3, Score: 0, Kills: 5, Deaths: 10, PowerWeaponKills: 2));
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
        var (season, sm) = await GradesTestData.SeedSeason(_context);
        await GradesTestData.AddLinkedMatch(_context, sm.SeasonMatchID, 1, TimeSpan.FromMinutes(12), new GradesTestData.P(9, 1, 1, 1, 0));

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
        var (season, sm) = await GradesTestData.SeedSeason(_context);
        var (_, otherSm) = await GradesTestData.SeedSeason(_context, "Other");
        await GradesTestData.AddLinkedMatch(_context, sm.SeasonMatchID, 1, TimeSpan.FromMinutes(10), new GradesTestData.P(1, 2, 4, 1, 0));
        await GradesTestData.AddLinkedMatch(_context, sm.SeasonMatchID, 2, TimeSpan.FromMinutes(15), new GradesTestData.P(1, 3, 6, 2, 1));
        // Same player in another season and an unlinked game: must not count
        await GradesTestData.AddLinkedMatch(_context, otherSm.SeasonMatchID, 1, TimeSpan.FromMinutes(30), new GradesTestData.P(1, 100, 100, 0, 0));
        await GradesTestData.AddLinkedMatch(_context, null, 0, TimeSpan.FromMinutes(30), new GradesTestData.P(1, 100, 100, 0, 0), new GradesTestData.P(2, 1, 1, 1, 1));

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
        Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(GradesController), nameof(GradesController.GetGrades)), Is.Null, "grades are public");
    }
}
