using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Teams;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordMentionTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task ForUser_LinkedAccount_IsAMention()
    {
        var user = await _context.AddUser("linked", "Linked", discordName: "linkeddisc");

        Assert.That(await DiscordMention.ForUser(_context, user.Id), Is.EqualTo($"<@{user.DiscordUserID}>"));
    }

    [Test]
    public async Task ForUser_UnknownUser_IsTheirId()
    {
        Assert.That(await DiscordMention.ForUser(_context, 424242), Is.EqualTo("424242"));
    }
}
