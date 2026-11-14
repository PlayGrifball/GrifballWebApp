using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// get_recent_games (<see cref="HaloTools"/>) on a temp-file SQLite database, with Halo's client and the pull substituted: it pulls
/// the player's latest games, then gives them from the database with their Waypoint links.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloToolsUnitTests : SqliteGrifballContextFixture
{
    private static readonly long[] Xuid77 = [77];
    private static readonly string[] BothPlayers = ["Grunt Padre", "Other"];
    private static readonly Guid MatchID = Guid.Parse("6f0c1c7e-1a52-4c11-9d7e-3f1c2b9a0a01");

    private readonly RecordingLogger<HaloTools> _logger = new();
    private readonly IGetsertXboxUserService _xboxUsers = Substitute.For<IGetsertXboxUserService>();
    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();

    private HaloTools Tools() => new(Db, _xboxUsers, _dataPull, _logger);

    private async Task<XboxUser> AddPlayer()
    {
        var xbox = XboxUser.FromXbox(77, "Grunt Padre");
        Db.XboxUsers.Add(xbox);
        await Db.SaveChangesAsync();
        _xboxUsers.GetsertXboxUserByGamertag("Grunt Padre", Arg.Any<CancellationToken>()).Returns((xbox, (string?)null));
        return xbox;
    }

    [Test]
    public async Task GetRecentGames_PullsThePlayersGames_AndGivesTheirLatestWithWaypointLinks()
    {
        await AddPlayer();
        Db.XboxUsers.Add(XboxUser.FromXbox(78, "Other"));
        var match = HaloMatch.Seed(MatchID, startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc));
        match.AddTeam(0, 50, Outcomes.Won);
        match.AddTeam(1, 20, Outcomes.Lost);
        match.AddParticipant(0, 77, new MatchParticipantStats());
        match.AddParticipant(1, 78, new MatchParticipantStats());
        Db.Matches.Add(match);
        // Someone else's game: not theirs
        var notTheirs = HaloMatch.Seed(Guid.NewGuid(), startTime: new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc));
        notTheirs.AddTeam(0, 1, Outcomes.Won);
        notTheirs.AddParticipant(0, 78, new MatchParticipantStats());
        Db.Matches.Add(notTheirs);
        await Db.SaveChangesAsync();

        var games = await Tools().GetRecentGames("Grunt Padre", 3);

        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(x => x.SequenceEqual(Xuid77)), 0, 0, 3, Arg.Any<CancellationToken>());
        var game = games.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_logger.Entries, Is.Empty);
            Assert.That(game.MatchID, Is.EqualTo(MatchID));
            Assert.That(game.WaypointUrl, Is.EqualTo($"https://www.halowaypoint.com/halo-infinite/players/Grunt%20Padre/matches/{MatchID}"));
            Assert.That(game.Teams.SelectMany(t => t.Players), Is.EquivalentTo(BothPlayers));
            Assert.That(game.Teams.Single(t => t.TeamID == 0).Outcome, Is.EqualTo(Outcomes.Won));
        }
    }

    [Test]
    public async Task GetRecentGames_WhenThePullFails_LogsItAndGivesTheGamesAlreadySaved()
    {
        await AddPlayer();
        _dataPull.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Halo is down"));

        var games = await Tools().GetRecentGames("Grunt Padre");

        var entry = _logger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo("Failed to fetch recent matches for Grunt Padre"));
            Assert.That(entry.Exception, Is.TypeOf<HttpRequestException>());
            Assert.That(games, Is.Empty);
        }
    }

    [Test]
    public async Task GetRecentGames_RefusesAnUnknownGamertag_WithWhyItIsUnknown()
    {
        _xboxUsers.GetsertXboxUserByGamertag("Nobody", Arg.Any<CancellationToken>()).Returns(((XboxUser?)null, (string?)"No such gamertag"));
        _xboxUsers.GetsertXboxUserByGamertag("Silent", Arg.Any<CancellationToken>()).Returns(((XboxUser?)null, (string?)null));

        var refused = await Assert.ThrowsAsync<ToolException>(() => Tools().GetRecentGames("Nobody"));
        var silent = await Assert.ThrowsAsync<ToolException>(() => Tools().GetRecentGames("Silent"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused!.Message, Is.EqualTo("No such gamertag"));
            Assert.That(silent!.Message, Does.Contain("Silent"));
        }
        await _dataPull.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [TestCase(0, 1)]
    [TestCase(100, HaloTools.MaxGames)]
    public async Task GetRecentGames_KeepsTheCountInRange(int asked, int pulled)
    {
        await AddPlayer();

        await Tools().GetRecentGames("Grunt Padre", asked);

        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), 0, 0, pulled, Arg.Any<CancellationToken>());
    }
}
