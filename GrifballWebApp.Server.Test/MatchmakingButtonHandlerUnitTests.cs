using DiscordInterfaces;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// MatchmakingButtonHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402): JoinQueue, LeaveQueue,
/// SetGamertag and VoteForWinner's guards. RefreshQueueDisplay's own tests are ButtonInteractionsUnitTests'.
/// TempResponse's 5-second wait is zeroed assembly-wide (AssemblySetUp).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingButtonHandlerUnitTests : SqliteGrifballContextFixture
{
    private MatchmakingButtonHandler Handler(IQueueRepository? queue = null) => new(
        new QueueMembership(queue ?? Substitute.For<IQueueRepository>(), Db, Substitute.For<IQueueNotifier>()),
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), Db, null!, null!, new RecordingLogger<MatchmakingButtonHandler>());

    private static IDiscordButtonInteractionContext ButtonContext(ulong discordUserId)
    {
        var context = Substitute.For<IDiscordButtonInteractionContext>();
        context.User.Id.Returns(discordUserId);
        return context;
    }

    private static async Task AssertSent(IDiscordButtonInteractionContext context, string expected)
        => await context.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties>(p =>
            ((InteractionCallbackProperties<InteractionMessageProperties>)p).Data.Content == expected));

    [Test]
    public async Task JoinQueue_RepliesToSetAGamertag_ForAnUnknownCaller()
    {
        var context = ButtonContext(909090);

        await Handler().JoinQueue(context);

        await AssertSent(context, "You must set your gamertag first");
    }

    [Test]
    public async Task LeaveQueue_RepliesNotInQueue_ForAnUnknownCaller()
    {
        var context = ButtonContext(909090);

        await Handler().LeaveQueue(context);

        await AssertSent(context, "You are not in the Late League queue.");
    }

    [Test]
    public async Task SetGamertag_SendsAModal()
    {
        var context = ButtonContext(42);

        await Handler().SetGamertag(context);

        await context.Interaction.Received(1).SendResponseAsync(Arg.Any<InteractionCallbackProperties>());
    }

    [Test]
    public async Task VoteForWinner_RepliesToSetAGamertag_ForAnUnknownCaller()
    {
        var context = ButtonContext(909090);

        await Handler().VoteForWinner(context, matchId: 1, winner: "Home");

        await AssertSent(context, "You must set your gamertag first");
    }

    [Test]
    public async Task VoteForWinner_RepliesItCouldNotParse_ForAnUnknownWinnerValue()
    {
        await AddUser(42, "Grifter1");
        var context = ButtonContext(42);

        await Handler().VoteForWinner(context, matchId: 1, winner: "NotAValue");

        await AssertSent(context, "I could not parse the value NotAValue. Contact developer, expected values are Home,Away,Cancel");
    }

    [Test]
    public async Task VoteForWinner_RepliesMatchDoesNotExist_ForAnUnknownMatch()
    {
        await AddUser(42, "Grifter1");
        var context = ButtonContext(42);

        await Handler().VoteForWinner(context, matchId: 999, winner: "Home");

        await AssertSent(context, "Match does not exist or it is no longer active");
    }
}
