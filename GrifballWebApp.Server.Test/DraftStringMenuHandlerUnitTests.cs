using DiscordInterfaces;
using GrifballWebApp.Server.Teams;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DraftStringMenuHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402). TempResponse's 5-second wait
/// is zeroed assembly-wide (AssemblySetUp). The draft pick itself (TeamService.AddPlayerToTeam) stays
/// GrifballWebApp.Integration.Test's, with a database.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DraftStringMenuHandlerUnitTests : SqliteGrifballContextFixture
{
    [Test]
    public async Task DraftPick_RepliesToSetAGamertag_ForAnUnknownCaller()
    {
        var handler = new DraftStringMenuHandler(Db, null!, new RecordingLogger<DraftStringMenuHandler>());
        var context = Substitute.For<IDiscordStringMenuInteractionContext>();
        context.User.Id.Returns(909090ul);

        await handler.DraftPick(context, seasonId: 1, captainId: 1);

        context.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties>(p =>
            ((InteractionCallbackProperties<InteractionMessageProperties>)p).Data.Content == "You must set your gamertag first"));
    }
}
