using GrifballWebApp.Server.Matchmaking;
using static GrifballWebApp.Server.Test.DiscordComponentContexts;

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

    [Test]
    public async Task VoteToKick_RepliesToSetAGamertag_ForAnUnknownCaller()
    {
        var context = StringMenuContext(909090, "1");

        await Handler().VoteToKick(context, matchId: 1);

        await AssertSent(context, "You must set your gamertag first");
    }

    [Test]
    public async Task VoteToKick_RepliesThisMatchDoesNotExist_ForAnUnknownMatch()
    {
        await AddUser(42, "Grifter1");
        var context = StringMenuContext(42, "1");

        await Handler().VoteToKick(context, matchId: 999);

        await AssertSent(context, "This match does not exist");
    }
}
