using System.Text.Json;
using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Match = GrifballWebApp.Domain.Halo.Match;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// QueueService.Go ending active matches, on a real (temp-file SQLite) GrifballContext: a majority's vote for a side
/// (MMR moved, the end post and thread close queued), a vote to cancel, a Halo game the players played (reported, its
/// duration in the post), Halo being down (the votes still count); and the vote message's tally. The matches start at
/// a fixed time; the deciding comes before the stale-match rule, so the clock never decides one.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueServiceFinishTests : SqliteContextFixture
{
    private const ulong QueueChannel = 111UL;
    private const ulong ThreadId = 999UL;
    private const ulong VoteMessageId = 4242UL;
    // In the future: never more than the two hours old that QueueService (on the real clock) cancels a match at
    private static readonly DateTime StartedAt = new(2099, 1, 1, 20, 0, 0, DateTimeKind.Utc);
    private static readonly string[] TalliedSides = ["Home (1)", "Away (1)"];
    private static readonly string[] EndedSteps = [MatchOutbox.EndedPost, MatchOutbox.ThreadClose];
    private static readonly string[] CanceledSteps = [MatchOutbox.CanceledPost, MatchOutbox.ThreadClose];

    private IDiscordRestClient _discord = null!;
    private IDataPullService _dataPull = null!;
    private int _seq;

    [SetUp]
    public void SetUp()
    {
        _discord = Substitute.For<IDiscordRestClient>();
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(1UL);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        var sent = Substitute.For<IDiscordRestMessage>();
        sent.Id.Returns(77UL);
        _discord.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(sent);
        _dataPull = Substitute.For<IDataPullService>();
    }

    private QueueService Service() => new(NullLogger<QueueService>.Instance, Options.Create(new DiscordOptions { QueueChannel = QueueChannel, MatchPlayers = 2 }),
        new QueueRepository(Context), _discord, Context, _dataPull, new ContextOutbox(Context), Substitute.For<IQueueNotifier>());

    private async Task<User> SeedUser()
    {
        var n = ++_seq;
        var user = User.Seed(userName: $"user{n}", xboxUser: GrifballWebApp.Domain.Players.XboxUser.FromXbox(1000 + n, $"gt{n}"));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    /// <summary>A 1v1 match, announced (its thread and vote message posted), each player's vote cast as given.</summary>
    private async Task<MatchedMatch> SeedMatch(User home, User away, WinnerVote? homeVote = null, WinnerVote? awayVote = null)
    {
        var match = MatchedMatch.Start([home.Id], [away.Id], StartedAt);
        match.AttachThread(ThreadId);
        match.PostVoteMessage(VoteMessageId);
        Context.MatchedMatches.Add(match);
        await Context.SaveChangesAsync();
        if (homeVote is { } h)
            Assert.That(match.VoteForWinner(home.Id, h), Is.EqualTo(WinnerVoteResult.Voted));
        if (awayVote is { } a)
            Assert.That(match.VoteForWinner(away.Id, a), Is.EqualTo(WinnerVoteResult.Voted));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return match;
    }

    private async Task<List<OutboxMessage>> Queued(int matchId)
    {
        Context.ChangeTracker.Clear();
        return await Context.Outbox.AsNoTracking().Where(x => x.GroupKey == MatchOutbox.Group(matchId)).OrderBy(x => x.Sequence).ToListAsync();
    }

    private static MatchEndedPayload Ended(IEnumerable<OutboxMessage> queued)
        => JsonSerializer.Deserialize<MatchEndedPayload>(queued.Single(x => x.Kind == MatchOutbox.EndedPost).Payload, OutboxJson.Options)!;

    private async Task<User> Reload(User user) => await Context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);

    [Test]
    public async Task Go_AMajorityVotesHome_DecidesTheMatch_MovesMmr_AndQueuesTheEndPost()
    {
        var home = await SeedUser();
        var away = await SeedUser();
        var match = await SeedMatch(home, away, WinnerVote.Home, WinnerVote.Home);

        await Service().Go(CancellationToken.None);

        var queued = await Queued(match.Id);
        var saved = await Context.MatchedMatches.AsNoTracking().SingleAsync(m => m.Id == match.Id);
        var winner = await Reload(home);
        var loser = await Reload(away);
        var ended = Ended(queued);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((saved.Status, saved.WinnerSide, saved.MatchID), Is.EqualTo((MatchedMatchStatus.VoteDecided, (MatchedSide?)MatchedSide.Home, (Guid?)null)));
            Assert.That((winner.Wins, winner.MMR > User.StartingMMR), Is.EqualTo((1, true)));
            Assert.That((loser.Losses, loser.MMR < User.StartingMMR), Is.EqualTo((1, true)));
            Assert.That(queued.Select(x => x.Kind), Is.EqualTo(EndedSteps));
            Assert.That((ended.MatchId, ended.WinnerId, ended.Duration), Is.EqualTo((match.Id, 0, "Unknown")));
            Assert.That(ended.HomeMmrChanges, Is.EqualTo($"gt{_seq - 1}: {User.StartingMMR} -> {winner.MMR} (+{winner.MMR - User.StartingMMR})"));
            Assert.That(ended.AwayMmrChanges, Is.EqualTo($"gt{_seq}: {User.StartingMMR} -> {loser.MMR} ({loser.MMR - User.StartingMMR})"));
        }
    }

    [Test]
    public async Task Go_AMajorityVotesAway_TheAwayTeamIsTheWinner()
    {
        var home = await SeedUser();
        var away = await SeedUser();
        var match = await SeedMatch(home, away, WinnerVote.Away, WinnerVote.Away);

        await Service().Go(CancellationToken.None);

        var ended = Ended(await Queued(match.Id));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ended.WinnerId, Is.EqualTo(1));
            Assert.That((await Reload(away)).Wins, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Go_AMajorityVotesToCancel_CancelsIt_LeavingMmrAlone()
    {
        var home = await SeedUser();
        var away = await SeedUser();
        var match = await SeedMatch(home, away, WinnerVote.Cancel, WinnerVote.Cancel);

        await Service().Go(CancellationToken.None);

        var queued = await Queued(match.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Context.MatchedMatches.AsNoTracking().SingleAsync(m => m.Id == match.Id)).Status, Is.EqualTo(MatchedMatchStatus.Canceled));
            Assert.That(queued.Select(x => x.Kind), Is.EqualTo(CanceledSteps));
            Assert.That(queued[0].Payload, Does.Contain("\"unannounced\":false"));
            Assert.That((await Reload(home)).MMR, Is.EqualTo(User.StartingMMR));
        }
    }

    [Test]
    public async Task Go_TheirHaloGameIsIn_ReportsTheMatchWithIt_AndItsDuration()
    {
        var home = await SeedUser();
        var away = await SeedUser();
        var match = await SeedMatch(home, away);
        var game = Match.Seed(Guid.NewGuid(), startTime: StartedAt.AddMinutes(1), endTime: StartedAt.AddMinutes(13), duration: TimeSpan.FromMinutes(12));
        game.AddTeam(0, 3, Outcomes.Lost);
        game.AddTeam(1, 5, Outcomes.Won);
        game.AddParticipant(0, home.XboxUserID!.Value, new MatchParticipantStats());
        game.AddParticipant(1, away.XboxUserID!.Value, new MatchParticipantStats());
        Context.Matches.Add(game);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await Service().Go(CancellationToken.None);

        var saved = await Context.MatchedMatches.AsNoTracking().SingleAsync(m => m.Id == match.Id);
        var ended = Ended(await Queued(match.Id));
        using (Assert.EnterMultipleScope())
        {
            Assert.That((saved.Status, saved.MatchID), Is.EqualTo((MatchedMatchStatus.Reported, (Guid?)game.MatchID)));
            Assert.That((ended.WinnerId, ended.Duration), Is.EqualTo((1, "00:12:00")), "the Halo team that won, and how long the game took");
            Assert.That((await Reload(away)).Wins, Is.EqualTo(1));
            Assert.That((await Reload(home)).Losses, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Go_HaloDown_TheVotesStillDecide()
    {
        var home = await SeedUser();
        var away = await SeedUser();
        var match = await SeedMatch(home, away, WinnerVote.Home, WinnerVote.Home);
        _dataPull.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Halo is down"));

        await Service().Go(CancellationToken.None);

        Assert.That((await Context.MatchedMatches.AsNoTracking().SingleAsync(m => m.Id == match.Id)).Status, Is.EqualTo(MatchedMatchStatus.VoteDecided));
        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(
            Arg.Is<List<long>>(ids => ids.Order().SequenceEqual(new[] { home.XboxUserID!.Value, away.XboxUserID!.Value }.Order())), 0, 0, 2, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Go_AQueuedPlayerAlreadyInAMatch_LeavesTheQueue()
    {
        var home = await SeedUser();
        var away = await SeedUser();
        await SeedMatch(home, away);
        Context.QueuedPlayer.Add(QueuedPlayer.Seed(home.Id, StartedAt.AddMinutes(5)));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await Service().Go(CancellationToken.None);

        Assert.That(await Context.QueuedPlayer.AnyAsync(), Is.False);
    }

    /// <summary>What an edit's action sets, on a substitute of the options it's given (whose properties keep what's set).</summary>
    private static IDiscordMessageOptions Applied(object? action)
    {
        var options = Substitute.For<IDiscordMessageOptions>();
        ((Action<IDiscordMessageOptions>)action!)(options);
        return options;
    }

    [Test]
    public async Task UpdateThreadMessage_WithVotes_TalliesThemUnderTheVoteMessage()
    {
        var home = await SeedUser();
        var away = await SeedUser();
        var match = await SeedMatch(home, away, WinnerVote.Home, WinnerVote.Away);

        await Service().UpdateThreadMessage(match, Context, _discord);

        var call = _discord.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync));
        var options = Applied(call.GetArguments()[2]);
        var embeds = options.Embeds!.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((call.GetArguments()[0], call.GetArguments()[1]), Is.EqualTo(((object?)ThreadId, (object?)VoteMessageId)));
            Assert.That((embeds[0].Title, embeds[0].Description), Is.EqualTo(((string?)"Votes", (string?)"You need a majority of at least 2 votes")));
            Assert.That(embeds.Skip(1).Select(e => e.Title), Is.EquivalentTo(TalliedSides));
            Assert.That(embeds.Single(e => e.Title == "Home (1)").Description, Is.EqualTo($"gt{_seq - 1}"), "who voted for it");
            Assert.That(options.Components, Is.Not.Empty, "the vote buttons and kick menu stay");
        }
    }

    [Test]
    public async Task Go_AnExistingQueueMessage_IsEditedToTheQueue()
    {
        var existing = Substitute.For<IDiscordRestMessage>();
        existing.Id.Returns(10UL);
        existing.Author.Id.Returns(1UL);
        existing.CreatedAt.Returns(new DateTimeOffset(StartedAt));
        var embed = Substitute.For<IDiscordEmbed>();
        embed.Title.Returns(QueueService.QueueEmbedTitle);
        existing.Embeds.Returns([embed]);
        _discord.GetMessagesAsync(QueueChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => new[] { existing }.ToAsyncEnumerable());

        await Service().Go(CancellationToken.None);

        var call = _discord.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.ModifyMessageAsync));
        var options = Applied(call.GetArguments()[2]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Content, Is.EqualTo(QueueService.QueueEmbedTitle));
            Assert.That(options.Embeds!.First().Description, Does.Contain("0 players in queue"));
        }
    }
}
