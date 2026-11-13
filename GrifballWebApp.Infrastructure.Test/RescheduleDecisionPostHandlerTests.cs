using DiscordInterface.Generated;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Reschedule;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;
using System.Text.Json;
using GrifballWebApp.Infrastructure.Outbox;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// RescheduleDecisionPostHandler, on a real (temp-file SQLite) GrifballContext with a substituted IDiscordRestClient:
/// no database server, host or network. Tells the reschedule's thread the decision, unless there's no thread, or a
/// retry finds it already told.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleDecisionPostHandlerTests
{
    private const ulong ThreadId = 444UL;

    private string _path = null!;
    private GrifballContext _context = null!;
    private IDiscordRestClient _discord = null!;
    private RescheduleDecisionPostHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-reschedule-decision-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        await _context.Database.EnsureCreatedAsync();
        _discord = Substitute.For<IDiscordRestClient>();
        _handler = new RescheduleDecisionPostHandler(_discord);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }

    private async Task<MatchReschedule> SeedReschedule(bool withThread)
    {
        var season = Season.Seed(seasonName: "Decision season");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        _context.Teams.AddRange(home, away);
        await _context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var requester = User.Register("decision-requester");
        _context.Users.Add(requester);
        await _context.SaveChangesAsync();
        var reschedule = MatchReschedule.Request(match.SeasonMatchID, null, null, "Can't make it", requester.Id, DateTime.UtcNow);
        if (withThread)
            reschedule.AttachThread(ThreadId);
        _context.MatchReschedules.Add(reschedule);
        await _context.SaveChangesAsync();
        return reschedule;
    }

    private static OutboxMessage Message(int rescheduleId, string content, int attempts = 0) => new()
    {
        Kind = RescheduleOutbox.DecisionPost,
        Payload = JsonSerializer.Serialize(new RescheduleDecisionPayload(rescheduleId, content), OutboxJson.Options),
        Attempts = attempts,
    };

    [Test]
    public async Task HandleAsync_NoThreadSaved_IsDoneWithoutMessaging()
    {
        var reschedule = await SeedReschedule(withThread: false);

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, "Approved"), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(_discord.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task HandleAsync_FirstAttempt_SendsTheDecision_WithoutLookingForAnExistingPost()
    {
        var reschedule = await SeedReschedule(withThread: true);

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, "Approved"), _context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.Received(1).SendMessageAsync(ThreadId, Arg.Is<MessageProperties>(m => m.Content == "Approved"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.DidNotReceive().GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>());
    }

    [Test]
    public async Task HandleAsync_ARetry_PostAlreadyFound_IsDoneWithoutSendingAgain()
    {
        var reschedule = await SeedReschedule(withThread: true);
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(1UL);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.Author.Id.Returns(1UL);
        posted.Content.Returns("Approved");
        _discord.GetMessagesAsync(ThreadId, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { posted }.ToAsyncEnumerable());

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, "Approved", attempts: 2), _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        }
        await _discord.DidNotReceive().SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }
}
