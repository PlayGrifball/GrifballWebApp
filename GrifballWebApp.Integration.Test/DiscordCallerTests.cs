using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Server.Commands;
using NetCord;
using NetCord.Rest;

using GrifballWebApp.Server.Test;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// <see cref="DiscordCaller"/>: a command that isn't open to everyone runs as the app user linked to the Discord account,
/// held to the site's roles.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCallerTests
{
    private GrifballContext _context = null!;
    private DiscordCaller _caller = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _caller = new DiscordCaller(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task Require_Should_OfferTheSetGamertagButton_When_TheDiscordUserHasNoAccount()
    {
        var discord = new CommandContextRecorder(discordUserId: 4242);

        var caller = await _caller.Require(discord.Context, DiscordCaller.Roles.Anyone);

        // The reply was deferred (ephemerally) before the lookup: the refusal is its edit
        var components = (IEnumerable<IMessageComponentProperties>)discord.EditArgument(nameof(DiscordInterface.Generated.IDiscordMessageOptions.WithComponents))!;
        var button = components.OfType<ActionRowProperties>().Single().OfType<ButtonProperties>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller, Is.Null);
            Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.NoGamertag));
            Assert.That(button.CustomId, Is.EqualTo(DiscordButtonContants.SetGamertag));
        }
    }

    [Test]
    public async Task Require_Should_OfferTheSetGamertagButton_When_TheLinkedAccountHasNoGamertag()
    {
        var user = await _context.AddUser("nogt", discordName: "nogt");
        var discord = new CommandContextRecorder((ulong)user.DiscordUserID!.Value);

        var caller = await _caller.Require(discord.Context, DiscordCaller.Roles.Anyone);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller, Is.Null);
            Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.NoGamertag));
        }
    }

    [Test]
    public async Task Require_Should_SayWhoMay_When_TheCallerLacksTheRole()
    {
        var user = await _context.AddUser("player", gamertag: "PlayerGT", discordName: "player");
        var discord = new CommandContextRecorder((ulong)user.DiscordUserID!.Value);

        var caller = await _caller.Require(discord.Context, DiscordCaller.Roles.Staff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller, Is.Null);
            Assert.That(discord.Content(), Is.EqualTo("Only a commissioner or sysadmin can do that."));
        }
    }

    [Test]
    public async Task Require_Should_ReturnTheCaller_When_TheyHaveTheRole()
    {
        var user = await _context.AddUser("commish", gamertag: "CommishGT", discordName: "commish", commissioner: true);
        var discord = new CommandContextRecorder((ulong)user.DiscordUserID!.Value);

        var caller = await _caller.Require(discord.Context, DiscordCaller.Roles.Staff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller?.UserID, Is.EqualTo(user.Id));
            Assert.That(caller?.Gamertag, Is.EqualTo("CommishGT"));
            Assert.That(caller?.Roles, Does.Contain("Commissioner"));
            Assert.That(discord.Callbacks, Is.Empty);
        }
    }
}
