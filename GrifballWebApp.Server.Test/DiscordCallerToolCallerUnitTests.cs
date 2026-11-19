using GrifballWebApp.Application.Tools;
using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server.Test;

/// <summary>Who the bot's chat tools run as (<see cref="DiscordCaller.AsToolCaller"/>): the author's linked account and roles, or nobody.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCallerToolCallerUnitTests : SqliteGrifballContextFixture
{
    private static readonly string[] Commissioner = ["Commissioner"];

    [Test]
    public async Task ALinkedAuthor_IsTheirAccount_WithItsRoles()
    {
        var user = await AddUser(5, "Linked", "Commissioner");

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller.UserID, Is.EqualTo(user.Id));
            Assert.That(caller.Roles, Is.EqualTo(Commissioner));
        }
    }

    [Test]
    public async Task AnUnlinkedAuthor_IsAnonymous()
    {
        await AddUser(5, "Someone else");

        var caller = await new DiscordCaller(Db).AsToolCaller(6);

        Assert.That(caller, Is.SameAs(ToolCaller.Anonymous));
    }

    /// <summary>
    /// <see cref="ToolCaller.SignedIn"/>: who counts as signed in for a chat model that is only for one
    /// (<see cref="GrifballWebApp.Application.Chat.ChatModelOption.SignedIn"/>). Only a linked account with a gamertag, not a
    /// placeholder, counts; the account and roles the tools run as (above) never change.
    /// </summary>
    [Test]
    public async Task ALinkedAuthorWithAGamertag_CountsAsSignedIn()
    {
        await AddUser(5, "Linked");

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        Assert.That(caller.SignedIn, Is.True);
    }

    [Test]
    public async Task ALinkedAuthorWithNoGamertag_DoesNotCountAsSignedIn()
    {
        await AddDiscordOnlyUser(5);

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller.UserID, Is.Not.Null, "Still their account: the tools run as them");
            Assert.That(caller.SignedIn, Is.False);
        }
    }

    [Test]
    public async Task ADummyUsersPlaceholderLinkedToDiscord_DoesNotCountAsSignedIn_EvenWithAGamertag()
    {
        await AddDummyUserLinkedToDiscord(5, "Historical1");

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        Assert.That(caller.SignedIn, Is.False);
    }

    [Test]
    public async Task AnUnlinkedAuthor_IsAnonymous_SoDoesNotCountAsSignedIn()
        => Assert.That((await new DiscordCaller(Db).AsToolCaller(31337)).SignedIn, Is.False);
}
