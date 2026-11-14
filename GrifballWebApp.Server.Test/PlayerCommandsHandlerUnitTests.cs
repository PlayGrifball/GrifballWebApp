using GrifballWebApp.Application;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// PlayerCommandsHandler's unit tests: the player lookup (<see cref="CommandLookups.Player"/>) and the account gate
/// (<see cref="DiscordCaller"/>) every action goes through first are real, on a temp-file SQLite GrifballContext (#402).
/// <see cref="Handler"/> leaves the profile services null, for the lookup/gate guards; <see cref="DeepHandler"/> makes
/// them real too, for the actions themselves.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PlayerCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private PlayerCommandsHandler Handler() => new(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)), new DiscordCaller(Db), null!, null!, null!, null!, Urls(), null!);

    /// <summary>The profile services real too (all IApplicationDbContext-backed); the pull tools stay null, unused by
    /// Profile/Stats/Commendations/History/Vods.</summary>
    private PlayerCommandsHandler DeepHandler() => new(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)), new DiscordCaller(Db),
        new ProfileStatsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db)),
        new ProfileCommendationsService(Db),
        new DeploymentHistoryService(Db),
        new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!),
        Urls(), null!);

    [TestCase(nameof(PlayerCommandsHandler.Profile))]
    [TestCase(nameof(PlayerCommandsHandler.Stats))]
    [TestCase(nameof(PlayerCommandsHandler.Commendations))]
    [TestCase(nameof(PlayerCommandsHandler.History))]
    [TestCase(nameof(PlayerCommandsHandler.Vods))]
    public async Task Actions_ReplyNoSuchPlayer_WhenTheNamedPlayerIsNotFound(string method)
    {
        var discord = new CommandContextRecorder();
        var handler = Handler();

        Task task = method switch
        {
            nameof(PlayerCommandsHandler.Profile) => handler.Profile(discord.Context, "nonsense"),
            nameof(PlayerCommandsHandler.Stats) => handler.Stats(discord.Context, "nonsense"),
            nameof(PlayerCommandsHandler.Commendations) => handler.Commendations(discord.Context, "nonsense"),
            nameof(PlayerCommandsHandler.History) => handler.History(discord.Context, "nonsense"),
            _ => handler.Vods(discord.Context, "nonsense"),
        };
        await task;

        Assert.That(discord.Content(), Is.EqualTo(PlayerCommandsHandler.NoSuchPlayer));
    }

    [Test]
    public async Task Profile_Refuses_AnUnknownCaller_WhenNoPlayerIsNamed()
    {
        var discord = new CommandContextRecorder(discordUserId: 909090);

        await Handler().Profile(discord.Context, null);

        Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.NoGamertag));
    }

    [Test]
    public async Task Profile_ShowsTheirProfile_ForAFoundPlayer()
    {
        await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var discord = new CommandContextRecorder();

        await DeepHandler().Profile(discord.Context, "Grifter1");

        Assert.That(discord.Embed().Title, Is.EqualTo("Grifter1"));
    }

    [Test]
    public async Task Stats_RepliesNoStatsYet_ForAFoundPlayerWithNoStats()
    {
        await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var discord = new CommandContextRecorder();

        await DeepHandler().Stats(discord.Context, "Grifter1");

        Assert.That(discord.Embed().Description, Does.Contain("No stats yet."));
    }

    [Test]
    public async Task Commendations_RepliesNoMedalsYet_ForAFoundPlayerWithNone()
    {
        await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var discord = new CommandContextRecorder();

        await DeepHandler().Commendations(discord.Context, "Grifter1");

        Assert.That(discord.Embed().Description, Does.Contain("No medals yet."));
    }

    [Test]
    public async Task History_RepliesNoSeasonsYet_ForAFoundPlayerWithNone()
    {
        await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var discord = new CommandContextRecorder();

        await DeepHandler().History(discord.Context, "Grifter1");

        Assert.That(discord.Embed().Description, Does.Contain("No seasons yet."));
    }

    [Test]
    public async Task Vods_RepliesNoVodsYet_ForAFoundPlayerWithNone()
    {
        await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var discord = new CommandContextRecorder();

        await DeepHandler().Vods(discord.Context, "Grifter1");

        Assert.That(discord.Embed().Description, Does.Contain("No VODs yet."));
    }
}
