using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Test;

/// <summary>
/// The jobs that pull players' recent games from Halo Infinite: a season match's, from its page, and every player's in
/// a season under way, on a schedule.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchPullJobTests
{
    private GrifballContext _context = null!;
    private IDataPullService _dataPull = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPull = Substitute.For<IDataPullService>();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private SeasonMatchService SeasonMatches() => new(_context, _dataPull, Substitute.For<IBracketService>(), new GrifballWebApp.Server.Championships.ChampionshipService(_context));

    [Test]
    public async Task ASeasonMatchsPull_LooksThroughBothTeamsPlayers_ElevenPagesBack()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _dataPull.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new MatchPullResult(3, 1));
        var context = Substitute.For<IJobExecutionContext>();

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(context, new SeasonMatchPull(seeded.SeasonMatch.SeasonMatchID));

        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(
            Arg.Is<List<long>>(ids => ids.Order().SequenceEqual(SeasonMatchTestData.HomeXbox.Concat(SeasonMatchTestData.AwayXbox).Order())),
            0, 10, 25, Arg.Any<CancellationToken>());
        Assert.That(((JobRunReport)context.Result!).Summary, Is.EqualTo("8 players: 3 new matches saved, 1 failed (the next pull tries them again)"));
    }

    [Test]
    public async Task TheScheduledPull_TakesThePlayersOfSeasonsUnderWay_OnePageEach()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var ended = new Season { SeasonName = "Over", SeasonStart = DateTime.UtcNow.AddDays(-60), SeasonEnd = DateTime.UtcNow.AddDays(-30) };
        _context.Seasons.Add(ended);
        await _context.SaveChangesAsync();
        var oldTeam = new Team { SeasonID = ended.SeasonID, TeamName = "Old Team" };
        _context.Teams.Add(oldTeam);
        await _context.SaveChangesAsync();
        _context.TeamPlayers.Add(new TeamPlayer { TeamID = oldTeam.TeamID, User = new User { UserName = "old", DisplayName = "Old", XboxUser = new XboxUser { XboxUserID = 999, Gamertag = "GT999" } } });
        await _context.SaveChangesAsync();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(1));
        _dataPull.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(MatchPullResult.None);
        var context = Substitute.For<IJobExecutionContext>();

        await new ActiveSeasonPullJob(_context, _dataPull, time).Execute(context);

        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(
            Arg.Is<List<long>>(ids => ids.Order().SequenceEqual(SeasonMatchTestData.HomeXbox.Concat(SeasonMatchTestData.AwayXbox).Order())),
            0, 0, 25, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheScheduledPull_WithNoSeasonUnderWay_AsksHaloNothing()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var context = Substitute.For<IJobExecutionContext>();

        // Before the season starts
        await new ActiveSeasonPullJob(_context, _dataPull, new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-1))).Execute(context);

        await _dataPull.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
        Assert.That(((JobRunReport)context.Result!).Result, Is.EqualTo(JobRunResult.Skipped));
    }

    [Test]
    public void TheScheduledPull_RunsEvery30Minutes_WithTheOtherHaloPulls()
    {
        var trigger = (ISimpleTrigger)ActiveSeasonPullJob.Definition.DefaultTrigger(DateTimeOffset.UtcNow);
        Assert.Multiple(() =>
        {
            Assert.That(trigger.RepeatInterval, Is.EqualTo(TimeSpan.FromMinutes(30)));
            Assert.That(trigger.ExecutionGroup, Is.EqualTo(JobsServiceCollectionExtensions.HaloMatchPulls));
            Assert.That(SeasonMatchPullJob.RunName(42), Is.EqualTo("season-match-42"));
        });
    }

    [Test]
    public void APull_IsAFailedRun_OnlyWhenEveryMatchItFoundFailed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new MatchPullResult(0, 4).Report(8).Result, Is.EqualTo(JobRunResult.Failed));
            Assert.That(new MatchPullResult(2, 4).Report(8).Result, Is.EqualTo(JobRunResult.Succeeded));
            Assert.That(MatchPullResult.None.Report(8), Is.EqualTo(JobRunReport.Succeeded("8 players: 0 new matches saved")));
        });
    }
}
