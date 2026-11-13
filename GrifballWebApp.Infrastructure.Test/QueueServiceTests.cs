using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// QueueService.Go, on a real (temp-file SQLite) GrifballContext with the real QueueRepository and ContextOutbox: no
/// database server, host or network. Vote-decided and reported-match endings (FinishMatch, HandleWinnersAndLosers)
/// need a Halo match correlation this doesn't set up, and stay uncovered here; everything else of a tick is.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueServiceTests
{
    private const ulong QueueChannel = 111UL;
    private const ulong BotId = 1UL;
    private static readonly ulong[] DeletedMessageIds = [11, 14];
    private static readonly string[] MatchAnnouncementSteps = [MatchOutbox.CreatedPost, MatchOutbox.ThreadCreate, MatchOutbox.ThreadInvite, MatchOutbox.VotePost];

    private string _path = null!;
    private GrifballContext _context = null!;
    private IDiscordRestClient _discord = null!;
    private IQueueNotifier _notifier = null!;
    private DiscordOptions _options = null!;
    private int _seq;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-queue-service-{Guid.NewGuid():N}.db");
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
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(BotId);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        var sent = Substitute.For<IDiscordRestMessage>();
        sent.Id.Returns(77UL);
        _discord.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(sent);

        _notifier = Substitute.For<IQueueNotifier>();
        _options = new DiscordOptions { QueueChannel = QueueChannel, MatchPlayers = 2 };
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private QueueService Service(IQueueRepository? repository = null, IDataPullService? dataPullService = null) => new(
        NullLogger<QueueService>.Instance, Options.Create(_options), repository ?? new QueueRepository(_context), _discord, _context,
        dataPullService ?? Substitute.For<IDataPullService>(), new ContextOutbox(_context), _notifier);

    private async Task<List<User>> SeedUsers(int count)
    {
        var users = new List<User>();
        for (var i = 0; i < count; i++)
        {
            var n = ++_seq;
            users.Add(User.Seed(userName: $"user{n}", xboxUser: GrifballWebApp.Domain.Players.XboxUser.FromXbox(1000 + n, $"gt{n}")));
        }
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();
        return users;
    }

    private async Task QueuePlayers(IEnumerable<User> users)
    {
        _context.QueuedPlayer.AddRange(users.Select((u, i) => QueuedPlayer.Seed(u.Id, DateTime.UtcNow.AddMinutes(-10 + i))));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task<MatchedMatch> SeedActiveMatch(IEnumerable<User> home, IEnumerable<User> away, ulong? threadId = null)
    {
        var match = MatchedMatch.Start(home.Select(u => u.Id), away.Select(u => u.Id));
        if (threadId is { } thread)
        {
            match.AttachThread(thread);
            match.PostVoteMessage(4242UL);
        }
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return match;
    }

    private int[] StatusChanged() => _notifier.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IQueueNotifier.SendStatusChanged))
        .SelectMany(c => (IReadOnlyCollection<int>)c.GetArguments()[0]!)
        .Order().ToArray();

    private static IDiscordRestMessage FakeMessage(ulong id, ulong author, string title, DateTimeOffset createdAt)
    {
        var m = Substitute.For<IDiscordRestMessage>();
        m.Id.Returns(id);
        m.Author.Id.Returns(author);
        m.CreatedAt.Returns(createdAt);
        var e = Substitute.For<IDiscordEmbed>();
        e.Title.Returns(title);
        m.Embeds.Returns([e]);
        return m;
    }

    [Test]
    public void Constructor_NoQueueChannelConfigured_Throws()
    {
        var options = Options.Create(new DiscordOptions());
        Assert.That(() => new QueueService(NullLogger<QueueService>.Instance, options, new QueueRepository(_context), _discord, _context,
            Substitute.For<IDataPullService>(), new ContextOutbox(_context), _notifier), Throws.Exception.With.Message.EqualTo("Discord:QueueChannel is not set"));
    }

    [Test]
    public async Task Go_NoExistingMessage_SendsANewOne()
    {
        await Service().Go(CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(QueueChannel, Arg.Is<MessageProperties>(m => m.Content == QueueService.QueueEmbedTitle),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Go_OneExistingMessage_ModifiesItInstead()
    {
        var existing = FakeMessage(10UL, BotId, QueueService.QueueEmbedTitle, DateTimeOffset.UtcNow);
        _discord.GetMessagesAsync(QueueChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => new[] { existing }.ToAsyncEnumerable());

        await Service().Go(CancellationToken.None);

        var modify = _discord.ReceivedCalls().SingleOrDefault(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(modify, Is.Not.Null);
            Assert.That(modify!.GetArguments()[0], Is.EqualTo(QueueChannel));
            Assert.That(modify.GetArguments()[1], Is.EqualTo(10UL));
        }
        await _discord.DidNotReceive().SendMessageAsync(QueueChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Go_SeveralExistingMessages_DeletesAllButTheNewest()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            FakeMessage(11UL, BotId, "Late League Queue", now.AddMinutes(-5)),
            FakeMessage(10UL, BotId, "Matchmaking Queue", now.AddMinutes(-1)),
            FakeMessage(12UL, 2UL, "Matchmaking Queue", now),
            FakeMessage(14UL, BotId, "Matchmaking Queue", now.AddMinutes(-9)),
        };
        _discord.GetMessagesAsync(QueueChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => messages.ToAsyncEnumerable());

        await Service().Go(CancellationToken.None);

        await _discord.Received(1).DeleteMessagesAsync(QueueChannel, Arg.Is<IEnumerable<ulong>>(ids => ids.OrderBy(x => x).SequenceEqual(DeletedMessageIds)),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Go_EnoughPlayersQueued_CreatesAMatch_RemovesThemFromTheQueue_AndAnnouncesInOrder()
    {
        var players = await SeedUsers(2);
        await QueuePlayers(players);

        await Service().Go(CancellationToken.None);

        var match = await _context.MatchedMatches.SingleAsync();
        var queued = await _context.Outbox.OrderBy(x => x.Sequence).ToListAsync();
        var stillQueued = await _context.QueuedPlayer.ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stillQueued, Is.Empty);
            Assert.That(queued.Select(x => x.Kind), Is.EqualTo(MatchAnnouncementSteps));
            Assert.That(queued.Select(x => x.GroupKey), Is.All.EqualTo(MatchOutbox.Group(match.Id)));
        }
        Assert.That(StatusChanged(), Is.EqualTo(players.Select(x => x.Id).Order().ToArray()));
    }

    [Test]
    public async Task Go_FewerPlayersThanAMatchNeeds_QueuesNoMatch()
    {
        await QueuePlayers(await SeedUsers(1));

        await Service().Go(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.MatchedMatches.AnyAsync(), Is.False);
            Assert.That(await _context.QueuedPlayer.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Go_QueuedPlayerAndAnActiveMatch_TheQueueMessageShowsBoth_WithTheirRank()
    {
        var queued = await SeedUsers(1);
        await QueuePlayers(queued);
        _context.Ranks.Add(Rank.Create("Bronze", 0, "#CD7F32", "Starting out", []));
        await _context.SaveChangesAsync();
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedActiveMatch(home, away);

        await Service().Go(CancellationToken.None);

        var sent = _discord.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.SendMessageAsync)).GetArguments();
        var message = (MessageProperties)sent[1]!;
        var embeds = message.Embeds!.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embeds[0].Description, Does.Contain("1 players in queue"));
            Assert.That(embeds[0].Fields!.Single().Value, Does.Contain("[Bronze]"));
            Assert.That(embeds.Any(e => e.Title == "Active Matches"), Is.True);
            Assert.That(embeds.Any(e => e.Title == $"Match #{match.Id}"), Is.True);
        }
    }

    [Test]
    public async Task Go_ActiveMatchWithNoAnnouncementQueued_QueuesOneAsMayBePosted()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedActiveMatch(home, away);

        await Service().Go(CancellationToken.None);

        var queued = await _context.Outbox.Where(x => x.GroupKey == MatchOutbox.Group(match.Id)).OrderBy(x => x.Sequence).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(queued.Select(x => x.Kind), Is.EqualTo(MatchAnnouncementSteps));
            Assert.That(queued[0].Payload, Does.Contain("\"mayBePosted\":true"), "Found announced by an older version of the app, looked for again");
        }
    }

    [Test]
    public async Task Go_ActiveMatchAlreadyAnnounced_QueuesNothingMore()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        await SeedActiveMatch(home, away, threadId: 999UL);

        await Service().Go(CancellationToken.None);

        Assert.That(await _context.Outbox.AnyAsync(), Is.False, "Already announced (its vote message is posted): nothing queued again");
    }

    [Test]
    public async Task Go_EveryTick_SendsTheQueuesSize_EvenWhenNothingChanged()
    {
        var service = Service();

        await service.Go(CancellationToken.None);
        await service.Go(CancellationToken.None);

        await _notifier.Received(2).Send(0);
        Assert.That(StatusChanged(), Is.Empty);
    }

    [Test]
    public async Task Go_TheSaveFails_RollsBackTheMatch_ButStillSendsTheSize()
    {
        var players = await SeedUsers(2);
        await QueuePlayers(players);
        _context.SavingChanges += (_, _) =>
        {
            if (_context.ChangeTracker.Entries<OutboxMessage>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("database down");
        };

        Assert.That(async () => await Service().Go(CancellationToken.None), Throws.InstanceOf<InvalidOperationException>());

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.MatchedMatches.AnyAsync(), Is.False, "Rolled back along with the outbox save that failed");
            Assert.That(await _context.QueuedPlayer.CountAsync(), Is.EqualTo(2), "Still queued: the match was rolled back too");
        }
        await _notifier.Received(1).Send(2);
        Assert.That(StatusChanged(), Is.Empty, "Rolled back: nobody's place changed");
    }

    [Test]
    public async Task Go_TheQueuesSizeCannotBeRead_IsLogged_ButTheNudgesStillGo()
    {
        var repository = Substitute.For<IQueueRepository>();
        repository.GetQueuePlayersWithInfo(Arg.Any<CancellationToken>()).Returns([]);
        repository.GetActiveMatches(Arg.Any<CancellationToken>()).Returns([]);
        repository.Count(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));

        await Service(repository: repository).Go(CancellationToken.None);

        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
        await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(x => x.Count == 0));
    }

    [Test]
    public async Task UpdateThreadMessage_NoVotesYet_HasNoVotesEmbed()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedActiveMatch(home, away, threadId: 999UL);

        await Service().UpdateThreadMessage(match, _context, _discord);

        var call = _discord.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(call.GetArguments()[0], Is.EqualTo(999UL));
            Assert.That(call.GetArguments()[1], Is.EqualTo(4242UL));
        }
    }

    [Test]
    public async Task UpdateThreadMessage_NoThreadYet_DoesNothing()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = MatchedMatch.Start(home.Select(x => x.Id), away.Select(x => x.Id));
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesAsync();

        await Service().UpdateThreadMessage(match, _context, _discord);

        Assert.That(_discord.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task UpdateThreadMessage_NullMatch_DoesNothing()
    {
        await Service().UpdateThreadMessage(null, _context, _discord);

        Assert.That(_discord.ReceivedCalls(), Is.Empty);
    }
}
