using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using static GrifballWebApp.Server.Test.DiscordComponentContexts;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The Late League queue's buttons (join, leave, vote for the winner) and the season signup button past their gamertag
/// guard: on a temp-file SQLite GrifballContext; the queue's store and notifier substituted, the display's refresh
/// publishing to a substituted IPublisher.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingAndSignupButtonsActionsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IQueueRepository _queue = Substitute.For<IQueueRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly TaskCompletionSource _refreshed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ServiceProvider? _services;

    [TearDown]
    public async Task TearDownServices()
    {
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private MatchmakingButtonHandler Buttons()
    {
        _publisher.When(x => x.Publish(Arg.Any<UpdateDisplayNotification>(), Arg.Any<CancellationToken>())).Do(_ => _refreshed.TrySetResult());
        _services = new ServiceCollection().AddSingleton(_publisher).BuildServiceProvider();
        var discord = Substitute.For<IDiscordRestClient>();
        var notifier = Substitute.For<IQueueNotifier>();
        var display = new QueueService(NullLogger<QueueService>.Instance, Options.Create(new DiscordOptions { QueueChannel = 1 }), _queue, discord, Db,
            Substitute.For<IDataPullService>(), Substitute.For<IOutbox>(), notifier);
        return new MatchmakingButtonHandler(new QueueMembership(_queue, Db, notifier), _services.GetRequiredService<IServiceScopeFactory>(), Db, discord, display,
            new RecordingLogger<MatchmakingButtonHandler>());
    }

    private async Task AssertDisplayRefreshed()
    {
        await _refreshed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(_refreshed.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task JoinQueue_Joining_RefreshesTheDisplay()
    {
        var player = await AddUser(1, "Alpha");
        _queue.PlayerStateByDiscord(1, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(player.Id, true, false, false, 0));
        _queue.AddPlayerToQueue(Arg.Any<QueuedPlayer>(), Arg.Any<CancellationToken>()).Returns(true);
        var context = ButtonContext(1);

        await Buttons().JoinQueue(context);

        await AssertSent(context, QueueMessages.For(QueueJoinResult.Joined));
        await AssertDisplayRefreshed();
    }

    [Test]
    public async Task LeaveQueue_Leaving_RefreshesTheDisplay()
    {
        var player = await AddUser(1, "Alpha");
        _queue.RemovePlayerToQueue(player.Id, Arg.Any<CancellationToken>()).Returns(true);
        var context = ButtonContext(1);

        await Buttons().LeaveQueue(context);

        await AssertSent(context, QueueMessages.For(QueueLeaveResult.Left));
        await AssertDisplayRefreshed();
    }

    /// <summary>Alpha and Bravo against Charlie and Delta (Discord 1 to 4).</summary>
    private async Task<MatchedMatch> AMatch()
    {
        int[] players = [(await AddUser(1, "Alpha")).Id, (await AddUser(2, "Bravo")).Id, (await AddUser(3, "Charlie")).Id, (await AddUser(4, "Delta")).Id];
        var match = MatchedMatch.Start([players[0], players[1]], [players[2], players[3]]);
        Db.MatchedMatches.Add(match);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return match;
    }

    [Test]
    public async Task VoteForWinner_APlayersVote_IsSavedAndThanked()
    {
        var match = await AMatch();
        var context = ButtonContext(1);

        await Buttons().VoteForWinner(context, match.Id, "Home");

        await AssertSent(context, "Thanks for voting! You voted for Home");
        Db.ChangeTracker.Clear();
        Assert.That(await Db.Set<MatchedWinnerVote>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task VoteForWinner_BySomeoneNotInTheMatch_IsRefused()
    {
        var match = await AMatch();
        await AddUser(5, "Echo");
        var context = ButtonContext(5);

        await Buttons().VoteForWinner(context, match.Id, "Away");

        await AssertSent(context, "You are not allowed to vote since you are not in this match");
    }

    [Test]
    public async Task VoteForWinner_AVoteItCantRead_SaysWhatItExpects()
    {
        var match = await AMatch();
        var context = ButtonContext(1);

        await Buttons().VoteForWinner(context, match.Id, "Nobody");

        await AssertSent(context, "I could not parse the value Nobody. Contact developer, expected values are Home,Away,Cancel");
    }

    private SignupButtonHandler Signups() => new(Db, new SignupsService(Db, new FakeTimeProvider(new DateTimeOffset(Now))), Urls());

    private async Task<Season> SeasonWithSignups(DateTime open, DateTime close)
    {
        var season = Season.Seed(seasonName: "Winter").WithDates(signupsOpen: open, signupsClose: close, draftStart: close.AddDays(3),
            seasonStart: close.AddDays(5), seasonEnd: close.AddDays(60));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task SignupButton_WhileSignupsAreOpen_SignsThePlayerUp()
    {
        var season = await SeasonWithSignups(Now.AddDays(-1), Now.AddDays(5));
        var player = await AddUser(1, "Alpha");
        var context = ButtonContext(1);

        await Signups().Signup(context, season.SeasonID);

        await AssertSent(context, $"You have signed up for the season. Remember you can do more things like set your availability [here](https://grif.test/login?followUp=/season/{season.SeasonID}/signupForm)");
        Assert.That(await Db.SeasonSignups.AnyAsync(s => s.SeasonID == season.SeasonID && s.UserID == player.Id), Is.True);
    }

    [Test]
    public async Task SignupButton_AfterSignupsClosed_SaysSo()
    {
        var season = await SeasonWithSignups(Now.AddDays(-10), Now.AddDays(-5));
        await AddUser(1, "Alpha");
        var context = ButtonContext(1);

        await Signups().Signup(context, season.SeasonID);

        await AssertSent(context, "Signups for this season are closed.");
        Assert.That(await Db.SeasonSignups.AnyAsync(), Is.False);
    }
}
