using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static GrifballWebApp.Server.Test.DiscordComponentContexts;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// MatchmakingStringMenuHandler's vote to kick on a Late League match, past the refusals of
/// MatchmakingStringMenuHandlerUnitTests: a 2v2 match on a temp-file SQLite GrifballContext (the match has no thread yet,
/// so its vote message has nothing to update), the queue's notifier substituted. A majority is more than half of the
/// four: three votes.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingKickVoteUnitTests : SqliteGrifballContextFixture
{
    private readonly IQueueNotifier _notifier = Substitute.For<IQueueNotifier>();
    private int[] _players = [];

    private MatchmakingStringMenuHandler Handler()
    {
        var discord = Substitute.For<IDiscordRestClient>();
        var queue = new QueueService(NullLogger<QueueService>.Instance, Options.Create(new DiscordOptions { QueueChannel = 1 }), Substitute.For<IQueueRepository>(),
            discord, Db, Substitute.For<IDataPullService>(), Substitute.For<IOutbox>(), _notifier);
        return new MatchmakingStringMenuHandler(Db, discord, queue, _notifier);
    }

    /// <summary>Alpha and Bravo against Charlie and Delta (Discord 1 to 4), with <paramref name="votesAgainstBravo"/> of Charlie's
    /// and Delta's votes to kick Bravo already in.</summary>
    private async Task<MatchedMatch> Match(int votesAgainstBravo = 0, Action<MatchedMatch>? before = null)
    {
        _players =
        [
            (await AddUser(1, "Alpha")).Id, (await AddUser(2, "Bravo")).Id,
            (await AddUser(3, "Charlie")).Id, (await AddUser(4, "Delta")).Id,
        ];
        var match = MatchedMatch.Start([_players[0], _players[1]], [_players[2], _players[3]]);
        foreach (var voter in _players.Skip(2).Take(votesAgainstBravo))
            match.VoteToKick(voter, _players[1]);
        before?.Invoke(match);
        Db.MatchedMatches.Add(match);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return match;
    }

    private async Task<int> VotesAgainst(int userID)
    {
        Db.ChangeTracker.Clear();
        return await Db.Set<MatchedKickVote>().CountAsync(v => v.KickMatchedPlayer.UserID == userID);
    }

    [Test]
    public async Task VoteToKick_AVoteShortOfAMajority_IsSavedAndAnnounced()
    {
        var match = await Match();
        var context = StringMenuContext(1, _players[1].ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "Alpha has voted to kick Bravo");
        Assert.That(await VotesAgainst(_players[1]), Is.EqualTo(1));
        await _notifier.DidNotReceiveWithAnyArgs().SendStatusChanged(default!);
    }

    [Test]
    public async Task VoteToKick_TheVoteThatMakesAMajority_KicksThem()
    {
        var match = await Match(votesAgainstBravo: 2);
        var context = StringMenuContext(1, _players[1].ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "Bravo has been kicked");
        Db.ChangeTracker.Clear();
        var bravo = await Db.Set<MatchedPlayer>().SingleAsync(p => p.UserID == _players[1]);
        Assert.That(bravo.Kicked, Is.True);
        await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(new[] { _players[1] })));
    }

    [Test]
    public async Task VoteToKick_Twice_IsRefused()
    {
        var match = await Match(votesAgainstBravo: 1);
        var context = StringMenuContext(3, _players[1].ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "You have already voted to kick Bravo");
        Assert.That(await VotesAgainst(_players[1]), Is.EqualTo(1));
    }

    [Test]
    public async Task VoteToKick_SomeoneNotInTheMatch_IsRefused()
    {
        var match = await Match();
        var outsider = await AddUser(5, "Echo");
        var context = StringMenuContext(1, outsider.Id.ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "They are not in this match");
    }

    [Test]
    public async Task VoteToKick_ByAPlayerNotInTheMatch_IsRefused()
    {
        var match = await Match();
        await AddUser(5, "Echo");
        var context = StringMenuContext(5, _players[1].ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "You are not in this match");
    }

    [Test]
    public async Task VoteToKick_InAMatchThatIsOver_IsRefused()
    {
        var match = await Match(before: m => m.Canceled());
        var context = StringMenuContext(1, _players[1].ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "This match is over");
        Assert.That(await VotesAgainst(_players[1]), Is.Zero);
    }

    [Test]
    public async Task VoteToKick_ByAPlayerKickedAlready_IsRefused()
    {
        var match = await Match(before: m => m.HomeTeam.Players.Single(p => p.UserID == _players[0]).AlreadyKicked());
        var context = StringMenuContext(1, _players[1].ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "You have been kicked. You cannot vote.");
    }

    [Test]
    public async Task VoteToKick_APlayerKickedAlready_IsRefused_NamingThem()
    {
        var match = await Match(before: m => m.HomeTeam.Players.Single(p => p.UserID == _players[1]).AlreadyKicked());
        var context = StringMenuContext(1, _players[1].ToString());

        await Handler().VoteToKick(context, match.Id);

        await AssertSent(context, "Bravo has already been kicked");
    }
}
