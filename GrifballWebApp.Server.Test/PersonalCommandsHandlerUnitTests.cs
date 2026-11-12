using GrifballWebApp.Application;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// PersonalCommandsHandler's unit tests: the account gate (<see cref="DiscordCaller"/>) and the season/match lookups
/// (<see cref="CommandLookups"/>) are real, on a temp-file SQLite GrifballContext (#402); the deeper services each action
/// goes on to stay null except where they are themselves only thin IApplicationDbContext reads (RegionService,
/// AvailabilityService, SeasonService), which are real too.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PersonalCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private static UrlService Urls() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());

    private IQueueRepository _queue = null!;

    private PersonalCommandsHandler Handler(SeasonService? seasons = null, AvailabilityService? availability = null, RegionService? regions = null)
    {
        _queue = Substitute.For<IQueueRepository>();
        return new PersonalCommandsHandler(new DiscordCaller(Db), new CommandLookups(Db, seasons ?? new SeasonService(Db, null!)),
            null!, _queue, null!, regions ?? new RegionService(Db), null!, null!, seasons ?? new SeasonService(Db, null!),
            availability ?? new AvailabilityService(Db), null!, Urls());
    }

    private async Task AssertRefusesUnknownCaller(Func<PersonalCommandsHandler, CommandContextRecorder, Task> call)
    {
        var discord = new CommandContextRecorder(discordUserId: 909090);

        await call(Handler(), discord);

        Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.NoGamertag));
    }

    [Test]
    public Task QueueJoin_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.QueueJoin(d.Context));

    [Test]
    public Task QueueLeave_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.QueueLeave(d.Context));

    [Test]
    public Task SetRegion_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.SetRegion(d.Context, "Europe"));

    [Test]
    public Task Claim_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Claim(d.Context, "SomeGamertag"));

    [Test]
    public Task Claims_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Claims(d.Context));

    [Test]
    public Task Logins_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Logins(d.Context));

    [Test]
    public Task Signup_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.Signup(d.Context, "season", null, null, null));

    [Test]
    public Task RequestReschedule_Refuses_AnUnknownCaller() => AssertRefusesUnknownCaller((h, d) => h.RequestReschedule(d.Context, "match", "reason", null));

    [Test]
    public async Task Signup_RepliesNoSuchSeason_WhenTheSeasonIsNotFound()
    {
        // Signup needs the Player (or Sysadmin) role, as the site's own signup endpoint does
        await AddUser(discordUserId: 42, gamertag: "Grifter1", roles: DiscordCaller.Roles.Player);
        var discord = new CommandContextRecorder(discordUserId: 42);

        await Handler().Signup(discord.Context, "nonsense", null, null, null);

        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchSeason));
    }

    [Test]
    public async Task RequestReschedule_RepliesNoSuchMatch_WhenTheMatchIsNotFound()
    {
        await AddUser(discordUserId: 42, gamertag: "Grifter1");
        var discord = new CommandContextRecorder(discordUserId: 42);

        await Handler().RequestReschedule(discord.Context, "nonsense", "Can't make it", null);

        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchMatch));
    }

    [Test]
    public async Task SetRegion_RepliesNoSuchRegion_ForAnUnknownRegion()
    {
        await AddUser(discordUserId: 42, gamertag: "Grifter1");
        var discord = new CommandContextRecorder(discordUserId: 42);

        await Handler().SetRegion(discord.Context, "Mars");

        Assert.That(discord.Content(), Does.StartWith(PersonalCommandsHandler.NoSuchRegion));
    }

    [Test]
    public async Task SetRegion_SetsTheCallersRegion_ByName()
    {
        await AddUser(discordUserId: 42, gamertag: "Grifter1");
        var discord = new CommandContextRecorder(discordUserId: 42);

        await Handler().SetRegion(discord.Context, "Australia");

        Assert.That(discord.Content(), Is.EqualTo("Your region is set."));
    }

    [Test]
    public async Task SetRegion_ClearsTheCallersRegion_ForNone()
    {
        await AddUser(discordUserId: 42, gamertag: "Grifter1");
        var discord = new CommandContextRecorder(discordUserId: 42);

        await Handler().SetRegion(discord.Context, "none");

        Assert.That(discord.Content(), Is.EqualTo("Your region is cleared."));
    }

    [Test]
    public async Task AvailabilityShow_RepliesNoSuchSeason_WhenTheSeasonIsNotFound()
    {
        var discord = new CommandContextRecorder();

        await Handler().AvailabilityShow(discord.Context, "nonsense");

        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchSeason));
    }

    [Test]
    public async Task AvailabilityShow_RepliesWithNoTimesSetYet_ForASeasonWithNoTimeslots()
    {
        await AddSeason("Winter");
        var discord = new CommandContextRecorder();

        await Handler().AvailabilityShow(discord.Context, "Winter");

        Assert.That(discord.Embed().Description, Does.Contain("No times are set yet."));
    }

    [Test]
    public async Task QueueStatus_RepliesTheQueueCount_ForAnUnknownCaller()
    {
        var handler = Handler();
        _queue.Count().Returns(0);
        var discord = new CommandContextRecorder();

        await handler.QueueStatus(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo("0 players are in the Late League queue."));
    }

    [Test]
    public async Task QueueStatus_TellsAKnownCaller_WhetherTheyAreInTheQueue()
    {
        var handler = Handler();
        _queue.PlayerStateByDiscord(Arg.Any<long>()).Returns(new QueuePlayerState(1, true, InQueue: true, InMatch: false, QueueCount: 3));
        var discord = new CommandContextRecorder();

        await handler.QueueStatus(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo("3 players are in the Late League queue.\nYou are in the queue."));
    }

    [Test]
    public async Task QueueStatus_TellsAKnownCaller_WhenTheyAreInAMatch()
    {
        var handler = Handler();
        _queue.PlayerStateByDiscord(Arg.Any<long>()).Returns(new QueuePlayerState(1, true, InQueue: false, InMatch: true, QueueCount: 1));
        var discord = new CommandContextRecorder();

        await handler.QueueStatus(discord.Context);

        Assert.That(discord.Content(), Is.EqualTo("1 player is in the Late League queue.\nYou are in a Late League match."));
    }
}
