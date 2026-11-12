using DiscordInterfaces;
using GrifballWebApp.Server.Matchmaking;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// MatchmakingStringMenuHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402). TempResponse's
/// 5-second wait is zeroed assembly-wide (AssemblySetUp).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingStringMenuHandlerUnitTests : SqliteGrifballContextFixture
{
    private MatchmakingStringMenuHandler Handler() => new(Db, null!, null!, null!);

    private static IDiscordStringMenuInteractionContext Context(ulong discordUserId, params string[] selectedValues)
    {
        var context = Substitute.For<IDiscordStringMenuInteractionContext>();
        context.User.Id.Returns(discordUserId);
        context.SelectedValues.Returns(selectedValues);
        return context;
    }

    private static void AssertSent(IDiscordStringMenuInteractionContext context, string expected)
        => context.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties>(p =>
            ((InteractionCallbackProperties<InteractionMessageProperties>)p).Data.Content == expected));

    [Test]
    public async Task VoteToKick_RepliesToSetAGamertag_ForAnUnknownCaller()
    {
        var context = Context(909090, "1");

        await Handler().VoteToKick(context, matchId: 1);

        AssertSent(context, "You must set your gamertag first");
    }

    [Test]
    public async Task VoteToKick_RepliesThisMatchDoesNotExist_ForAnUnknownMatch()
    {
        await AddUser(42, "Grifter1");
        var context = Context(42, "1");

        await Handler().VoteToKick(context, matchId: 999);

        AssertSent(context, "This match does not exist");
    }
}
