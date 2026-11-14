using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordCommandsHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402): /matches reads the saved games through
/// the agents' get_recent_games (<see cref="HaloTools"/>), looking nothing up on Xbox and pulling nothing from Halo. A typed
/// gamertag finds an account by its current gamertag only; a past one is found when picked from the list (its XUID).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private DiscordCommandsHandler Handler() => new(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)), new HaloTools(Db, new XboxAccountService(Db)));

    [Test]
    public async Task RecentMatches_SaysItFoundNoAccount_ForAGamertagItDoesntKnow()
    {
        var discord = new CommandContextRecorder();

        await Handler().RecentMatches(discord.Context, "nonsense");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchXboxAccount));
            Assert.That(Db.XboxUsers.Any(), Is.False, "no Xbox account is saved for it");
        }
    }

    [Test]
    public async Task RecentMatches_RepliesToSetAGamertag_ForAnUnknownCaller_WithNoneNamed()
    {
        var discord = new CommandContextRecorder();

        await Handler().RecentMatches(discord.Context, null);

        Assert.That(discord.Content(), Is.EqualTo(DiscordCommandsHandler.NoGamertag));
    }

    [Test]
    public async Task RecentMatches_SaysSo_ForAPlayerWithNoGamesSaved()
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(1001, "Grifter1"));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().RecentMatches(discord.Context, "Grifter1");

        Assert.That(discord.Content(), Is.EqualTo("The site has no games saved for Grifter1."));
    }

    [Test]
    public async Task RecentMatches_FindsNoAccount_ByAPastGamertagTyped_ButFindsIt_PickedFromTheList()
    {
        var renamed = XboxUser.FromXbox(1001, "New Name");
        renamed.RecordPastGamertag("Old Name", null, null, GamertagHistorySource.Manual);
        Db.XboxUsers.Add(renamed);
        await Db.SaveChangesAsync();
        var typed = new CommandContextRecorder();
        var picked = new CommandContextRecorder();

        await Handler().RecentMatches(typed.Context, "Old Name");
        await Handler().RecentMatches(picked.Context, CommandLookups.ChoiceValue(1001L));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(typed.Content(), Is.EqualTo(CommandLookups.NoSuchXboxAccount));
            Assert.That(picked.Content(), Is.EqualTo("The site has no games saved for New Name."));
        }
    }
}
