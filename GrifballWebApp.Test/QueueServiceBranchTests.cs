using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Seeder;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

/// <summary>
/// Covers the QueueService branches that QueueServiceTests does not: vote based match endings, cancellation,
/// MMR streak handling, queue message cleanup / modification, thread user adds and UpdateThreadMessage.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueServiceBranchTests
{
    private const ulong QueueChannel = 111;
    private const ulong LogChannel = 222;
    private const ulong ThreadId = 999;
    private const ulong BotId = 1;

    private GrifballContext _context;
    private RecordingLogger<QueueService> _logger;
    private IDiscordRestClient _discordClient;
    private IDataPullService _dataPullService;
    private IDiscordGuildThread _thread;
    private DiscordOptions _options;
    private QueueService _service;
    private int _seq;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _logger = new RecordingLogger<QueueService>();
        _dataPullService = Substitute.For<IDataPullService>();
        _discordClient = Substitute.For<IDiscordRestClient>();

        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(BotId);
        _discordClient.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);

        var sent = Substitute.For<IDiscordRestMessage>();
        sent.Id.Returns(77ul);
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(sent);

        _thread = Substitute.For<IDiscordGuildThread>();
        _thread.Id.Returns(ThreadId);
        _discordClient.CreateGuildThreadAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_thread);
        _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());

        _options = new DiscordOptions
        {
            QueueChannel = QueueChannel,
            LogChannel = LogChannel,
            MatchPlayers = 4,
        };
        _service = new QueueService(_logger, Options.Create(_options), new QueueRepository(_context), _discordClient, _context, _dataPullService);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private GrifballContext Fresh() => _context.NewContextLike();

    private async Task<List<User>> SeedUsers(int count, Action<User>? configure = null, bool withDiscord = false)
    {
        var users = new List<User>();
        for (var i = 0; i < count; i++)
        {
            var n = ++_seq;
            var user = new User
            {
                UserName = $"user{n}",
                XboxUser = new XboxUser { XboxUserID = 1000 + n, Gamertag = $"gt{n}" },
                DiscordUser = withDiscord ? new DiscordUser { DiscordUserID = 5000 + n, DiscordUsername = $"disc{n}" } : null,
            };
            configure?.Invoke(user);
            users.Add(user);
        }
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();
        return users;
    }

    private async Task<MatchedMatch> SeedMatch(IEnumerable<User> home, IEnumerable<User> away, DateTime? startedAt = null, ulong? threadId = ThreadId)
    {
        var match = new MatchedMatch
        {
            HomeTeam = new MatchedTeam { Players = home.Select(u => new MatchedPlayer { UserID = u.Id }).ToList() },
            AwayTeam = new MatchedTeam { Players = away.Select(u => new MatchedPlayer { UserID = u.Id }).ToList() },
            StartedAt = startedAt ?? DateTime.UtcNow,
            ThreadID = threadId,
            VoteMessageID = threadId is null ? null : 4242,
        };
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    private async Task Vote(MatchedMatch match, MatchedPlayer player, WinnerVote vote)
    {
        _context.MatchedWinnerVotes.Add(new MatchedWinnerVote { MatchId = match.Id, MatchedPlayerId = player.Id, WinnerVote = vote });
        await _context.SaveChangesAsync();
    }

    private List<MessageProperties> SentTo(ulong channel) => _discordClient.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.SendMessageAsync))
        .Where(c => c.GetArguments()[0] is ulong ch && ch == channel)
        .Select(c => (MessageProperties)c.GetArguments()[1]!)
        .ToList();

    private static string Field(MessageProperties mp, string name) =>
        mp.Embeds!.Single().Fields!.Single(f => f.Name == name).Value!;

    [Test]
    public void Constructor_Throws_WhenQueueChannelNotSet()
    {
        var ex = Assert.Throws<Exception>(() => new QueueService(_logger, Options.Create(new DiscordOptions()), Substitute.For<IQueueRepository>(), _discordClient, _context, _dataPullService));
        Assert.That(ex!.Message, Is.EqualTo("Discord:QueueChannel is not set"));
    }

    [Test]
    public async Task Go_HomeMajorityVote_EndsMatch_AndAdjustsMmr()
    {
        // Padding users so that MatchedPlayer ids do not line up with user ids
        await SeedUsers(4);
        var home = await SeedUsers(2);
        var away = await SeedUsers(2);
        var match = await SeedMatch(home, away);
        await Vote(match, match.HomeTeam.Players[0], WinnerVote.Home);
        await Vote(match, match.HomeTeam.Players[1], WinnerVote.Home);
        await Vote(match, match.AwayTeam.Players[0], WinnerVote.Home);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var dbMatch = await fresh.MatchedMatches.SingleAsync();
        var homeIds = home.Select(x => x.Id).ToArray();
        var awayIds = away.Select(x => x.Id).ToArray();
        var winners = await fresh.Users.Where(x => homeIds.Contains(x.Id)).ToListAsync();
        var losers = await fresh.Users.Where(x => awayIds.Contains(x.Id)).ToListAsync();
        var log = SentTo(LogChannel).Single();
        var embed = log.Embeds!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(dbMatch.Active, Is.False, "Majority vote should end the match");
            Assert.That(dbMatch.MatchID, Is.Null, "Manually ended matches have no infinite match id");
            Assert.That(winners.Select(x => x.MMR), Is.All.EqualTo(1024), "KFactor 32 * 0.75 = 24 gained");
            Assert.That(winners.Select(x => x.Wins), Is.All.EqualTo(1));
            Assert.That(winners.Select(x => x.WinStreak), Is.All.EqualTo(1));
            Assert.That(losers.Select(x => x.MMR), Is.All.EqualTo(980), "KFactor 32 * 0.625 = 20 lost");
            Assert.That(losers.Select(x => x.Losses), Is.All.EqualTo(1));
            Assert.That(losers.Select(x => x.LossStreak), Is.All.EqualTo(1));
            Assert.That(embed.Title, Is.EqualTo("Match Ended"));
            Assert.That(embed.Description, Is.EqualTo($"Match #{match.Id} has ended. Team 0 is the winner"));
            Assert.That(Field(log, "Winning Team"), Is.EqualTo("0"));
            Assert.That(Field(log, "Duration"), Is.EqualTo("Unknown"));
            Assert.That(Field(log, "Home Team MMR Changes"), Does.Contain("1000 -> 1024 (+24)"));
            Assert.That(Field(log, "Away Team MMR Changes"), Does.Contain("1000 -> 980 (-20)"));
        });
        Assert.That(SentTo(ThreadId).Single().Content, Does.StartWith("Match Completed."), "Thread should be told it will be deleted");
    }

    [Test]
    public async Task Go_AwayMajorityVote_AwayTeamWins_AndShowsMmrDelta()
    {
        // No padding: matched player ids equal user ids here, so the MMR change text resolves the old value.
        var home = await SeedUsers(2);
        var away = await SeedUsers(2);
        var match = await SeedMatch(home, away);
        foreach (var p in match.HomeTeam.Players.Concat(match.AwayTeam.Players).Take(3))
            await Vote(match, p, WinnerVote.Away);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var awayIds = away.Select(x => x.Id).ToArray();
        var awayUsers = await fresh.Users.Where(x => awayIds.Contains(x.Id)).ToListAsync();
        var log = SentTo(LogChannel).Single();
        Assert.Multiple(() =>
        {
            Assert.That(awayUsers.Select(x => x.Wins), Is.All.EqualTo(1));
            Assert.That(log.Embeds!.Single().Description, Does.EndWith("Team 1 is the winner"));
            Assert.That(Field(log, "Away Team MMR Changes"), Does.Contain("1000 -> 1024 (+24)"));
            Assert.That(Field(log, "Home Team MMR Changes"), Does.Contain("1000 -> 980 (-20)"));
            Assert.That(Field(log, "Home Team MMR Changes").Split('\n'), Has.Length.EqualTo(2), "One line per player");
        });
    }

    [Test]
    public async Task Go_WinAndLossStreaks_ApplyBonusAndClamp()
    {
        var home = await SeedUsers(2, u => { u.WinStreak = 2; });
        home[1].WinStreak = 10; // way past threshold -> capped at MaxBonus
        var away = await SeedUsers(2, u => { u.Losses = 5; u.LossStreak = 2; });
        away[1].MMR = 10;
        await _context.SaveChangesAsync();
        var match = await SeedMatch(home, away);
        foreach (var p in match.HomeTeam.Players.Concat(match.AwayTeam.Players).Take(3))
            await Vote(match, p, WinnerVote.Home);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var h0 = await fresh.Users.SingleAsync(x => x.Id == home[0].Id);
        var h1 = await fresh.Users.SingleAsync(x => x.Id == home[1].Id);
        var a0 = await fresh.Users.SingleAsync(x => x.Id == away[0].Id);
        var a1 = await fresh.Users.SingleAsync(x => x.Id == away[1].Id);
        Assert.Multiple(() =>
        {
            Assert.That(h0.WinStreak, Is.EqualTo(3));
            Assert.That(h0.MMR, Is.EqualTo(1000 + 24 + 10), "Streak of 3 hits the threshold: +BonusPerWin");
            Assert.That(h1.MMR, Is.EqualTo(1000 + 24 + 20), "Bonus is capped at MaxBonus");
            Assert.That(a0.LossStreak, Is.EqualTo(3));
            Assert.That(a0.WinStreak, Is.EqualTo(0));
            Assert.That(a0.MMR, Is.EqualTo(1000 - 20 - 10), "LossStreak of 3 hits the threshold: mmrLoss + PenaltyPerLoss");
            Assert.That(a1.MMR, Is.EqualTo(0), "MMR is clamped at zero");
        });
    }

    [Test]
    public async Task Go_CancelMajorityVote_CancelsWithoutChangingMmr()
    {
        var home = await SeedUsers(2);
        var away = await SeedUsers(2);
        var match = await SeedMatch(home, away);
        foreach (var p in match.HomeTeam.Players.Concat(match.AwayTeam.Players).Take(3))
            await Vote(match, p, WinnerVote.Cancel);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var dbMatch = await fresh.MatchedMatches.SingleAsync();
        var users = await fresh.Users.ToListAsync();
        var log = SentTo(LogChannel).Single().Embeds!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(dbMatch.Active, Is.False);
            Assert.That(users.Select(x => x.MMR), Is.All.EqualTo(1000));
            Assert.That(users.Select(x => x.Wins + x.Losses), Is.All.EqualTo(0));
            Assert.That(log.Title, Is.EqualTo("Match Canceled"));
            Assert.That(log.Description, Is.EqualTo($"Match #{match.Id} has been canceled."));
            Assert.That(log.Fields!.Single().Value, Is.EqualTo(match.Id.ToString()));
        });
        Assert.That(SentTo(ThreadId), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Go_VotesOfKickedPlayers_AreIgnored()
    {
        var home = await SeedUsers(2);
        var away = await SeedUsers(2);
        var match = await SeedMatch(home, away);
        match.HomeTeam.Players[0].Kicked = true;
        match.HomeTeam.Players[1].Kicked = true;
        await _context.SaveChangesAsync();
        // 2 active players => majority is (0 + 2) / 2 = 1, so 2 votes are needed. Only one active player votes.
        await Vote(match, match.HomeTeam.Players[0], WinnerVote.Away);
        await Vote(match, match.HomeTeam.Players[1], WinnerVote.Away);
        await Vote(match, match.AwayTeam.Players[0], WinnerVote.Away);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        Assert.That((await fresh.MatchedMatches.SingleAsync()).Active, Is.True);
        Assert.That(SentTo(LogChannel), Is.Empty);
    }

    [Test]
    public async Task Go_MatchWithStartTimeInTheFuture_IsNotCanceled()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        await SeedMatch(home, away, startedAt: DateTime.UtcNow.AddHours(3), threadId: null);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        Assert.That((await fresh.MatchedMatches.SingleAsync()).Active, Is.True);
        Assert.That(SentTo(LogChannel), Is.Empty);
    }

    [Test]
    public async Task Go_StaleMatchStartedHoursAgo_IsCanceled()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        await SeedMatch(home, away, startedAt: DateTime.UtcNow.AddHours(-3));
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        Assert.That((await fresh.MatchedMatches.SingleAsync()).Active, Is.False);
    }

    [Test]
    public async Task Go_CancelVote_ThreadMessageFailure_IsLoggedAndSwallowed()
    {
        _discordClient.SendMessageAsync(ThreadId, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no access"));
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedMatch(home, away);
        await Vote(match, match.HomeTeam.Players[0], WinnerVote.Cancel);
        await Vote(match, match.AwayTeam.Players[0], WinnerVote.Cancel);
        _context.ChangeTracker.Clear();

        await Assert.DoesNotThrowAsync(() => _service.Go(CancellationToken.None));

        var warning = _logger.Entries.Single(x => x.Level == LogLevel.Warning);
        Assert.Multiple(() =>
        {
            Assert.That(warning.Message, Is.EqualTo($"Failed to send message to thread {ThreadId}"));
            Assert.That(warning.Exception, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task Go_RemovesQueuedPlayersThatAreInAnActiveMatch()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var other = await SeedUsers(1);
        await SeedMatch(home, away);
        _context.QueuedPlayer.AddRange(new QueuedPlayer { UserID = home[0].Id }, new QueuedPlayer { UserID = other[0].Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        Assert.That(await fresh.QueuedPlayer.Select(x => x.UserID).ToListAsync(), Is.EqualTo(new[] { other[0].Id }));
    }

    [Test]
    public async Task Go_ExistingQueueMessages_DeletesDuplicatesAndModifiesNewest()
    {
        var now = DateTimeOffset.UtcNow;
        IDiscordRestMessage Msg(ulong id, ulong author, string title, int minutesAgo)
        {
            var m = Substitute.For<IDiscordRestMessage>();
            m.Id.Returns(id);
            m.Author.Id.Returns(author);
            m.CreatedAt.Returns(now.AddMinutes(-minutesAgo));
            var e = Substitute.For<IDiscordEmbed>();
            e.Title.Returns(title);
            m.Embeds.Returns([e]);
            return m;
        }
        var messages = new[]
        {
            Msg(11, BotId, "Matchmaking Queue", 5),
            Msg(10, BotId, "Matchmaking Queue", 1),
            Msg(12, 2, "Matchmaking Queue", 0), // someone else's message
            Msg(13, BotId, "Something else", 0),
            Msg(14, BotId, "Matchmaking Queue", 9),
        };
        _discordClient.GetMessagesAsync(QueueChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => messages.ToAsyncEnumerable());
        var queued = await SeedUsers(1);
        _context.QueuedPlayer.Add(new QueuedPlayer { UserID = queued[0].Id, JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedMatch(home, away);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await _discordClient.Received(1).DeleteMessagesAsync(QueueChannel, Arg.Is<IEnumerable<ulong>>(ids => ids.SequenceEqual(new ulong[] { 11, 14 })), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        var call = _discordClient.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync));
        Assert.That(call.GetArguments()[0], Is.EqualTo(QueueChannel));
        Assert.That(call.GetArguments()[1], Is.EqualTo(10ul));
        Assert.That(SentTo(QueueChannel), Is.Empty, "Existing message is modified instead of sending a new one");

        var options = Substitute.For<IDiscordMessageOptions>();
        ((Action<IDiscordMessageOptions>)call.GetArguments()[2]!)(options);
        var embeds = options.Embeds!.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(options.Content, Is.EqualTo("Matchmaking Queue"));
            Assert.That(options.Components, Is.Not.Null.And.Not.Empty);
            Assert.That(embeds.Select(x => x.Title), Is.EqualTo(new[] { "Matchmaking Queue", "Active Matches", $"Match #{match.Id}" }));
            Assert.That(embeds[0].Description, Is.EqualTo("1 players in queue"));
            // No ranks seeded -> no rank shown
            Assert.That(embeds[0].Fields!.Single().Value, Is.EqualTo($"1. {queued[0].XboxUser!.Gamertag} (MMR: 1000) = Queued <t:1704067200:R>"));
            Assert.That(embeds[1].Description, Is.EqualTo("1 active matches"));
            Assert.That(embeds[2].Fields!.Select(f => f.Name), Is.EqualTo(new[] { "Home (Avg MMR: 1000)", "Away (Avg MMR: 1000)" }));
            Assert.That(embeds[2].Fields!.First().Value, Is.EqualTo($"{home[0].XboxUser!.Gamertag} (MMR: 1000)"));
        });
    }

    [Test]
    public async Task Go_QueueMessage_ShowsLowestRank_WhenMmrBelowEveryThreshold()
    {
        await new RankSeeder(_context, new TestReader()).SeedRanks();
        var queued = await SeedUsers(1, u => u.MMR = 5);
        _context.QueuedPlayer.Add(new QueuedPlayer { UserID = queued[0].Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        var queueMessage = SentTo(QueueChannel).Single();
        Assert.That(queueMessage.Embeds!.First().Fields!.Single().Value, Does.StartWith($"1. {queued[0].XboxUser!.Gamertag} [Iron 2] (MMR: 5)"));
    }

    [Test]
    public async Task Go_CreatesMatch_AddsDiscordUsersToThread_AndToleratesFailures()
    {
        var users = await SeedUsers(4, withDiscord: true);
        var failingId = (ulong)users[0].DiscordUser!.DiscordUserID;
        _thread.AddUserAsync(failingId, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("not in guild"));
        _context.QueuedPlayer.AddRange(users.Select((u, i) => new QueuedPlayer { UserID = u.Id, JoinedAt = DateTime.UtcNow.AddMinutes(-10 + i) }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        foreach (var u in users)
            await _thread.Received(1).AddUserAsync((ulong)u.DiscordUser!.DiscordUserID, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        var warning = _logger.Entries.Single(x => x.Level == LogLevel.Warning);
        Assert.That(warning.Message, Is.EqualTo($"Failed to add user to thread {failingId}"));

        await using var fresh = Fresh();
        var match = await fresh.MatchedMatches.Include(x => x.HomeTeam.Players).Include(x => x.AwayTeam.Players).SingleAsync();
        var voteMessage = SentTo(ThreadId).Single();
        Assert.Multiple(() =>
        {
            Assert.That(match.ThreadID, Is.EqualTo(ThreadId));
            Assert.That(match.VoteMessageID, Is.EqualTo(77ul));
            Assert.That(match.HomeTeam.Players.Select(x => x.UserID), Is.EqualTo(new[] { users[0].Id, users[2].Id }), "Players alternate between teams by queue order");
            Assert.That(match.AwayTeam.Players.Select(x => x.UserID), Is.EqualTo(new[] { users[1].Id, users[3].Id }));
            Assert.That(voteMessage.Content, Does.StartWith("This match can be now be started in game."));
            Assert.That(voteMessage.Components!.Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task UpdateThreadMessage_DoesNothing_WhenMatchNullOrNoThread()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedMatch(home, away, threadId: null);

        await _service.UpdateThreadMessage(null, _context, _discordClient);
        await _service.UpdateThreadMessage(match, _context, _discordClient);

        Assert.That(_discordClient.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync)), Is.Empty);
    }

    [Test]
    public async Task UpdateThreadMessage_WithoutVotes_ResendsVoteMessageWithoutEmbeds()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedMatch(home, away);

        await _service.UpdateThreadMessage(match, _context, _discordClient);

        var call = _discordClient.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync));
        var options = Substitute.For<IDiscordMessageOptions>();
        ((Action<IDiscordMessageOptions>)call.GetArguments()[2]!)(options);
        Assert.Multiple(() =>
        {
            Assert.That(call.GetArguments()[0], Is.EqualTo(ThreadId));
            Assert.That(call.GetArguments()[1], Is.EqualTo(4242ul));
            Assert.That(options.Embeds, Is.Empty);
            Assert.That(options.Content, Does.StartWith("This match can be now be started in game."));
        });
    }

    [Test]
    public async Task UpdateThreadMessage_WithVotes_AddsVoteSummaryEmbeds()
    {
        var home = await SeedUsers(2);
        var away = await SeedUsers(2);
        var match = await SeedMatch(home, away);
        match.AwayTeam.Players[1].Kicked = true;
        await _context.SaveChangesAsync();
        await Vote(match, match.HomeTeam.Players[0], WinnerVote.Home);
        await Vote(match, match.HomeTeam.Players[1], WinnerVote.Home);
        await Vote(match, match.AwayTeam.Players[0], WinnerVote.Cancel);
        await Vote(match, match.AwayTeam.Players[1], WinnerVote.Away); // kicked -> ignored

        await _service.UpdateThreadMessage(match, _context, _discordClient);

        var call = _discordClient.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync));
        var options = Substitute.For<IDiscordMessageOptions>();
        ((Action<IDiscordMessageOptions>)call.GetArguments()[2]!)(options);
        var embeds = options.Embeds!.ToList();
        var kickMenu = options.Components!.OfType<StringMenuProperties>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(embeds[0].Title, Is.EqualTo("Votes"));
            Assert.That(embeds[0].Description, Is.EqualTo("You need a majority of at least 2 votes"), "3 non-kicked players -> 3/2 + 1");
            Assert.That(embeds.Single(e => e.Title == "Home (2)").Description!.Split(", "), Is.EquivalentTo(home.Select(x => x.XboxUser!.Gamertag)));
            Assert.That(embeds.Single(e => e.Title == "Cancel (1)").Description, Is.EqualTo(away[0].XboxUser!.Gamertag));
            Assert.That(embeds.Any(e => e.Title!.StartsWith("Away")), Is.False, "Kicked player's vote is not shown");
            Assert.That(embeds, Has.Count.EqualTo(3));
            Assert.That(kickMenu.Options.Select(o => o.Value), Is.EquivalentTo(new[] { home[0].Id, home[1].Id, away[0].Id }.Select(x => x.ToString())), "Kicked players cannot be voted on again");
        });
    }

    [Test]
    public void CreateVoteMessage_UsesBestAvailableName()
    {
        var users = new[]
        {
            new User { Id = 1, XboxUser = new XboxUser { Gamertag = "xbox" }, DiscordUser = new DiscordUser { DiscordUsername = "disc" } },
            new User { Id = 2, DiscordUser = new DiscordUser { DiscordUsername = "disc" } },
            new User { Id = 3, DisplayName = "display" },
            new User { Id = 4 },
        };

        var message = QueueService.CreateVoteMessage(5, users);

        var menu = message.Components!.OfType<StringMenuProperties>().Single();
        var buttons = message.Components!.OfType<ActionRowProperties>().Single().Components.OfType<ButtonProperties>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(menu.Options.Select(o => o.Label), Is.EqualTo(new[] { "xbox", "disc", "display", "4" }));
            Assert.That(menu.Placeholder, Is.EqualTo("Vote to kick"));
            Assert.That(buttons.Select(b => b.Label), Is.EqualTo(new[] { "Home Team Won", "Away Team Won", "Cancel" }));
            Assert.That(buttons.Select(b => b.CustomId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(buttons.All(b => b.CustomId.Contains('5')), Is.True, "Match id is encoded in the custom id");
        });
    }

    [Test]
    public async Task Go_KickedPlayerWhoRejoined_IsMatchedAgain_AndTheTickCompletes()
    {
        // A kicked player may queue again (IsInMatch ignores kicked players), so the tick must neither remove them as
        // still "in" the old match nor try to remove their queue row twice.
        var home = await SeedUsers(2);
        var away = await SeedUsers(2);
        var oldMatch = await SeedMatch(home, away);
        oldMatch.HomeTeam.Players[0].Kicked = true;
        var kicked = home[0];
        var others = await SeedUsers(3);
        _context.QueuedPlayer.Add(new QueuedPlayer { UserID = kicked.Id, JoinedAt = DateTime.UtcNow.AddMinutes(-5) });
        _context.QueuedPlayer.AddRange(others.Select((u, i) => new QueuedPlayer { UserID = u.Id, JoinedAt = DateTime.UtcNow.AddMinutes(-4 + i) }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        Assert.That(await new QueueRepository(_context).IsInMatch(kicked.Id, CancellationToken.None), Is.False, "Kicked players are not in the match");

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var matches = await fresh.MatchedMatches.Include(x => x.HomeTeam.Players).Include(x => x.AwayTeam.Players).ToListAsync();
        var newMatch = matches.Single(x => x.Id != oldMatch.Id);
        Assert.Multiple(() =>
        {
            Assert.That(newMatch.HomeTeam.Players.Concat(newMatch.AwayTeam.Players).Select(x => x.UserID),
                Is.EquivalentTo(others.Select(x => x.Id).Append(kicked.Id)));
            Assert.That(fresh.QueuedPlayer.Any(), Is.False, "Everyone queued was matched");
            Assert.That(_logger.Entries.Where(x => x.Level >= LogLevel.Error), Is.Empty);
        });
    }

    [Test]
    public async Task Go_QueuedPlayerStillInAnActiveMatch_IsRemoved_AndNotMatched()
    {
        var home = await SeedUsers(2);
        var away = await SeedUsers(2);
        await SeedMatch(home, away);
        var others = await SeedUsers(3);
        // home[1] is still in the active match: removed from the queue, which leaves too few for a match
        _context.QueuedPlayer.Add(new QueuedPlayer { UserID = home[1].Id, JoinedAt = DateTime.UtcNow.AddMinutes(-5) });
        _context.QueuedPlayer.AddRange(others.Select(u => new QueuedPlayer { UserID = u.Id, JoinedAt = DateTime.UtcNow }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        Assert.Multiple(async () =>
        {
            Assert.That(await fresh.MatchedMatches.CountAsync(), Is.EqualTo(1));
            Assert.That(await fresh.QueuedPlayer.Select(x => x.UserID).ToListAsync(), Is.EquivalentTo(others.Select(x => x.Id)));
        });
    }

    [Test]
    public async Task Go_VoteEndedMatch_KickedPlayersGetNoWinLossOrMmr()
    {
        var home = await SeedUsers(3);
        var away = await SeedUsers(2);
        var match = await SeedMatch(home, away);
        match.HomeTeam.Players[2].Kicked = true;
        match.AwayTeam.Players[1].Kicked = true;
        await _context.SaveChangesAsync();
        foreach (var p in match.HomeTeam.Players.Take(2))
            await Vote(match, p, WinnerVote.Home);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var kickedWinner = await fresh.Users.SingleAsync(x => x.Id == home[2].Id);
        var kickedLoser = await fresh.Users.SingleAsync(x => x.Id == away[1].Id);
        var winner = await fresh.Users.SingleAsync(x => x.Id == home[0].Id);
        var loser = await fresh.Users.SingleAsync(x => x.Id == away[0].Id);
        var log = SentTo(LogChannel).Single();
        Assert.Multiple(() =>
        {
            Assert.That((kickedWinner.Wins, kickedWinner.Losses, kickedWinner.MMR), Is.EqualTo((0, 0, 1000)));
            Assert.That((kickedLoser.Wins, kickedLoser.Losses, kickedLoser.MMR), Is.EqualTo((0, 0, 1000)));
            Assert.That((winner.Wins, winner.MMR), Is.EqualTo((1, 1024)));
            Assert.That((loser.Losses, loser.MMR), Is.EqualTo((1, 980)));
            Assert.That(Field(log, "Home Team MMR Changes").Split('\n'), Has.Length.EqualTo(2), "Kicked players are not listed");
            Assert.That(Field(log, "Away Team MMR Changes").Split('\n'), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task Go_DiscordFailsWhileAnnouncingAMatch_TheMatchIsStillCreated_AndActiveMatchesStillFinish()
    {
        // An active match that a majority voted to end
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var active = await SeedMatch(home, away);
        await Vote(active, active.HomeTeam.Players[0], WinnerVote.Home);
        await Vote(active, active.AwayTeam.Players[0], WinnerVote.Home);
        // And enough players queued for a new match, whose thread can't be made
        var queued = await SeedUsers(4);
        _context.QueuedPlayer.AddRange(queued.Select(u => new QueuedPlayer { UserID = u.Id, JoinedAt = DateTime.UtcNow }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        _discordClient.CreateGuildThreadAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("discord down"));

        await _service.Go(CancellationToken.None);
        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var created = await fresh.MatchedMatches.Where(x => x.Id != active.Id).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(created, Has.Count.EqualTo(1), "Made once, and not made again on the next tick");
            Assert.That(created.Single().Active, Is.True);
            Assert.That(await fresh.QueuedPlayer.AnyAsync(), Is.False);
            Assert.That((await fresh.MatchedMatches.SingleAsync(x => x.Id == active.Id)).Active, Is.False, "The voted match still ended");
            Assert.That(_logger.Entries.Single(x => x.Level == LogLevel.Error).Message, Is.EqualTo($"Failed to announce match {created.Single().Id} on Discord"));
        });
    }

    [Test]
    public async Task Go_HaloDownloadFails_VoteEndingsStillHappen()
    {
        _dataPullService.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("halo down"));
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedMatch(home, away);
        await Vote(match, match.HomeTeam.Players[0], WinnerVote.Cancel);
        await Vote(match, match.AwayTeam.Players[0], WinnerVote.Cancel);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        Assert.That((await fresh.MatchedMatches.SingleAsync()).Active, Is.False);
        Assert.That(_logger.Entries.Single(x => x.Level == LogLevel.Error).Exception, Is.TypeOf<HttpRequestException>());
    }

    [Test]
    public async Task Go_OneMatchFailingToFinish_DoesNotStopTheOthers()
    {
        var home1 = await SeedUsers(1);
        var away1 = await SeedUsers(1);
        var first = await SeedMatch(home1, away1, threadId: 1);
        var home2 = await SeedUsers(1);
        var away2 = await SeedUsers(1);
        var second = await SeedMatch(home2, away2, threadId: 2);
        foreach (var m in new[] { first, second })
        {
            await Vote(m, m.HomeTeam.Players[0], WinnerVote.Home);
            await Vote(m, m.AwayTeam.Players[0], WinnerVote.Home);
        }
        _context.ChangeTracker.Clear();
        // The first match's result can't be posted
        var calls = 0;
        _discordClient.SendMessageAsync(LogChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1 ? throw new InvalidOperationException("discord down") : Substitute.For<IDiscordRestMessage>());

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        var users = await fresh.Users.ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(2), "The second match was still finished");
            Assert.That(_logger.Entries.Count(x => x.Level == LogLevel.Error), Is.EqualTo(1));
            Assert.That(users.Sum(x => x.Wins), Is.EqualTo(2), "Both matches' results were saved before Discord was told");
        });
    }

    [Test]
    public async Task Go_QueueMessageFails_IsLogged_AndMatchesStillFinish()
    {
        _discordClient.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("discord down"));
        var home = await SeedUsers(1);
        var away = await SeedUsers(1);
        var match = await SeedMatch(home, away);
        await Vote(match, match.HomeTeam.Players[0], WinnerVote.Cancel);
        await Vote(match, match.AwayTeam.Players[0], WinnerVote.Cancel);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        await using var fresh = Fresh();
        Assert.That((await fresh.MatchedMatches.SingleAsync()).Active, Is.False);
        Assert.That(_logger.Entries.Single(x => x.Level == LogLevel.Error).Message, Is.EqualTo("Failed to update the matchmaking queue message"));
    }

    [Test]
    public async Task Go_VoteEndedMatch_TeamWhosePlayersWereAllKicked_ShowsNoPlayers()
    {
        var home = await SeedUsers(1);
        var away = await SeedUsers(2);
        var match = await SeedMatch(home, away);
        match.HomeTeam.Players[0].Kicked = true;
        await _context.SaveChangesAsync();
        foreach (var p in match.AwayTeam.Players)
            await Vote(match, p, WinnerVote.Away);
        _context.ChangeTracker.Clear();

        await _service.Go(CancellationToken.None);

        Assert.That(Field(SentTo(LogChannel).Single(), "Home Team MMR Changes"), Is.EqualTo("No players"), "Discord rejects an empty field");
    }
}
