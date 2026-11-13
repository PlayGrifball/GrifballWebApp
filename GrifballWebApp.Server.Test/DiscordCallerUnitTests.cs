using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordCaller's unit tests, on a real GrifballContext over a temp-file SQLite database (#402): no database server, no
/// host, no network. <see cref="DiscordCaller.Find"/> is too tied to the database to run over a substitute one.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCallerUnitTests : SqliteGrifballContextFixture
{
    private DiscordCaller Caller() => new(Db);

    [Test]
    public async Task Find_ReturnsNull_ForAnUnknownDiscordUser()
    {
        Assert.That(await Caller().Find(31337), Is.Null);
    }

    [Test]
    public async Task Find_ReturnsNull_ForAUserWithNoGamertag()
    {
        await AddDiscordOnlyUser(31337);

        Assert.That(await Caller().Find(31337), Is.Null);
    }

    [Test]
    public async Task Find_ReturnsTheCaller_WithGamertagAndRoles()
    {
        var user = await AddUser(31337, "Grifter1", DiscordCaller.Roles.Commissioner);

        var found = await Caller().Find(31337);

        Assert.That(found, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(found!.UserID, Is.EqualTo(user.Id));
            Assert.That(found.Gamertag, Is.EqualTo("Grifter1"));
            Assert.That(found.Roles, Is.EquivalentTo(new[] { DiscordCaller.Roles.Commissioner }));
        }
    }

    [Test]
    public async Task Require_RepliesWithTheSetGamertagButton_ForAnUnknownUser()
    {
        var discord = new CommandContextRecorder(discordUserId: 31337);

        var result = await Caller().Require(discord.Context, DiscordCaller.Roles.Anyone);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.NoGamertag));
        }
    }

    [Test]
    public async Task Require_RepliesWithMissingRole_WhenTheCallerLacksEveryRole()
    {
        await AddUser(31337, "Grifter1", DiscordCaller.Roles.Player);
        var discord = new CommandContextRecorder(discordUserId: 31337);

        var result = await Caller().Require(discord.Context, DiscordCaller.Roles.CommissionerOnly);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.MissingRole(DiscordCaller.Roles.CommissionerOnly)));
        }
    }

    [Test]
    public async Task Require_ReturnsTheCaller_WhenTheyHaveOneOfTheRoles()
    {
        var user = await AddUser(31337, "Grifter1", DiscordCaller.Roles.Commissioner);
        var discord = new CommandContextRecorder(discordUserId: 31337);

        var result = await Caller().Require(discord.Context, DiscordCaller.Roles.Staff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result?.UserID, Is.EqualTo(user.Id));
            Assert.That(discord.Edits, Is.Empty);
        }
    }

    [Test]
    public async Task Require_ReturnsTheCaller_WhenNoRoleIsRequired()
    {
        var user = await AddUser(31337, "Grifter1");
        var discord = new CommandContextRecorder(discordUserId: 31337);

        var result = await Caller().Require(discord.Context, DiscordCaller.Roles.Anyone);

        Assert.That(result?.UserID, Is.EqualTo(user.Id));
    }

    [Test]
    public void MissingRole_JoinsTheRolesWithOr()
    {
        Assert.That(DiscordCaller.MissingRole([DiscordCaller.Roles.Commissioner, DiscordCaller.Roles.Sysadmin]), Is.EqualTo("Only a commissioner or sysadmin can do that."));
    }
}
