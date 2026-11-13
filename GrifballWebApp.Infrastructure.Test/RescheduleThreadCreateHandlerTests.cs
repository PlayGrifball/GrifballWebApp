using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Reschedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using System.Text.Json;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// RescheduleThreadCreateHandler, on a real (temp-file SQLite) GrifballContext with a substituted IDiscordRestClient:
/// no database server, host or network. Makes the reschedule's thread from its request post, or keeps a thread a
/// retry (or the post found) already made from it.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleThreadCreateHandlerTests
{
    private const ulong Channel = 333UL;

    private string _path = null!;
    private GrifballContext _context = null!;
    private IDiscordRestClient _discord = null!;
    private RescheduleThreadCreateHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-reschedule-create-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        await _context.Database.EnsureCreatedAsync();
        _discord = Substitute.For<IDiscordRestClient>();
        _handler = new RescheduleThreadCreateHandler(_discord, Options.Create(new DiscordOptions { ReschedulesChannel = Channel }));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }

    private async Task<MatchReschedule> SeedReschedule()
    {
        var season = Season.Seed(seasonName: "Reschedule create season");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        _context.Teams.AddRange(home, away);
        await _context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var requester = User.Register("requester");
        _context.Users.Add(requester);
        await _context.SaveChangesAsync();
        var reschedule = MatchReschedule.Request(match.SeasonMatchID, null, null, "Can't make it", requester.Id, DateTime.UtcNow);
        _context.MatchReschedules.Add(reschedule);
        await _context.SaveChangesAsync();
        return reschedule;
    }

    private async Task SeedRequestPostResult(int rescheduleId, ulong messageId, bool found)
    {
        _context.Outbox.Add(new OutboxMessage
        {
            Kind = RescheduleOutbox.RequestPost,
            Payload = "{}",
            GroupKey = RescheduleOutbox.Group(rescheduleId),
            Sequence = 0,
            Status = OutboxStatus.Sent,
            Result = JsonSerializer.Serialize(new RescheduleRequestPostResult(messageId, found), OutboxJson.Options),
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private static OutboxMessage Message(int rescheduleId, int attempts = 0) => new()
    {
        Kind = RescheduleOutbox.ThreadCreate,
        Payload = JsonSerializer.Serialize(new RescheduleThreadPayload(rescheduleId, "Reschedule thread"), OutboxJson.Options),
        Attempts = attempts,
    };

    [Test]
    public async Task HandleAsync_AThreadAlreadySaved_IsDoneWithoutAskingDiscordAnything()
    {
        var reschedule = await SeedReschedule();
        reschedule.AttachThread(777UL);
        await _context.SaveChangesAsync();

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(_discord.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task HandleAsync_NoRequestPostResult_Throws()
    {
        var reschedule = await SeedReschedule();

        Assert.That(async () => await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("has no request post to make its thread from"));
    }

    [Test]
    public async Task HandleAsync_FirstAttempt_PostNotFound_CreatesANewThread()
    {
        var reschedule = await SeedReschedule();
        await SeedRequestPostResult(reschedule.MatchRescheduleID, messageId: 50UL, found: false);
        var thread = Substitute.For<IDiscordGuildThread>();
        thread.Id.Returns(901UL);
        _discord.CreateGuildThreadAsync(Channel, 50UL, Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(thread);

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That((await _context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(901UL));
        }
        await _discord.DidNotReceive().GetMessageAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_ARetry_WithAThreadAlreadyStarted_TakesIt_WithoutMakingANewOne()
    {
        var reschedule = await SeedReschedule();
        await SeedRequestPostResult(reschedule.MatchRescheduleID, messageId: 50UL, found: false);
        var existingThread = Substitute.For<IDiscordGuildThread>();
        existingThread.Id.Returns(902UL);
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.StartedThread.Returns(existingThread);
        _discord.GetMessageAsync(Channel, 50UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, attempts: 2), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That((await _context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(902UL));
        }
        await _discord.DidNotReceive().CreateGuildThreadAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_ARetry_ButNoThreadWasStartedYet_MakesOne()
    {
        var reschedule = await SeedReschedule();
        await SeedRequestPostResult(reschedule.MatchRescheduleID, messageId: 50UL, found: false);
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.StartedThread.Returns((IDiscordGuildThread?)null);
        _discord.GetMessageAsync(Channel, 50UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var thread = Substitute.For<IDiscordGuildThread>();
        thread.Id.Returns(903UL);
        _discord.CreateGuildThreadAsync(Channel, 50UL, Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(thread);

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, attempts: 2), _context, CancellationToken.None);

        Assert.That((await _context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(903UL));
    }

    [Test]
    public async Task HandleAsync_PostWasFoundRatherThanMade_LooksForAnExistingThread_EvenOnTheFirstAttempt()
    {
        var reschedule = await SeedReschedule();
        await SeedRequestPostResult(reschedule.MatchRescheduleID, messageId: 60UL, found: true);
        var existingThread = Substitute.For<IDiscordGuildThread>();
        existingThread.Id.Returns(904UL);
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.StartedThread.Returns(existingThread);
        _discord.GetMessageAsync(Channel, 60UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        Assert.That((await _context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(904UL));
    }
}
