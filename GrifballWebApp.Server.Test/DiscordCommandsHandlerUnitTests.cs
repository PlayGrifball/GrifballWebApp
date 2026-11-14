using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordCommandsHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402): /matches reads the saved games through
/// the agents' get_recent_games (<see cref="HaloTools"/>), looking nothing up on Xbox and pulling nothing from Halo.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private DiscordCommandsHandler Handler() => new(Db, new HaloTools(Db, new XboxAccountService(Db)));

    [Test]
    public async Task RecentMatchesDeferred_SaysTheSiteHasNoGamesForAGamertagItDoesntKnow()
    {
        var discord = new CommandContextRecorder();

        await Handler().RecentMatchesDeferred(discord.Context, "nonsense");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Does.StartWith("The site has no games saved for the gamertag nonsense"));
            Assert.That(Db.XboxUsers.Any(), Is.False, "no Xbox account is saved for it");
        }
    }

    [Test]
    public async Task RecentMatchesDeferred_RepliesToSetAGamertag_ForAnUnknownCaller_WithNoneNamed()
    {
        var discord = new CommandContextRecorder();

        await Handler().RecentMatchesDeferred(discord.Context, null);

        Assert.That(discord.Content(), Is.EqualTo(DiscordCommandsHandler.NoGamertag));
    }

    [Test]
    public async Task RecentMatchesDeferred_SaysSo_ForAPlayerWithNoGamesSaved()
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(1001, "Grifter1"));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().RecentMatchesDeferred(discord.Context, "Grifter1");

        Assert.That(discord.Content(), Is.EqualTo("The site has no games saved for Grifter1."));
    }
}
