using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonMatchServicePossibleMatchesTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _service = new SeasonMatchService(_context, _dataPullService, Substitute.For<IBracketService>());
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetPossibleMatches_ReturnsEmpty_When_AwayTeamHasNoPlayers()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var lonely = new Team { SeasonID = seeded.Season.SeasonID, TeamName = "Empty" };
        _context.Teams.Add(lonely);
        await _context.SaveChangesAsync();
        var sm = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = lonely.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(sm);
        await _context.SaveChangesAsync();

        var result = await _service.GetPossibleMatches(sm.SeasonMatchID);

        Assert.That(result, Is.Empty);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task GetPossibleMatches_ReturnsEmpty_When_PlayersHaveNoXboxAccount()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, playersHaveXbox: false);

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(result, Is.Empty);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task GetPossibleMatches_DownloadsMatchesForAllPlayers_AndFiltersCandidates()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var h = SeasonMatchTestData.HomeXbox;
        var a = SeasonMatchTestData.AwayXbox;
        var o = SeasonMatchTestData.OtherXbox;
        var start = new DateTime(2025, 3, 1, 20, 0, 0, DateTimeKind.Utc);

        // Valid, home players on team 1 (swapped)
        var valid1 = await SeasonMatchTestData.AddMatchAsync(_context, start, a, Outcomes.Lost, h, Outcomes.Won, team0Score: 2, team1Score: 7);
        // Valid with a ringer on the away side, later start time
        var valid2 = await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(1), h, Outcomes.Won, [a[0], a[1], a[2], o[0]], Outcomes.Lost);
        // Excluded: no winner
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(2), h, Outcomes.Tie, a, Outcomes.Tie);
        // Excluded: team sizes are not 4v4
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(3), h[..3], Outcomes.Won, a, Outcomes.Lost);
        // Excluded: already linked to a season match
        var linked = await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(4), h, Outcomes.Won, a, Outcomes.Lost);
        _context.MatchLinks.Add(new MatchLink { MatchID = linked.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchNumber = 1 });
        await _context.SaveChangesAsync();
        // Excluded by GetTeams: only one known player, other team all strangers
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(5), [h[0], o[1], o[2], o[3]], Outcomes.Won, o[4..], Outcomes.Lost);
        // Excluded by GetTeams: mixed teams
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(6), [h[0], h[1], a[0], a[1]], Outcomes.Won, [h[2], h[3], a[2], a[3]], Outcomes.Lost);
        // Excluded: strangers only
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(7), o[..4], Outcomes.Won, o[4..], Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        await _dataPullService.Received(1).DownloadRecentMatchesForPlayers(
            Arg.Is<List<long>>(ids => ids.Count == 8 && h.All(ids.Contains) && a.All(ids.Contains)),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        Assert.That(result.Select(x => x.MatchID), Is.EqualTo(new[] { valid2.MatchID, valid1.MatchID }), "ordered by start time desc");

        var first = result[1]; // valid1
        Assert.Multiple(() =>
        {
            Assert.That(first.HomeTeam.TeamID, Is.EqualTo(1));
            Assert.That(first.HomeTeam.Score, Is.EqualTo(7));
            Assert.That(first.HomeTeam.Outcome, Is.EqualTo(Outcomes.Won));
            Assert.That(first.HomeTeam.Players.Select(p => p.XboxUserID), Is.EquivalentTo(h));
            Assert.That(first.HomeTeam.Players.All(p => p.IsOnTeam), Is.True);
            Assert.That(first.AwayTeam.TeamID, Is.EqualTo(0));
            Assert.That(first.AwayTeam.Score, Is.EqualTo(2));
            Assert.That(first.AwayTeam.Outcome, Is.EqualTo(Outcomes.Lost));
            var p201 = first.AwayTeam.Players.Single(p => p.XboxUserID == 201);
            Assert.That(p201.Gamertag, Is.EqualTo("GT201"));
            Assert.That(p201.Kills, Is.EqualTo(1));
            Assert.That(p201.Deaths, Is.EqualTo(1));
            Assert.That(p201.Score, Is.EqualTo(1));
        });

        var second = result[0]; // valid2, ringer o[0]
        Assert.Multiple(() =>
        {
            Assert.That(second.HomeTeam.TeamID, Is.EqualTo(0));
            Assert.That(second.AwayTeam.Players.Single(p => p.XboxUserID == o[0]).IsOnTeam, Is.False);
            Assert.That(second.AwayTeam.Players.Count(p => p.IsOnTeam), Is.EqualTo(3));
        });
    }
}
