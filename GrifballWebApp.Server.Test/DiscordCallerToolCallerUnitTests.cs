using GrifballWebApp.Domain.Players;
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
    /// (<see cref="GrifballWebApp.Application.Chat.ChatModelOption.SignedIn"/>). A real (non-placeholder) linked account always
    /// counts, gamertag or not: it only exists through /setgamertag or a site sign-in with Discord, either of which also signs
    /// them in on the site. Only a dummy user (a historical import placeholder) doesn't; the account and roles the tools run as
    /// (above) never change.
    /// </summary>
    [Test]
    public async Task ALinkedAuthorWithAGamertag_CountsAsSignedIn()
    {
        await AddUser(5, "Linked");

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        Assert.That(caller.SignedIn, Is.True);
    }

    [Test]
    public async Task ALinkedAuthorWithNoGamertag_StillCountsAsSignedIn()
    {
        await AddDiscordOnlyUser(5);

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller.UserID, Is.Not.Null, "Still their account: the tools run as them");
            Assert.That(caller.SignedIn, Is.True, "A real account, linked only through /setgamertag or a site sign-in: either signs them in on the site too");
        }
    }

    [Test]
    public async Task ADummyUsersPlaceholderLinkedToDiscord_DoesNotCountAsSignedIn_EvenWithAGamertag()
    {
        await AddDummyUserLinkedToDiscord(5, "Historical1");

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        Assert.That(caller.SignedIn, Is.False);
    }

    private async Task AddLogin(int userId, string provider, ulong key)
    {
        Db.UserLogins.Add(new UserLogin { UserId = userId, LoginProvider = provider, ProviderKey = key.ToString(), ProviderDisplayName = provider });
        await Db.SaveChangesAsync();
    }

    [Test]
    public async Task ASecondDiscordAccountThatSignsIn_IsTheSameAccount_AndSignedIn()
    {
        var user = await AddUser(5, "Linked", "Commissioner");
        await AddLogin(user.Id, "Discord", 99);

        var caller = await new DiscordCaller(Db).AsToolCaller(99);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller.UserID, Is.EqualTo(user.Id));
            Assert.That(caller.Roles, Is.EqualTo(Commissioner));
            Assert.That(caller.SignedIn, Is.True);
        }
        Assert.That((await new DiscordCaller(Db).Find(99))?.UserID, Is.EqualTo(user.Id), "The commands too");
    }

    [Test]
    public async Task ADiscordIdHeldByOneAccount_BeatsAnotherAccountsLoginWithIt()
    {
        var holder = await AddUser(5, "Holder");
        var other = await AddUser(6, "Other");
        await AddLogin(other.Id, "Discord", 5);

        var caller = await new DiscordCaller(Db).AsToolCaller(5);

        Assert.That(caller.UserID, Is.EqualTo(holder.Id));
    }

    [Test]
    public async Task ALoginFromAnotherProvider_IsNotADiscordAccount()
    {
        var user = await AddUser(5, "Linked");
        await AddLogin(user.Id, "Google", 99);

        var caller = await new DiscordCaller(Db).AsToolCaller(99);

        Assert.That(caller, Is.SameAs(ToolCaller.Anonymous));
    }

    [Test]
    public async Task AnUnlinkedAuthor_IsAnonymous_SoDoesNotCountAsSignedIn()
        => Assert.That((await new DiscordCaller(Db).AsToolCaller(31337)).SignedIn, Is.False);
}
