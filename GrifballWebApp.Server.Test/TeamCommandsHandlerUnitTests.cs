using GrifballWebApp.Application;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// TeamCommandsHandler's unit tests: the team lookup (<see cref="CommandLookups.Team"/>) every action goes through first
/// is real, on a temp-file SQLite GrifballContext (#402); the team page services each goes on to stay null
/// (GrifballWebApp.Integration.Test's, with a database).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private static UrlService Urls() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());

    private TeamCommandsHandler Handler() => new(new CommandLookups(Db, null!), null!, null!, null!, Urls());

    [TestCase(nameof(TeamCommandsHandler.Info))]
    [TestCase(nameof(TeamCommandsHandler.Roster))]
    [TestCase(nameof(TeamCommandsHandler.History))]
    [TestCase(nameof(TeamCommandsHandler.Matches))]
    [TestCase(nameof(TeamCommandsHandler.Vods))]
    public async Task Actions_ReplyNoSuchTeam_WhenTheNamedTeamIsNotFound(string method)
    {
        var discord = new CommandContextRecorder();
        var handler = Handler();

        Task task = method switch
        {
            nameof(TeamCommandsHandler.Info) => handler.Info(discord.Context, "nonsense"),
            nameof(TeamCommandsHandler.Roster) => handler.Roster(discord.Context, "nonsense"),
            nameof(TeamCommandsHandler.History) => handler.History(discord.Context, "nonsense"),
            nameof(TeamCommandsHandler.Matches) => handler.Matches(discord.Context, "nonsense"),
            _ => handler.Vods(discord.Context, "nonsense"),
        };
        await task;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.True);
            Assert.That(discord.Content(), Is.EqualTo(TeamCommandsHandler.NoSuchTeam));
        }
    }
}
