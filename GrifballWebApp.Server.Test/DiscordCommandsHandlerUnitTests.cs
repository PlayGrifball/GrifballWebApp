using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordCommandsHandler's unit tests, on a real GrifballContext (temp-file SQLite, #402): the Xbox lookup
/// (<see cref="IGetsertXboxUserService"/>) and the Halo pull (<see cref="IDataPullService"/>) are substituted; the rest
/// is the real GrifballContext query.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private IGetsertXboxUserService _getsert = null!;
    private IDataPullService _dataPull = null!;
    private RecordingLogger<DiscordCommandsHandler> _logger = null!;

    private DiscordCommandsHandler Handler()
    {
        _getsert = Substitute.For<IGetsertXboxUserService>();
        _dataPull = Substitute.For<IDataPullService>();
        _logger = new RecordingLogger<DiscordCommandsHandler>();
        return new DiscordCommandsHandler(Db, _logger, _dataPull, _getsert);
    }

    [Test]
    public async Task RecentMatchesDeferred_RepliesTheServicesMessage_WhenTheNamedGamertagHasNoXboxAccount()
    {
        var handler = Handler();
        _getsert.GetsertXboxUserByGamertag("nonsense").Returns(((XboxUser?)null, "I found no such gamertag."));
        var discord = new CommandContextRecorder();

        await handler.RecentMatchesDeferred(discord.Context, "nonsense");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("I found no such gamertag."));
            Assert.That(_dataPull.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task RecentMatchesDeferred_RepliesToSetAGamertag_ForAnUnknownCaller_WithNoneNamed()
    {
        var handler = Handler();
        var discord = new CommandContextRecorder();

        await handler.RecentMatchesDeferred(discord.Context, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Content(), Is.EqualTo("You must set your gamertag first, or provide someone else's gamertag to search for"));
            Assert.That(_dataPull.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task RecentMatchesDeferred_RepliesNoMatchesFound_ForAPlayerWithNonePulled()
    {
        var handler = Handler();
        var xboxUser = XboxUser.FromXbox(1001, "Grifter1");
        _getsert.GetsertXboxUserByGamertag("Grifter1").Returns((xboxUser, (string?)null));
        var discord = new CommandContextRecorder();

        await handler.RecentMatchesDeferred(discord.Context, "Grifter1");

        Assert.That(discord.Content(), Is.EqualTo("I found no matches for Grifter1"));
    }

    [Test]
    public async Task RecentMatchesDeferred_RepliesAFailureMessage_WhenThePullThrows()
    {
        var handler = Handler();
        var xboxUser = XboxUser.FromXbox(1001, "Grifter1");
        _getsert.GetsertXboxUserByGamertag("Grifter1").Returns((xboxUser, (string?)null));
        _dataPull.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), 0, 0, 5).ThrowsAsync(new InvalidOperationException("boom"));
        var discord = new CommandContextRecorder();

        await handler.RecentMatchesDeferred(discord.Context, "Grifter1");

        Assert.That(discord.Content(), Is.EqualTo("Failed to fetch recent matches, if this continues to occur contact sysadmin"));
    }
}
