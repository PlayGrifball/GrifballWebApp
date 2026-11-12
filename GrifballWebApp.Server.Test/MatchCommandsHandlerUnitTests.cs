using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// MatchCommandsHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402) for the lookups and services it
/// goes through: no database server, no host. Open to everyone: no DiscordCaller gate.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private static UrlService Urls() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());

    private MatchCommandsHandler Handler()
    {
        var seasonMatches = new SeasonMatchService(Db, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(), new ChampionshipService(Db));
        var matchDetails = new MatchDetailsService(Db);
        return new MatchCommandsHandler(new CommandLookups(Db, null!), seasonMatches, matchDetails, Urls());
    }

    [Test]
    public async Task Info_DefersPublicly_ThenRepliesNoSuchMatch_WhenTheMatchIsNotFound()
    {
        var discord = new CommandContextRecorder();

        await Handler().Info(discord.Context, "nonsense");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.True);
            Assert.That(discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchMatch));
        }
    }

    [Test]
    public async Task Game_RepliesNoSuchGame_ForAnUnparsableID()
    {
        var discord = new CommandContextRecorder();

        await Handler().Game(discord.Context, "not-a-guid");

        Assert.That(discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
    }

    [Test]
    public async Task Game_RepliesNoSuchGame_ForAGuidThatWasNeverPulled()
    {
        var discord = new CommandContextRecorder();

        await Handler().Game(discord.Context, Guid.NewGuid().ToString());

        Assert.That(discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
    }
}
