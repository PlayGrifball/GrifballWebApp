using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Integration.Test;

/// <summary>get_recent_games (<see cref="HaloTools"/>) on each database: what "newest" means where they sort differently.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloToolsTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetRecentGames_PutsAGameWithNoStartTime_LastNotFirst()
    {
        // Postgres sorts NULLs first when descending: a game with no start time must not count as the newest
        var xbox = XboxUser.FromXbox(9_200_001, "Undated");
        _context.XboxUsers.Add(xbox);
        var dated = HaloMatch.Seed(Guid.NewGuid(), startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc));
        dated.AddTeam(0, 50, Outcomes.Won);
        dated.AddParticipant(0, xbox.XboxUserID, new MatchParticipantStats());
        var undated = HaloMatch.Seed(Guid.NewGuid(), startTime: null);
        undated.AddTeam(0, 10, Outcomes.Lost);
        undated.AddParticipant(0, xbox.XboxUserID, new MatchParticipantStats());
        _context.Matches.AddRange(dated, undated);
        await _context.SaveChangesAsync();
        var xboxUsers = Substitute.For<IGetsertXboxUserService>();
        xboxUsers.GetsertXboxUserByGamertag("Undated", Arg.Any<CancellationToken>()).Returns((xbox, (string?)null));
        var dataPull = Substitute.For<IDataPullService>();
        dataPull.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(MatchPullResult.Empty);

        var recent = await new HaloTools(_context, xboxUsers, dataPull, NullLogger<HaloTools>.Instance).GetRecentGames("Undated", 1);

        Assert.That(recent.Games.Single().MatchID, Is.EqualTo(dated.MatchID));
    }
}
