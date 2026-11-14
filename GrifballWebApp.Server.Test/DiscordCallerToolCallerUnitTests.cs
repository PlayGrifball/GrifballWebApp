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
}
