using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>The right-click user commands (Profile, Pull games, Set gamertag), on a temp-file SQLite GrifballContext.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserCommandsUnitTests : SqliteGrifballContextFixture
{
    private readonly IDiscordGamertagSetter _setter = Substitute.For<IDiscordGamertagSetter>();

    private PlayerCommandsHandler Players() => new(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)), new DiscordCaller(Db),
        new ProfileStatsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db)),
        new ProfileCommendationsService(Db),
        new DeploymentHistoryService(Db),
        new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!),
        Urls(), null!);

    private UserCommandsHandler Handler() => new(new DiscordCaller(Db), _setter);

    [Test]
    public async Task Profile_ShowsTheProfileOfTheClickedDiscordUser()
    {
        await AddUser(777, "Clicked");
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Players().ProfileOfDiscordUser(discord.Context, 777);

        Assert.That(discord.Embed().Title, Is.EqualTo("Clicked"));
    }

    [Test]
    public async Task Profile_SaysSo_WhenTheClickedDiscordUserHasNoAccountWithAGamertag()
    {
        await AddDiscordOnlyUser(777);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Players().ProfileOfDiscordUser(discord.Context, 777);

        Assert.That(discord.Content(), Is.EqualTo(PlayerCommandsHandler.NoLinkedAccount));
    }

    [Test]
    public async Task Pull_SaysSo_WhenTheClickedDiscordUserHasNoAccountWithAGamertag()
    {
        await AddUser(1, "Caller", DiscordCaller.Roles.Player);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Players().PullOfDiscordUser(discord.Context, 777);

        Assert.That(discord.Content(), Is.EqualTo(PlayerCommandsHandler.NoLinkedAccount));
    }

    [Test]
    public async Task OpenSetGamertag_OpensTheModalForTheClickedUser_ForASysadmin()
    {
        await AddUser(1, "Admin", DiscordCaller.Roles.Sysadmin);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().OpenSetGamertag(discord.Context, 777, "clicked");

        var callback = discord.Callbacks.Single();
        Assert.That(callback.Type, Is.EqualTo(InteractionCallbackType.Modal));
    }

    [Test]
    public async Task OpenSetGamertag_OpensTheModal_ForASysadminWithNoGamertagToo()
    {
        var admin = await AddDiscordOnlyUser(1);
        Db.UserRoles.Add(new GrifballWebApp.Domain.Players.UserRole { UserId = admin.Id, RoleId = (await Db.Roles.SingleAsync(r => r.Name == DiscordCaller.Roles.Sysadmin)).Id });
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().OpenSetGamertag(discord.Context, 777, "clicked");

        Assert.That(discord.Callbacks.Single().Type, Is.EqualTo(InteractionCallbackType.Modal));
    }

    [Test]
    public async Task SetGamertag_SetsItThroughTheDiscordFlow_ForASysadmin()
    {
        await AddUser(1, "Admin", DiscordCaller.Roles.Sysadmin);
        _setter.SetGamertagForDiscordUser(777, "clicked", "NewTag", Arg.Any<CancellationToken>()).Returns((string?)null);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, "clicked", " NewTag ");

        await _setter.Received(1).SetGamertagForDiscordUser(777, "clicked", "NewTag", Arg.Any<CancellationToken>());
        Assert.That(discord.Content(), Does.Contain("NewTag").And.Contain("<@777>"));
    }

    [Test]
    public async Task SetGamertag_RepliesWhatTheFlowRefuses()
    {
        await AddUser(1, "Admin", DiscordCaller.Roles.Sysadmin);
        _setter.SetGamertagForDiscordUser(777, "clicked", "Taken", Arg.Any<CancellationToken>()).Returns("That gamertag belongs to someone else.");
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, "clicked", "Taken");

        Assert.That(discord.Content(), Is.EqualTo("That gamertag belongs to someone else."));
    }

    [Test]
    public async Task SetGamertag_RefusesABlankGamertag()
    {
        await AddUser(1, "Admin", DiscordCaller.Roles.Sysadmin);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, "clicked", "   ");

        Assert.That(discord.Content(), Is.EqualTo(UserCommandsHandler.NoGamertag));
        await _setter.DidNotReceiveWithAnyArgs().SetGamertagForDiscordUser(default, default, default!);
    }

    [Test]
    public async Task SetGamertag_Refuses_WhenTheSubmitterIsNotASysadmin()
    {
        await AddUser(1, "Commish", DiscordCaller.Roles.Commissioner);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, "clicked", "NewTag");

        Assert.That(discord.Content(), Is.EqualTo(UserCommandsHandler.NotSysadmin));
        await _setter.DidNotReceiveWithAnyArgs().SetGamertagForDiscordUser(default, default, default!);
    }

    [Test]
    public async Task OpenSetGamertag_RefusesWhoIsNotASysadmin()
    {
        await AddUser(1, "Commish", DiscordCaller.Roles.Commissioner);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().OpenSetGamertag(discord.Context, 777, "clicked");

        Assert.That(discord.Message().Content, Is.EqualTo(UserCommandsHandler.NotSysadmin));
    }
}
