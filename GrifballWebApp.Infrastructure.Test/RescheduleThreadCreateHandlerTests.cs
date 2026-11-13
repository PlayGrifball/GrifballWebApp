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
public class RescheduleThreadCreateHandlerTests : SqliteContextFixture
{
    private const ulong Channel = 333UL;

    private IDiscordRestClient _discord = null!;
    private RescheduleThreadCreateHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _discord = Substitute.For<IDiscordRestClient>();
        _handler = new RescheduleThreadCreateHandler(_discord, Options.Create(new DiscordOptions { ReschedulesChannel = Channel }));
    }

    private async Task<MatchReschedule> SeedReschedule()
    {
        var season = Season.Seed(seasonName: "Reschedule create season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        var requester = User.Register("requester");
        Context.Users.Add(requester);
        await Context.SaveChangesAsync();
        var reschedule = MatchReschedule.Request(match.SeasonMatchID, null, null, "Can't make it", requester.Id, DateTime.UtcNow);
        Context.MatchReschedules.Add(reschedule);
        await Context.SaveChangesAsync();
        return reschedule;
    }

    private async Task SeedRequestPostResult(int rescheduleId, ulong messageId, bool found)
    {
        Context.Outbox.Add(new OutboxMessage
        {
            Kind = RescheduleOutbox.RequestPost,
            Payload = "{}",
            GroupKey = RescheduleOutbox.Group(rescheduleId),
            Sequence = 0,
            Status = OutboxStatus.Sent,
            Result = JsonSerializer.Serialize(new RescheduleRequestPostResult(messageId, found), OutboxJson.Options),
        });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
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
        await Context.SaveChangesAsync();

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

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

        Assert.That(async () => await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None),
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

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That((await Context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(901UL));
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

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, attempts: 2), Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That((await Context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(902UL));
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

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID, attempts: 2), Context, CancellationToken.None);

        Assert.That((await Context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(903UL));
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

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

        Assert.That((await Context.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(904UL));
    }
}
