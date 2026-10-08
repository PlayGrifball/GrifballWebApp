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
public class StatsControllerTests
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
        var players = Enumerable.Range(1, 12).Select(i => new GradesTestData.P(i, 0, i * 2, 0, 0)).ToArray();
        await GradesTestData.AddLinkedMatch(_context, null, 0, TimeSpan.FromMinutes(10), players.Append(new GradesTestData.P(50, 0, 0, 0, 0)).ToArray());
        await GradesTestData.AddLinkedMatch(_context, null, 0, TimeSpan.FromMinutes(10), new GradesTestData.P(1, 0, 100, 0, 0));
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
