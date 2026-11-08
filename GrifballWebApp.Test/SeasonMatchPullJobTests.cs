using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using System.Reflection;

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
        _dataPull.DownloadRecentMatchesForPlayers(default!).ReturnsForAnyArgs(new MatchPullResult(8, [], 10, 4, 6, []));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private SeasonMatchService SeasonMatches() => new(_context, _dataPull, Substitute.For<IBracketService>(), new GrifballWebApp.Server.Championships.ChampionshipService(_context));

    [Test]
    public async Task ASeasonMatchsPull_LooksThroughBothTeamsPlayers_ElevenPagesBack()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var context = Substitute.For<IJobExecutionContext>();

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(context, new SeasonMatchPull(seeded.SeasonMatch.SeasonMatchID));

        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(
            Arg.Is<List<long>>(ids => ids.Order().SequenceEqual(SeasonMatchTestData.HomeXbox.Concat(SeasonMatchTestData.AwayXbox).Order())),
            0, 10, 25, Arg.Any<CancellationToken>());
        var report = (JobRunReport)context.Result!;
        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Succeeded, "8 players, 10 Grifball games found, 4 saved before, 6 new saved")));
    }

    [Test]
    public async Task APullThatCouldntFetchEverything_FailsSayingWhat_ThoughWhatItGotIsSaved()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _dataPull.DownloadRecentMatchesForPlayers(default!).ReturnsForAnyArgs(new MatchPullResult(8, ["xuid(1)"], 10, 4, 4, [Guid.NewGuid(), Guid.NewGuid()]));
        var context = Substitute.For<IJobExecutionContext>();

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(context, new SeasonMatchPull(seeded.SeasonMatch.SeasonMatchID));

        var report = (JobRunReport)context.Result!;
        Assert.That(report.Result, Is.EqualTo(JobRunResult.Failed));
        Assert.That(report.Summary, Is.EqualTo("8 players, 10 Grifball games found, 4 saved before, 4 new saved; 2 games Halo didn't give, not saved (the next pull tries them again); 1 players' match histories incomplete"));
    }

    [Test]
    public async Task ASeasonMatchsPull_BackToItsSeasonsStart_FindsTheGamesFromADayBeforeItToAWeekAfter()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        _dataPull.DownloadMatchesBetween(default!, default, default).ReturnsForAnyArgs(new MatchPullResult(8, [], 30, 10, 20, [], CappedHistories: ["xuid(1)"]));
        var context = Substitute.For<IJobExecutionContext>();

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(context, new SeasonMatchPull(seeded.SeasonMatch.SeasonMatchID, BackToSeasonStart: true));

        var from = seeded.Season.SeasonStart - SeasonMatchService.BeforeSeason;
        var to = seeded.Season.SeasonEnd + SeasonMatchService.AfterSeason;
        await _dataPull.Received(1).DownloadMatchesBetween(
            Arg.Is<List<long>>(ids => ids.Order().SequenceEqual(SeasonMatchTestData.HomeXbox.Concat(SeasonMatchTestData.AwayXbox).Order())),
            Arg.Is<DateTime>(d => Math.Abs((d - from).TotalSeconds) < 1), Arg.Is<DateTime>(d => Math.Abs((d - to).TotalSeconds) < 1),
            Arg.Is(DataPullService.DefaultMaxCallsBack), Arg.Any<CancellationToken>());
        await _dataPull.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
        var report = (JobRunReport)context.Result!;
        Assert.That(report.Result, Is.EqualTo(JobRunResult.Succeeded), "Histories too long to search are reported, not a failure");
        Assert.That(report.Summary, Is.EqualTo($"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}: 8 players, 30 Grifball games found, 10 saved before, 20 new saved; 1 players' histories too long to search back to the season"));
    }

    [Test]
    public async Task ABackToTheSeasonPull_ForAMatchNoLongerThere_PullsTheRecentGamesOfNobody()
    {
        var context = Substitute.For<IJobExecutionContext>();

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(context, new SeasonMatchPull(-1, BackToSeasonStart: true));

        await _dataPull.DidNotReceiveWithAnyArgs().DownloadMatchesBetween(default!, default, default);
        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(ids => ids.Count == 0), 0, 10, 25, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheScheduledPull_TakesThePlayersOfSeasonsUnderWay_OnePageEach()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var ended = Season.Seed(seasonName: "Over").WithDates(seasonStart: DateTime.UtcNow.AddDays(-60), seasonEnd: DateTime.UtcNow.AddDays(-30));
        _context.Seasons.Add(ended);
        await _context.SaveChangesAsync();
        var oldTeam = new Team { SeasonID = ended.SeasonID, TeamName = "Old Team" };
        _context.Teams.Add(oldTeam);
        await _context.SaveChangesAsync();
        _context.TeamPlayers.Add(new TeamPlayer { TeamID = oldTeam.TeamID, User = new User { UserName = "old", DisplayName = "Old", XboxUser = new XboxUser { XboxUserID = 999, Gamertag = "GT999" } } });
        await _context.SaveChangesAsync();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(1));
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
    public void BothPulls_DeclareTheHaloGroup_SoAManualRunCountsAgainstIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(ActiveSeasonPullJob).GetCustomAttribute<ExecutionGroupAttribute>()?.Template, Is.EqualTo(JobsServiceCollectionExtensions.HaloMatchPulls));
            Assert.That(typeof(SeasonMatchPullJob).GetCustomAttribute<ExecutionGroupAttribute>()?.Template, Is.EqualTo(JobsServiceCollectionExtensions.HaloMatchPulls));
        });
    }
}
