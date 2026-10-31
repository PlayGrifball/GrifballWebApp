using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;

namespace GrifballWebApp.Test;

/// <summary>Matchmaking's outbox handlers, on the edges the queue tests don't reach.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchOutboxHandlerTests
{
    private const ulong LogChannel = 222;
    private const ulong ThreadId = 999;

    private GrifballContext _context;
    private IDiscordRestClient _discord;
    private OutboxTestDriver _outbox;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discord = Substitute.For<IDiscordRestClient>();
        var sent = Substitute.For<IDiscordRestMessage>();
        sent.Id.Returns(77ul);
        _discord.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(sent);
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        _outbox = new OutboxTestDriver(_context, _discord, new DiscordOptions { LogChannel = LogChannel, QueueChannel = 111 });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _outbox.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

    private async Task<MatchedMatch> SeedMatch(ulong? threadId = ThreadId, ulong? voteMessageId = null)
    {
        var home = new User { UserName = "home" };
        var away = new User { UserName = "away" };
        var match = new MatchedMatch
        {
            HomeTeam = new MatchedTeam { Players = [new MatchedPlayer { User = home }] },
            AwayTeam = new MatchedTeam { Players = [new MatchedPlayer { User = away }] },
            ThreadID = threadId,
            VoteMessageID = voteMessageId,
        };
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    private async Task Enqueue<T>(string kind, T payload, int matchId)
    {
        await new ContextOutbox(_context).EnqueueAsync(kind, payload, MatchOutbox.Group(matchId));
        await _context.SaveChangesAsync();
    }

    private static RestException NotFound() => new(HttpStatusCode.NotFound, "Not Found", new RestError(10003, "Unknown Channel", null));

    [Test]
    public async Task ThreadClose_ThreadAlreadyGone_IsDone_WithoutDeleting()
    {
        var match = await SeedMatch();
        _discord.SendMessageAsync(ThreadId, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(NotFound());
        await Enqueue(MatchOutbox.ThreadClose, new MatchPayload(match.Id), match.Id);

        await _outbox.DrainAsync();

        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
        await _discord.DidNotReceive().DeleteChannelAsync(Arg.Any<ulong>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThreadClose_WaitsBeforeDeleting_AndAThreadGoneByThen_IsDone()
    {
        var match = await SeedMatch();
        _discord.DeleteChannelAsync(ThreadId, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(NotFound());
        await Enqueue(MatchOutbox.ThreadClose, new MatchPayload(match.Id), match.Id);

        await _outbox.RunDueAsync();
        var waiting = (await _outbox.MessagesAsync()).Single();
        var noticeAt = _outbox.Time.Now.UtcDateTime;
        await _outbox.DrainAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(waiting.Status, Is.EqualTo(OutboxStatus.Pending));
            Assert.That(waiting.NextAttemptAt, Is.EqualTo(noticeAt + MatchOutbox.ThreadDeleteDelay).Within(TimeSpan.FromMilliseconds(5)));
            Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
        });
        await _discord.Received(1).SendMessageAsync(ThreadId, Arg.Is<MessageProperties>(m => m.Content == MatchThreadCloseHandler.Notice), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.Received(1).DeleteChannelAsync(ThreadId, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThreadClose_MatchWithoutAThread_HasNothingToClose()
    {
        var match = await SeedMatch(threadId: null);
        await Enqueue(MatchOutbox.ThreadClose, new MatchPayload(match.Id), match.Id);

        await _outbox.DrainAsync();

        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
        Assert.That(_discord.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task CanceledPost_AlreadyPostedByAFailedTry_IsNotPostedAgain()
    {
        var match = await SeedMatch();
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(1ul);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.Author.Id.Returns(1ul);
        var embed = Substitute.For<IDiscordEmbed>();
        embed.Title.Returns("Match Canceled");
        embed.Description.Returns($"Match #{match.Id} has been canceled.");
        posted.Embeds.Returns([embed]);
        _discord.GetMessagesAsync(LogChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { posted }.ToAsyncEnumerable());
        var calls = 0;
        _discord.SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1 ? Task.FromException<IDiscordRestMessage>(new TaskCanceledException("timed out")) : Task.FromResult(posted));
        await Enqueue(MatchOutbox.CanceledPost, new MatchCanceledPayload(match.Id, Unannounced: false), match.Id);

        await _outbox.DrainAsync();

        Assert.That(calls, Is.EqualTo(1));
        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
    }

    [Test]
    public async Task VotePost_AlreadySaved_IsNotPostedAgain()
    {
        var match = await SeedMatch(voteMessageId: 4242);
        await Enqueue(MatchOutbox.VotePost, new MatchPayload(match.Id), match.Id);

        await _outbox.DrainAsync();

        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
        Assert.That(_discord.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task ThreadSteps_WithoutTheStepsBefore_Fail()
    {
        // Each step needs what the one before it made: run out of order, they fail rather than guess.
        var match = await SeedMatch(threadId: null);
        await new ContextOutbox(_context).EnqueueAsync(MatchOutbox.ThreadCreate, new MatchPayload(match.Id), "a");
        await new ContextOutbox(_context).EnqueueAsync(MatchOutbox.ThreadInvite, new MatchPayload(match.Id), "b");
        await new ContextOutbox(_context).EnqueueAsync(MatchOutbox.VotePost, new MatchPayload(match.Id), "c");
        await _context.SaveChangesAsync();

        await _outbox.RunDueAsync();

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(messages.Select(x => x.Status), Is.All.EqualTo(OutboxStatus.Failed));
            Assert.That(messages[0].LastError, Is.EqualTo($"Match {match.Id} has no Match Created post to make its thread from"));
            Assert.That(messages[1].LastError, Is.EqualTo($"Match {match.Id} has no thread"));
            Assert.That(messages[2].LastError, Is.EqualTo($"Match {match.Id} has no thread"));
        });
    }

    [Test]
    public async Task Announcement_DeadLetteredForAMatchAlreadyOver_LeavesItAlone()
    {
        var match = await SeedMatch(threadId: null);
        match.Active = false;
        await _context.SaveChangesAsync();
        _discord.SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("503"));
        await Enqueue(MatchOutbox.CreatedPost, new MatchCreatedPayload(match.Id, MayBePosted: false), match.Id);

        await _outbox.DrainAsync();

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That((await _outbox.MessagesAsync()).Select(x => x.Kind), Is.EqualTo(new[] { MatchOutbox.CreatedPost }), "Nothing more queued");
            Assert.That(await fresh.QueuedPlayer.AnyAsync(), Is.False, "Nobody requeued");
        });
    }
}
