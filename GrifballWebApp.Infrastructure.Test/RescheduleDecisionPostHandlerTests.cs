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
public class RescheduleDecisionPostHandlerTests : SqliteContextFixture
{
    private const ulong ThreadId = 444UL;

    private IDiscordRestClient _discord = null!;
    private RescheduleDecisionPostHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _discord = Substitute.For<IDiscordRestClient>();
        _handler = new RescheduleDecisionPostHandler(_discord);
    }

    private async Task<MatchReschedule> SeedReschedule(bool withThread)
    {
        var season = Season.Seed(seasonName: "Decision season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        var requester = User.Register("decision-requester");
        Context.Users.Add(requester);
        await Context.SaveChangesAsync();
        var reschedule = MatchReschedule.Request(match.SeasonMatchID, null, null, "Can't make it", requester.Id, DateTime.UtcNow);
        if (withThread)
            reschedule.AttachThread(ThreadId);
        Context.MatchReschedules.Add(reschedule);
        await Context.SaveChangesAsync();
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

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, "Approved"), Context, CancellationToken.None);

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

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, "Approved"), Context, CancellationToken.None);

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

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, "Approved", attempts: 2), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        }
        await _discord.DidNotReceive().SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }
}
