using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>The right-click user commands (Profile, Pull games, Set gamertag), on a temp-file SQLite GrifballContext.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserCommandsUnitTests : SqliteGrifballContextFixture
{
    private readonly ISetGamertagService _setGamertag = Substitute.For<ISetGamertagService>();

    private PlayerCommandsHandler Players() => new(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)), new DiscordCaller(Db),
        new ProfileStatsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db)),
        new ProfileCommendationsService(Db),
        new DeploymentHistoryService(Db),
        new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!),
        Urls(), null!);

    private UserCommandsHandler Handler() => new(new DiscordCaller(Db), Db, _setGamertag);

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

        await Handler().OpenSetGamertag(discord.Context, 777);

        var callback = discord.Callbacks.Single();
        Assert.That(callback.Type, Is.EqualTo(InteractionCallbackType.Modal));
    }

    [Test]
    public async Task SetGamertag_SetsItThroughThePlayersService_ForASysadmin()
    {
        await AddUser(1, "Admin", DiscordCaller.Roles.Sysadmin);
        var target = await AddDiscordOnlyUser(777);
        _setGamertag.SetGamertag(target.Id, "NewTag").Returns((string?)null);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, " NewTag ");

        await _setGamertag.Received(1).SetGamertag(target.Id, "NewTag");
        Assert.That(discord.Content(), Does.Contain("NewTag"));
    }

    [Test]
    public async Task SetGamertag_RepliesWhatTheServiceRefuses_AndPointsToTheEditUserPage()
    {
        await AddUser(1, "Admin", DiscordCaller.Roles.Sysadmin);
        var target = await AddDiscordOnlyUser(777);
        _setGamertag.SetGamertag(target.Id, "Taken").Returns("That gamertag belongs to someone else.");
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, "Taken");

        Assert.That(discord.Content(), Does.StartWith("That gamertag belongs to someone else.").And.Contain("Edit User"));
    }

    [Test]
    public async Task SetGamertag_RefusesAUserWithNoSiteAccount()
    {
        await AddUser(1, "Admin", DiscordCaller.Roles.Sysadmin);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, "NewTag");

        Assert.That(discord.Content(), Is.EqualTo(UserCommandsHandler.NoAccount));
        await _setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!);
    }

    [Test]
    public async Task SetGamertag_Refuses_WhenTheSubmitterIsNotASysadmin()
    {
        await AddUser(1, "Commish", DiscordCaller.Roles.Commissioner);
        await AddDiscordOnlyUser(777);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().SetGamertag(discord.Context, 777, "NewTag");

        Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.MissingRole(DiscordCaller.Roles.SysadminOnly)));
        await _setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!);
    }

    [Test]
    public async Task OpenSetGamertag_RefusesWhoIsNotASysadmin()
    {
        await AddUser(1, "Commish", DiscordCaller.Roles.Commissioner);
        var discord = new CommandContextRecorder(discordUserId: 1);

        await Handler().OpenSetGamertag(discord.Context, 777);

        Assert.That(discord.Message().Content, Is.EqualTo(UserCommandsHandler.NotSysadmin));
    }
}
