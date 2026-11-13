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
public class MatchOutboxHandlersTests
{
    private const ulong LogChannel = 222UL;
    private static readonly int[] HomeIds = [1, 2];
    private static readonly int[] AwayIds = [3, 4];

    private string _path = null!;
    private GrifballContext _context = null!;
    private IDiscordRestClient _discord = null!;
    private DiscordOptions _options = null!;
    private FakeTimeProvider _time = null!;
    private int _seq;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-match-outbox-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        try
        {
            await _context.Database.EnsureCreatedAsync();
        }
        catch
        {
            await _context.DisposeAsync();
            if (File.Exists(_path))
                File.Delete(_path);
            throw;
        }
        _discord = Substitute.For<IDiscordRestClient>();
        _options = new DiscordOptions { LogChannel = LogChannel };
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(1UL);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private IOptions<DiscordOptions> Options_() => Options.Create(_options);

    private async Task<User> AddUser(long? discordId = null)
    {
        var n = ++_seq;
        var user = User.Seed(userName: $"user{n}", discordUser: discordId is { } id ? GrifballWebApp.Domain.Players.DiscordUser.FromDiscord(id, $"disc{n}") : null);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<MatchedMatch> SeedMatch(IEnumerable<int> home, IEnumerable<int> away, ulong? threadId = null)
    {
        var match = MatchedMatch.Start(home, away);
        if (threadId is { } thread)
            match.AttachThread(thread);
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
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
        var e = Substitute.For<IDiscordEmbed>();
        e.Title.Returns(title);
        e.Description.Returns(description);
        e.Fields.Returns(fields.Select(f =>
        {
            var field = Substitute.For<IDiscordEmbedField>();
            field.Name.Returns(f.Name);
            field.Value.Returns(f.Value);
            return field;
        }).ToArray());
        return e;
    }

    // ---- MatchCreatedPostHandler ----

    [Test]
    public async Task MatchCreatedPostHandler_NewMatch_PostsAndSavesTheResult()
    {
        var match = await SeedMatch(HomeIds, AwayIds);
        var posted = FakeMessage(50UL);
        _discord.SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var handler = new MatchCreatedPostHandler(_discord, Options_(), NullLogger<MatchCreatedPostHandler>.Instance);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.CreatedPost, new MatchCreatedPayload(match.Id, false)), _context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.Received(1).SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchCreatedPostHandler_MayBePosted_FindsAnExistingPostInstead()
    {
        var match = await SeedMatch(HomeIds, AwayIds);
        var existing = FakeMessage(60UL, FakeEmbed("Match Created", fields: [("Match ID", match.Id.ToString())]));
        _discord.GetMessagesAsync(LogChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { existing }.ToAsyncEnumerable());
        var handler = new MatchCreatedPostHandler(_discord, Options_(), NullLogger<MatchCreatedPostHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.CreatedPost, new MatchCreatedPayload(match.Id, true)), _context, CancellationToken.None);

        await _discord.DidNotReceive().SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchThreadCreateHandler ----

    [Test]
    public async Task MatchThreadCreateHandler_AlreadyHasAThread_IsDoneAndMakesNoCalls()
    {
        var match = await SeedMatch(HomeIds, AwayIds, threadId: 777UL);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(_discord.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task MatchThreadCreateHandler_NoCreatedPostResult_Throws()
    {
        var match = await SeedMatch(HomeIds, AwayIds);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        Assert.That(async () => await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), _context, CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("no Match Created post"));
    }

    [Test]
    public async Task MatchThreadCreateHandler_MakesTheThread_AndSavesItOnTheMatch()
    {
        var match = await SeedMatch(HomeIds, AwayIds);
        await EnqueueCreatedPostResult(match.Id, messageId: 50UL, found: false);
        var thread = Substitute.For<IDiscordGuildThread>();
        thread.Id.Returns(900UL);
        _discord.CreateGuildThreadAsync(LogChannel, 50UL, Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(thread);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), _context, CancellationToken.None);

        Assert.That((await _context.MatchedMatches.SingleAsync()).ThreadID, Is.EqualTo(900UL));
    }

    [Test]
    public async Task MatchThreadCreateHandler_PostWasFound_AlreadyHasAThread_IsTaken()
    {
        var match = await SeedMatch(HomeIds, AwayIds);
        await EnqueueCreatedPostResult(match.Id, messageId: 50UL, found: true);
        var postedThread = Substitute.For<IDiscordGuildThread>();
        postedThread.Id.Returns(901UL);
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.StartedThread.Returns(postedThread);
        _discord.GetMessageAsync(LogChannel, 50UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _context.MatchedMatches.SingleAsync()).ThreadID, Is.EqualTo(901UL));
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
        _context.Outbox.Add(message);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
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

        await handler.HandleAsync(Message(MatchOutbox.ThreadInvite, new MatchPayload(match.Id)), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            await _discord.Received(1).AddGuildThreadUserAsync(777UL, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
            await _discord.Received(1).AddGuildThreadUserAsync(777UL, 5002UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        }
        await _discord.DidNotReceive().AddGuildThreadUserAsync(777UL, 0UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchThreadInviteHandler_OneAddFails_IsLogged_AndDoesNotStopTheOthers()
    {
        var first = await AddUser(5001);
        var second = await AddUser(5002);
        var match = await SeedMatch([first.Id], [second.Id], threadId: 777UL);
        _discord.AddGuildThreadUserAsync(777UL, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("gone"));
        var handler = new MatchThreadInviteHandler(_discord, Options_(), NullLogger<MatchThreadInviteHandler>.Instance);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadInvite, new MatchPayload(match.Id)), _context, CancellationToken.None);

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

        Assert.That(async () => await handler.HandleAsync(Message(MatchOutbox.ThreadInvite, new MatchPayload(match.Id)), _context, CancellationToken.None),
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

        await handler.HandleAsync(Message(MatchOutbox.VotePost, new MatchPayload(match.Id)), _context, CancellationToken.None);

        Assert.That((await _context.MatchedMatches.SingleAsync()).VoteMessageID, Is.EqualTo(70UL));
    }

    [Test]
    public async Task MatchVotePostHandler_AlreadyPosted_IsDoneWithoutPostingAgain()
    {
        var home = await AddUser();
        var away = await AddUser();
        var match = await SeedMatch([home.Id], [away.Id], threadId: 777UL);
        match = await _context.MatchedMatches.SingleAsync();
        match.PostVoteMessage(70UL);
        await _context.SaveChangesAsync();
        var handler = new MatchVotePostHandler(_discord, Options_(), NullLogger<MatchVotePostHandler>.Instance);

        await handler.HandleAsync(Message(MatchOutbox.VotePost, new MatchPayload(match.Id)), _context, CancellationToken.None);

        await _discord.DidNotReceive().SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchEndedPostHandler ----

    [Test]
    public async Task MatchEndedPostHandler_Posts()
    {
        var handler = new MatchEndedPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.EndedPost, new MatchEndedPayload(5, 0, "5m", "a", "b")), _context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchEndedPostHandler_RetryWithAnExistingPost_DoesNotPostAgain()
    {
        (string Name, string Value)[] matchIdField = [("Match ID", "5")];
        var existing = FakeMessage(80UL, FakeEmbed("Match Ended", fields: matchIdField));
        _discord.GetMessagesAsync(LogChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { existing }.ToAsyncEnumerable());
        var handler = new MatchEndedPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.EndedPost, new MatchEndedPayload(5, 0, "5m", "a", "b"), attempts: 2), _context, CancellationToken.None);

        await _discord.DidNotReceive().SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchCanceledPostHandler ----

    [Test]
    public async Task MatchCanceledPostHandler_Posts()
    {
        var handler = new MatchCanceledPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.CanceledPost, new MatchCanceledPayload(5, false)), _context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchCanceledPostHandler_RetryWithAnExistingPost_DoesNotPostAgain()
    {
        var existing = FakeMessage(90UL, FakeEmbed("Match Canceled", "Match #5 has been canceled."));
        _discord.GetMessagesAsync(LogChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { existing }.ToAsyncEnumerable());
        var handler = new MatchCanceledPostHandler(_discord, Options_());

        await handler.HandleAsync(Message(MatchOutbox.CanceledPost, new MatchCanceledPayload(5, false), attempts: 2), _context, CancellationToken.None);

        await _discord.DidNotReceive().SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    // ---- MatchThreadCloseHandler ----

    [Test]
    public async Task MatchThreadCloseHandler_FirstCall_SendsTheNoticeAndWaitsToDelete()
    {
        var match = await SeedMatch(HomeIds, AwayIds, threadId: 777UL);
        var handler = new MatchThreadCloseHandler(_discord, _time);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id)), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.ContinueAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime + MatchOutbox.ThreadDeleteDelay));
            await _discord.Received(1).SendMessageAsync(777UL, Arg.Is<MessageProperties>(m => m.Content == MatchThreadCloseHandler.Notice), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task MatchThreadCloseHandler_SecondCall_DeletesTheThread()
    {
        var match = await SeedMatch(HomeIds, AwayIds, threadId: 777UL);
        var handler = new MatchThreadCloseHandler(_discord, _time);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id), result: new ThreadCloseResult(true)), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            await _discord.Received(1).DeleteChannelAsync(777UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task MatchThreadCloseHandler_NoThread_IsDoneWithoutCallingDiscord()
    {
        var match = await SeedMatch(HomeIds, AwayIds);
        var handler = new MatchThreadCloseHandler(_discord, _time);

        var outcome = await handler.HandleAsync(Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id)), _context, CancellationToken.None);

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

        await handler.OnDeadLetteredAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), _context, new ContextOutbox(_context), CancellationToken.None);
        await _context.SaveChangesAsync();

        var requeued = await _context.QueuedPlayer.Select(x => x.UserID).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _context.MatchedMatches.SingleAsync()).Status, Is.EqualTo(MatchedMatchStatus.Canceled));
            Assert.That(requeued, Is.EquivalentTo(new[] { home.Id, away.Id }));
        }
    }

    [Test]
    public async Task OnDeadLetteredAsync_MatchAlreadyFinished_DoesNothing()
    {
        var home = await AddUser();
        var away = await AddUser();
        var match = await SeedMatch([home.Id], [away.Id]);
        match = await _context.MatchedMatches.SingleAsync();
        match.Cancel();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var handler = new MatchThreadCreateHandler(_discord, Options_(), NullLogger<MatchThreadCreateHandler>.Instance);

        await handler.OnDeadLetteredAsync(Message(MatchOutbox.ThreadCreate, new MatchPayload(match.Id)), _context, new ContextOutbox(_context), CancellationToken.None);

        Assert.That(await _context.QueuedPlayer.AnyAsync(), Is.False);
    }
}
