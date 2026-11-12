using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordSetGamertag's unit tests, on a real GrifballContext (temp-file SQLite, #402). UserManager is NSubstitute's,
/// as the Identity controller tests already substitute it. TempResponse/ModifyTempResponse's 5-second wait is zeroed
/// assembly-wide (AssemblySetUp).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordSetGamertagUnitTests : SqliteGrifballContextFixture
{
    private static UserManager<User> Users() => Substitute.For<UserManager<User>>(
        Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);

    [Test]
    public async Task SetGamertag_RepliesLinkedToOtherDiscord_WhenTheSignedInAccountIsLinkedElsewhere()
    {
        var users = Users();
        var elsewhere = User.Register("someoneElse");
        elsewhere.LinkDiscord(DiscordUser.FromDiscord(111, "other"));
        users.FindByLoginAsync("Discord", "909090").Returns(elsewhere);
        var handler = new DiscordSetGamertag(Db, users, Substitute.For<ISetGamertagService>());
        var discord = new CommandContextRecorder(discordUserId: 909090);

        await handler.SetGamertag(discord.Context, "NewGamertag");

        Assert.That(discord.Content(), Is.EqualTo(DiscordSetGamertag.LinkedToOtherDiscord));
    }
}
