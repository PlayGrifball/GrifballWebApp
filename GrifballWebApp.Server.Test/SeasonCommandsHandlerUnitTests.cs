using GrifballWebApp.Application;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SeasonCommandsHandler's unit tests: the season lookup (<see cref="CommandLookups.SeasonFor"/>) and name
/// (<see cref="SeasonService.GetSeasonName"/>) every action goes through first are real, on a temp-file SQLite
/// GrifballContext (#402); the season page services each goes on to stay null (GrifballWebApp.Integration.Test's,
/// with a database). <see cref="SeasonCommandsHandler.Draft"/> needs nothing past that, so it is covered fully.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private static UrlService Urls() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());

    private SeasonCommandsHandler Handler()
    {
        var seasons = new SeasonService(Db, null!);
        return new SeasonCommandsHandler(new CommandLookups(Db, seasons), seasons, null!, null!, null!, null!, null!, Urls());
    }

    [TestCase(nameof(SeasonCommandsHandler.Info))]
    [TestCase(nameof(SeasonCommandsHandler.Standings))]
    [TestCase(nameof(SeasonCommandsHandler.Schedule))]
    [TestCase(nameof(SeasonCommandsHandler.Grades))]
    [TestCase(nameof(SeasonCommandsHandler.Signups))]
    [TestCase(nameof(SeasonCommandsHandler.Bracket))]
    [TestCase(nameof(SeasonCommandsHandler.Draft))]
    public async Task Actions_ReplyNoSuchSeason_WhenTheNamedSeasonIsNotFound(string method)
    {
        var discord = new CommandContextRecorder();
        var handler = Handler();

        Task task = method switch
        {
            nameof(SeasonCommandsHandler.Info) => handler.Info(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Standings) => handler.Standings(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Schedule) => handler.Schedule(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Grades) => handler.Grades(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Signups) => handler.Signups(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Bracket) => handler.Bracket(discord.Context, "nonsense"),
            _ => handler.Draft(discord.Context, "nonsense"),
        };
        await task;

        Assert.That(discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task Stats_RepliesNoSuchSeason_WhenTheNamedSeasonIsNotFound()
    {
        var discord = new CommandContextRecorder();

        await Handler().Stats(discord.Context, "nonsense", SeasonStat.Goals);

        Assert.That(discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task Draft_LinksToTheLiveDraftBoard_WhenTheSeasonIsFound()
    {
        var season = await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await Handler().Draft(discord.Context, "Winter 2026");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Winter 2026 draft"));
            Assert.That(embed.Description, Is.EqualTo("The draft board is live on the site."));
        }
    }
}
