using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// MatchOutbox's handlers, on a real (temp-file SQLite) GrifballContext with a substituted IDiscordRestClient: no
/// database server, host or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchOutboxHandlersTests : SqliteContextFixture
{
    private const ulong LogChannel = 222UL;
    private static readonly int[] HomeIds = [1, 2];
    private static readonly int[] AwayIds = [3, 4];

    private IDiscordRestClient _discord = null!;
    private DiscordOptions _options = null!;
    private FakeTimeProvider _time = null!;
    private int _seq;

    [SetUp]
    public void SetUp()
    {
        _discord = Substitute.For<IDiscordRestClient>();
        _options = new DiscordOptions { LogChannel = LogChannel };
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(1UL);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
    }

    private IOptions<DiscordOptions> Options_() => Options.Create(_options);

    private async Task<User> AddUser(long? discordId = null)
    {
        var n = ++_seq;
        var user = User.Seed(userName: $"user{n}", discordUser: discordId is { } id ? GrifballWebApp.Domain.Players.DiscordUser.FromDiscord(id, $"disc{n}") : null);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    /// <summary>Real users' ids, two a side: MatchedPlayer.UserID is a foreign key, so a match's players must exist.</summary>
    private async Task<(int[] Home, int[] Away)> SeedMatchPlayers()
    {
        var home = new[] { (await AddUser()).Id, (await AddUser()).Id };
        var away = new[] { (await AddUser()).Id, (await AddUser()).Id };
        return (home, away);
    }

    private async Task<MatchedMatch> SeedMatch(IEnumerable<int> home, IEnumerable<int> away, ulong? threadId = null)
    {
        var match = MatchedMatch.Start(home, away);
        if (threadId is { } thread)
            match.AttachThread(thread);
        Context.MatchedMatches.Add(match);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return match;
    }

    private static OutboxMessage Message<T>(string kind, T payload, int attempts = 0, object? result = null) => new()
    {
        Kind = kind,
        Payload = JsonSerializer.Serialize(payload, OutboxJson.Options),
        Attempts = attempts,
        Result = result is null ? null : JsonSerializer.Serialize(result, OutboxJson.Options),
    };

    private static IDiscordRestMessage FakeMessage(ulong id, IDiscordEmbed? embed = null)
    {
        var m = Substitute.For<IDiscordRestMessage>();
        m.Id.Returns(id);
        m.Author.Id.Returns(1UL);
        m.Embeds.Returns(embed is null ? [] : new[] { embed });
        return m;
    }

    private static IDiscordEmbed FakeEmbed(string? title = null, string? description = null, params (string Name, string Value)[] fields)
    {
        // Built before e.Fields.Returns(...) is called: nested Substitute.For/.Returns() calls made while evaluating
        // that argument would otherwise consume the "last call" e.Fields itself just registered.
        var fieldSubstitutes = new IDiscordEmbedField[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            var field = Substitute.For<IDiscordEmbedField>();
            field.Name.Returns(fields[i].Name);
            field.Value.Returns(fields[i].Value);
            fieldSubstitutes[i] = field;
        }

        var e = Substitute.For<IDiscordEmbed>();
        e.Title.Returns(title);
        e.Description.Returns(description);
        e.Fields.Returns(fieldSubstitutes);
        return e;
    }

    private static MatchCreatedResult? CreatedResultOf(OutboxMessage message) =>
        message.Result is null ? null : JsonSerializer.Deserialize<MatchCreatedResult>(message.Result, OutboxJson.Options);

    // ---- MatchCreatedPostHandler ----

    [Test]
    public async Task MatchCreatedPostHandler_NewMatch_PostsAndSavesTheResult()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away);
        var posted = FakeMessage(50UL);
        _discord.SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var handler = new MatchCreatedPostHandler(_discord, Options_(), NullLogger<MatchCreatedPostHandler>.Instance);

        var message = Message(MatchOutbox.CreatedPost, new MatchCreatedPayload(match.Id, false));

        var outcome = await handler.HandleAsync(message, Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            // MatchThreadCreateHandler reads this to find the post to thread from
            Assert.That(CreatedResultOf(message), Is.EqualTo(new MatchCreatedResult(50UL, false)), "The new post, sent by this try");
        }
        await _discord.Received(1).SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchCreatedPostHandler_MayBePosted_FindsAnExistingPostInstead()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away);
        var existing = FakeMessage(60UL, FakeEmbed("Match Created", fields: [("Match ID", match.Id.ToString())]));
        _discord.GetMessagesAsync(LogChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { existing }.ToAsyncEnumerable());
        var handler = new MatchCreatedPostHandler(_discord, Options_(), NullLogger<MatchCreatedPostHandler>.Instance);

        var message = Message(MatchOutbox.CreatedPost, new MatchCreatedPayload(match.Id, true));

        var outcome = await handler.HandleAsync(message, Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(CreatedResultOf(message), Is.EqualTo(new MatchCreatedResult(60UL, true)), "The earlier post, found");
        }
        await _discord.DidNotReceive().SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchThreadCreateHandler ----

    [Test]
    public async Task MatchThreadCreateHandler_AlreadyHasAThread_IsDoneAndMakesNoCalls()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away, threadId: 777UL);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(_discord.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task MatchThreadCreateHandler_NoCreatedPostResult_Throws()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        Assert.That(async () => await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), Context, CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("no Match Created post"));
    }

    [Test]
    public async Task MatchThreadCreateHandler_MakesTheThread_AndSavesItOnTheMatch()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away);
        await EnqueueCreatedPostResult(match.Id, messageId: 50UL, found: false);
        var thread = Substitute.For<IDiscordGuildThread>();
        thread.Id.Returns(900UL);
        _discord.CreateGuildThreadAsync(LogChannel, 50UL, Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(thread);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), Context, CancellationToken.None);

        Assert.That((await Context.MatchedMatches.SingleAsync()).ThreadID, Is.EqualTo(900UL));
    }

    [Test]
    public async Task MatchThreadCreateHandler_PostWasFound_AlreadyHasAThread_IsTaken()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away);
        await EnqueueCreatedPostResult(match.Id, messageId: 50UL, found: true);
        var postedThread = Substitute.For<IDiscordGuildThread>();
        postedThread.Id.Returns(901UL);
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.StartedThread.Returns(postedThread);
        _discord.GetMessageAsync(LogChannel, 50UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Context.MatchedMatches.SingleAsync()).ThreadID, Is.EqualTo(901UL));
            Assert.That(_discord.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.CreateGuildThreadAsync)), Is.False);
        }
    }

    private async Task EnqueueCreatedPostResult(int matchId, ulong messageId, bool found)
    {
        var message = new OutboxMessage
        {
            Kind = MatchOutbox.CreatedPost,
            Payload = "{}",
            GroupKey = MatchOutbox.Group(matchId),
            Sequence = 0,
            Status = OutboxStatus.Sent,
            Result = JsonSerializer.Serialize(new MatchCreatedResult(messageId, found), OutboxJson.Options),
        };
        Context.Outbox.Add(message);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // ---- MatchThreadInviteHandler ----

    [Test]
    public async Task MatchThreadInviteHandler_AddsEveryPlayerWithDiscord()
    {
        var withDiscord1 = await AddUser(5001);
        var withDiscord2 = await AddUser(5002);
        var noDiscord = await AddUser();
        var match = await SeedMatch([withDiscord1.Id, noDiscord.Id], [withDiscord2.Id], threadId: 777UL);
        var handler = new MatchThreadInviteHandler(_discord, Options_(), NullLogger<MatchThreadInviteHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.ThreadInvite, new MatchPayload(match.Id)), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            await _discord.Received(1).AddGuildThreadUserAsync(777UL, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
            await _discord.Received(1).AddGuildThreadUserAsync(777UL, 5002UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        }
        // Those two and no one else: the player without Discord isn't invited under any id
        await _discord.Received(2).AddGuildThreadUserAsync(777UL, Arg.Any<ulong>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchThreadInviteHandler_OneAddFails_IsLogged_AndDoesNotStopTheOthers()
    {
        var first = await AddUser(5001);
        var second = await AddUser(5002);
        var match = await SeedMatch([first.Id], [second.Id], threadId: 777UL);
        _discord.AddGuildThreadUserAsync(777UL, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("gone"));
        var handler = new MatchThreadInviteHandler(_discord, Options_(), NullLogger<MatchThreadInviteHandler>.Instance);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadInvite, new MatchPayload(match.Id)), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            await _discord.Received(1).AddGuildThreadUserAsync(777UL, 5002UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task MatchThreadInviteHandler_NoThread_Throws()
    {
        var user = await AddUser(5001);
        var match = await SeedMatch([user.Id], []);
        var handler = new MatchThreadInviteHandler(_discord, Options_(), NullLogger<MatchThreadInviteHandler>.Instance);

        Assert.That(async () => await handler.HandleAsync(Message(MatchOutbox.ThreadInvite, new MatchPayload(match.Id)), Context, CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("no thread"));
    }

    // ---- MatchVotePostHandler ----

    [Test]
    public async Task MatchVotePostHandler_PostsTheVoteMessage_AndSavesItsId()
    {
        var home = await AddUser();
        var away = await AddUser();
        var match = await SeedMatch([home.Id], [away.Id], threadId: 777UL);
        var posted = FakeMessage(70UL);
        _discord.SendMessageAsync(777UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var handler = new MatchVotePostHandler(_discord, Options_(), NullLogger<MatchVotePostHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.VotePost, new MatchPayload(match.Id)), Context, CancellationToken.None);

        Assert.That((await Context.MatchedMatches.SingleAsync()).VoteMessageID, Is.EqualTo(70UL));
    }

    [Test]
    public async Task MatchVotePostHandler_AlreadyPosted_IsDoneWithoutPostingAgain()
    {
        var home = await AddUser();
        var away = await AddUser();
        var match = await SeedMatch([home.Id], [away.Id], threadId: 777UL);
        match = await Context.MatchedMatches.SingleAsync();
        match.PostVoteMessage(70UL);
        await Context.SaveChangesAsync();
        var handler = new MatchVotePostHandler(_discord, Options_(), NullLogger<MatchVotePostHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.VotePost, new MatchPayload(match.Id)), Context, CancellationToken.None);

        await _discord.DidNotReceive().SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchEndedPostHandler ----

    [Test]
    public async Task MatchEndedPostHandler_Posts()
    {
        var handler = new MatchEndedPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.EndedPost, new MatchEndedPayload(5, 0, "5m", "a", "b")), Context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchEndedPostHandler_RetryWithAnExistingPost_DoesNotPostAgain()
    {
        (string Name, string Value)[] matchIdField = [("Match ID", "5")];
        var existing = FakeMessage(80UL, FakeEmbed("Match Ended", fields: matchIdField));
        _discord.GetMessagesAsync(LogChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { existing }.ToAsyncEnumerable());
        var handler = new MatchEndedPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.EndedPost, new MatchEndedPayload(5, 0, "5m", "a", "b"), attempts: 2), Context, CancellationToken.None);

        await _discord.DidNotReceive().SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchCanceledPostHandler ----

    [Test]
    public async Task MatchCanceledPostHandler_Posts()
    {
        var handler = new MatchCanceledPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.CanceledPost, new MatchCanceledPayload(5, false)), Context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchCanceledPostHandler_RetryWithAnExistingPost_DoesNotPostAgain()
    {
        var existing = FakeMessage(90UL, FakeEmbed("Match Canceled", "Match #5 has been canceled."));
        _discord.GetMessagesAsync(LogChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { existing }.ToAsyncEnumerable());
        var handler = new MatchCanceledPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.CanceledPost, new MatchCanceledPayload(5, false), attempts: 2), Context, CancellationToken.None);

        await _discord.DidNotReceive().SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchThreadCloseHandler ----

    [Test]
    public async Task MatchThreadCloseHandler_FirstCall_SendsTheNoticeAndWaitsToDelete()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away, threadId: 777UL);
        var handler = new MatchThreadCloseHandler(_discord, _time);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id)), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.ContinueAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime + MatchOutbox.ThreadDeleteDelay));
            await _discord.Received(1).SendMessageAsync(777UL, Arg.Is<MessageProperties>(m => m.Content == MatchThreadCloseHandler.Notice), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task MatchThreadCloseHandler_SecondCall_DeletesTheThread()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away, threadId: 777UL);
        var handler = new MatchThreadCloseHandler(_discord, _time);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id), result: new ThreadCloseResult(true)), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            await _discord.Received(1).DeleteChannelAsync(777UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task MatchThreadCloseHandler_NoThread_IsDoneWithoutCallingDiscord()
    {
        var (home, away) = await SeedMatchPlayers();
        var match = await SeedMatch(home, away);
        var handler = new MatchThreadCloseHandler(_discord, _time);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id)), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(_discord.ReceivedCalls(), Is.Empty);
        }
    }

    // ---- MatchRequeue (through MatchThreadCreateHandler's OnDeadLetteredAsync) ----

    [Test]
    public async Task OnDeadLetteredAsync_ActiveMatch_CancelsItAndRequeuesItsPlayers()
    {
        var home = await AddUser();
        var away = await AddUser();
        var match = await SeedMatch([home.Id], [away.Id]);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        await handler.OnDeadLetteredAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), Context, new ContextOutbox(Context), CancellationToken.None);
        await Context.SaveChangesAsync();

        var requeued = await Context.QueuedPlayer.Select(x => x.UserID).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Context.MatchedMatches.SingleAsync()).Status, Is.EqualTo(MatchedMatchStatus.Canceled));
            Assert.That(requeued, Is.EquivalentTo(new[] { home.Id, away.Id }));
        }
    }

    [Test]
    public async Task OnDeadLetteredAsync_MatchAlreadyFinished_DoesNothing()
    {
        var home = await AddUser();
        var away = await AddUser();
        var match = await SeedMatch([home.Id], [away.Id]);
        match = await Context.MatchedMatches.SingleAsync();
        match.Cancel();
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        await handler.OnDeadLetteredAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), Context, new ContextOutbox(Context), CancellationToken.None);

        Assert.That(await Context.QueuedPlayer.AnyAsync(), Is.False);
    }
}
