using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Reschedule;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;

namespace GrifballWebApp.Test;

/// <summary>The reschedule outbox handlers, on the edges the service tests don't reach.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleOutboxHandlerTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discord;
    private OutboxTestDriver _outbox;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discord = Substitute.For<IDiscordRestClient>();
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        _outbox = new OutboxTestDriver(_context, _discord, new DiscordOptions { ReschedulesChannel = 123UL });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _outbox.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

    private async Task<MatchReschedule> Reschedule(ulong? threadId)
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        return await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: threadId);
    }

    private async Task Enqueue<T>(string kind, T payload, int rescheduleId)
    {
        await new ContextOutbox(_context).EnqueueAsync(kind, payload, RescheduleOutbox.Group(rescheduleId));
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task DecisionPost_ThreadDeleted_IsDone()
    {
        var r = await Reschedule(threadId: 50UL);
        _discord.SendMessageAsync(50UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestException(HttpStatusCode.NotFound, "Not Found", new RestError(10003, "Unknown Channel", null)));
        await Enqueue(RescheduleOutbox.DecisionPost, new RescheduleDecisionPayload(r.MatchRescheduleID, "Your request has been Approved by x"), r.MatchRescheduleID);

        await _outbox.DrainAsync();

        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
    }

    [Test]
    public async Task DecisionPost_NoThreadBySendTime_SendsNothing()
    {
        var r = await Reschedule(threadId: null);
        await Enqueue(RescheduleOutbox.DecisionPost, new RescheduleDecisionPayload(r.MatchRescheduleID, "x"), r.MatchRescheduleID);

        await _outbox.DrainAsync();

        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
        Assert.That(_discord.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task DecisionPost_AlreadyPostedByAFailedTry_IsNotPostedAgain()
    {
        var r = await Reschedule(threadId: 50UL);
        const string content = "Your request has been Rejected by x";
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.Content.Returns(content);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<IDiscordCurrentUser>());
        _discord.GetMessagesAsync(50UL, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { posted }.ToAsyncEnumerable());
        var calls = 0;
        _discord.SendMessageAsync(50UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1 ? Task.FromException<IDiscordRestMessage>(new TaskCanceledException("timed out")) : Task.FromResult(posted));
        await Enqueue(RescheduleOutbox.DecisionPost, new RescheduleDecisionPayload(r.MatchRescheduleID, content), r.MatchRescheduleID);

        await _outbox.DrainAsync();

        Assert.That(calls, Is.EqualTo(1));
        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
    }

    [Test]
    public async Task ThreadSteps_WithoutTheStepsBefore_Fail_AndAnInviteWhoseErrorsWerePosted_IsDone()
    {
        var r = await Reschedule(threadId: null);
        await new ContextOutbox(_context).EnqueueAsync(RescheduleOutbox.ThreadCreate, new RescheduleThreadPayload(r.MatchRescheduleID, "t"), "a");
        await new ContextOutbox(_context).EnqueueAsync(RescheduleOutbox.ThreadInvite, new ReschedulePayload(r.MatchRescheduleID), "b");
        await _context.SaveChangesAsync();
        _context.Outbox.Add(new OutboxMessage { Kind = RescheduleOutbox.ThreadInvite, Payload = """{"rescheduleId":1}""", Result = """{"errorsPosted":true}""" });
        await _context.SaveChangesAsync();

        await _outbox.RunDueAsync();

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(messages[0].Status, Is.EqualTo(OutboxStatus.Failed));
            Assert.That(messages[0].LastError, Is.EqualTo($"Reschedule {r.MatchRescheduleID} has no request post to make its thread from"));
            Assert.That(messages[1].LastError, Is.EqualTo($"Reschedule {r.MatchRescheduleID} has no thread"));
            Assert.That(messages[2].Status, Is.EqualTo(OutboxStatus.Sent), "Its errors were posted: not again");
        });
    }

    [Test]
    public async Task ThreadCreate_ThreadAlreadySaved_IsKept()
    {
        var r = await Reschedule(threadId: 50UL);
        await Enqueue(RescheduleOutbox.ThreadCreate, new RescheduleThreadPayload(r.MatchRescheduleID, "t"), r.MatchRescheduleID);

        await _outbox.DrainAsync();

        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
        Assert.That(_discord.ReceivedCalls(), Is.Empty);
    }
}
