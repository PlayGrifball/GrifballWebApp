using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Server.Commands;
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

    [Test]
    public async Task Info_RepliesWithTheMatch_WhenItIsFound()
    {
        var season = await AddSeason("Winter League");
        var home = await AddTeam(season, "Home");
        var away = await AddTeam(season, "Away");
        var match = await AddSeasonMatch(season, home, away);
        var discord = new CommandContextRecorder();

        await Handler().Info(discord.Context, match.SeasonMatchID.ToString());

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.True);
            Assert.That(embed.Title, Is.EqualTo("Home vs Away"));
            Assert.That(embed.Fields!.ToList()[0].Value, Does.Contain("Winter League"));
        }
    }

    [Test]
    public async Task Game_RepliesWithTheMatch_WhenItHasBeenPulled()
    {
        var matchID = Guid.NewGuid();
        Db.Matches.Add(GrifballWebApp.Domain.Halo.Match.Seed(matchID));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().Game(discord.Context, matchID.ToString());

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.True);
            Assert.That(embed.Title, Is.EqualTo("Halo Infinite match"));
            Assert.That(embed.Fields!.ToList(), Has.Count.EqualTo(2));
        }
    }
}
