using GrifballWebApp.Application;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// PlayerCommandsHandler's unit tests: the player lookup (<see cref="CommandLookups.Player"/>) and the account gate
/// (<see cref="DiscordCaller"/>) every action goes through first are real, on a temp-file SQLite GrifballContext (#402);
/// the profile services each goes on to stay null (GrifballWebApp.Integration.Test's, with a database).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PlayerCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private static UrlService Urls() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());

    private PlayerCommandsHandler Handler() => new(new CommandLookups(Db, null!), new DiscordCaller(Db), null!, null!, null!, null!, null!, Urls());

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
    public async Task Matches_RepliesNoSuchPlayer_WhenTheNamedPlayerIsNotFound()
    {
        var discord = new CommandContextRecorder();

        await Handler().Matches(discord.Context, "nonsense");

        Assert.That(discord.Content(), Is.EqualTo(PlayerCommandsHandler.NoSuchPlayer));
    }
}
