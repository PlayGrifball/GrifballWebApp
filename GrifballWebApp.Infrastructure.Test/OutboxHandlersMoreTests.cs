using System.Net;
using System.Text.Json;
using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.Events;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Reschedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// More of the outbox's Discord handlers, on a real (temp-file SQLite) GrifballContext with a substituted Discord
/// client: a season's announcement (posted, or edited by the id its last send kept or found in the channel, posted
/// again when deleted); a match thread already gone when it's closed; a reschedule request's post (once: a retry finds
/// the earlier try's); and each handler's kind.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OutboxHandlersMoreTests : SqliteContextFixture
{
    private const ulong EventsChannel = 444UL;
    private const ulong ReschedulesChannel = 555UL;
    private const ulong BotId = 1UL;
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDiscordRestClient _discord = Substitute.For<IDiscordRestClient>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));
    private IOptions<DiscordOptions> _options = null!;

    [SetUp]
    public void SetUp()
    {
        _options = Options.Create(new DiscordOptions { EventsChannel = EventsChannel, ReschedulesChannel = ReschedulesChannel });
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(BotId);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
    }

    private static RestException NotFound() => new(HttpStatusCode.NotFound, "Not Found", new RestError(10008, "Unknown Message", null));

    private static OutboxMessage Message<T>(string kind, T payload, int attempts = 1, object? result = null) => new()
    {
        Kind = kind,
        Payload = JsonSerializer.Serialize(payload, OutboxJson.Options),
        Attempts = attempts,
        CreatedAt = Now,
        Result = result is null ? null : JsonSerializer.Serialize(result, OutboxJson.Options),
    };

    private static IDiscordRestMessage DiscordMessage(ulong id, string? content = null)
    {
        var message = Substitute.For<IDiscordRestMessage>();
        message.Id.Returns(id);
        message.Author.Id.Returns(BotId);
        message.Content.Returns(content);
        message.CreatedAt.Returns(new DateTimeOffset(Now));
        return message;
    }

    private void InChannel(ulong channel, IDiscordRestMessage message) =>
        _discord.GetMessagesAsync(channel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => new[] { message }.ToAsyncEnumerable());

    // ---- SeasonAnnouncementHandler ----

    private static readonly SeasonAnnouncement Announcement = new(7, "Fall League", "Signups are open", SignupsOpen: true);

    private SeasonAnnouncementHandler Announcer() => new(_discord, _options);

    private static ulong? ResultId(OutboxMessage message) => JsonSerializer.Deserialize<SeasonAnnouncementResult>(message.Result!, OutboxJson.Options)!.MessageId;

    [Test]
    public async Task Announcement_NeverPosted_IsPosted_AndItsIdKept()
    {
        var posted = DiscordMessage(500);
        _discord.UpsertMessageAsync(EventsChannel, null, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var message = Message(EventsOutbox.SeasonUpsert, Announcement);

        var outcome = await Announcer().HandleAsync(message, Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(ResultId(message), Is.EqualTo(500UL));
        }
        await _discord.Received(1).UpsertMessageAsync(EventsChannel, null, Arg.Is<MessageProperties>(m => m.Content == "Fall League"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Announcement_SentBefore_IsEditedByTheIdItsLastSendKept()
    {
        Context.Outbox.Add(new OutboxMessage
        {
            Kind = EventsOutbox.SeasonUpsert,
            Payload = "{}",
            GroupKey = EventsOutbox.Group(7),
            Status = OutboxStatus.Sent,
            CreatedAt = Now.AddDays(-1),
            NextAttemptAt = Now.AddDays(-1),
            Result = JsonSerializer.Serialize(new SeasonAnnouncementResult(500), OutboxJson.Options),
        });
        await Context.SaveChangesAsync();
        var edited = DiscordMessage(500);
        _discord.UpsertMessageAsync(EventsChannel, 500UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(edited);
        var message = Message(EventsOutbox.SeasonUpsert, Announcement);

        await Announcer().HandleAsync(message, Context, CancellationToken.None);

        Assert.That(ResultId(message), Is.EqualTo(500UL));
        await _discord.Received(1).UpsertMessageAsync(EventsChannel, 500UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        _discord.DidNotReceiveWithAnyArgs().GetMessagesAsync(default, default!, default);
    }

    [Test]
    public async Task Announcement_WithNoKeptId_EditsTheBotsPostOfTheSeasonInTheChannel()
    {
        InChannel(EventsChannel, DiscordMessage(600, "Fall League"));
        var edited = DiscordMessage(600);
        _discord.UpsertMessageAsync(EventsChannel, 600UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(edited);
        var message = Message(EventsOutbox.SeasonUpsert, Announcement);

        await Announcer().HandleAsync(message, Context, CancellationToken.None);

        Assert.That(ResultId(message), Is.EqualTo(600UL));
    }

    [Test]
    public async Task Announcement_DeletedSince_IsPostedAgain()
    {
        InChannel(EventsChannel, DiscordMessage(600, "Fall League"));
        var reposted = DiscordMessage(700);
        _discord.UpsertMessageAsync(EventsChannel, 600UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(NotFound());
        _discord.UpsertMessageAsync(EventsChannel, null, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(reposted);
        var message = Message(EventsOutbox.SeasonUpsert, Announcement);

        await Announcer().HandleAsync(message, Context, CancellationToken.None);

        Assert.That(ResultId(message), Is.EqualTo(700UL));
    }

    [Test]
    public void Announcement_OtherFailures_AreTheOutboxsToRetry()
    {
        _discord.UpsertMessageAsync(EventsChannel, null, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestException(HttpStatusCode.InternalServerError, "Server Error", new RestError(0, "Oops", null)));

        Assert.That(async () => await Announcer().HandleAsync(Message(EventsOutbox.SeasonUpsert, Announcement), Context, CancellationToken.None),
            Throws.InstanceOf<RestException>());
    }

    // ---- MatchThreadCloseHandler ----

    private async Task<MatchedMatch> MatchWithThread(ulong threadId)
    {
        var users = new[] { User.Seed(userName: "home"), User.Seed(userName: "away") };
        Context.Users.AddRange(users);
        await Context.SaveChangesAsync();
        var match = MatchedMatch.Start([users[0].Id], [users[1].Id], Now);
        match.AttachThread(threadId);
        Context.MatchedMatches.Add(match);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return match;
    }

    [Test]
    public async Task ThreadClose_ThreadAlreadyGone_IsDone_WithoutWaitingToDeleteIt()
    {
        var match = await MatchWithThread(888UL);
        _discord.SendMessageAsync(888UL, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(NotFound());
        var message = Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id));

        var outcome = await new MatchThreadCloseHandler(_discord, _time).HandleAsync(message, Context, CancellationToken.None);

        Assert.That((outcome, message.Result), Is.EqualTo((OutboxOutcome.Done, (string?)null)));
        await _discord.DidNotReceiveWithAnyArgs().DeleteChannelAsync(default, default, default);
    }

    [Test]
    public async Task ThreadClose_ThreadGoneBeforeItsDeletion_IsDone()
    {
        var match = await MatchWithThread(888UL);
        _discord.DeleteChannelAsync(888UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(NotFound());
        var message = Message(MatchOutbox.ThreadClose, new MatchPayload(match.Id), attempts: 2, result: new ThreadCloseResult(true));

        var outcome = await new MatchThreadCloseHandler(_discord, _time).HandleAsync(message, Context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.Received(1).DeleteChannelAsync(888UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.DidNotReceiveWithAnyArgs().SendMessageAsync(default, default!, default, default);
    }

    // ---- RescheduleRequestPostHandler ----

    [Test]
    public async Task RequestPost_FirstTry_PostsToTheReschedulesChannel()
    {
        var posted = DiscordMessage(900);
        _discord.SendMessageAsync(ReschedulesChannel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        var message = Message(RescheduleOutbox.RequestPost, new RescheduleRequestPostPayload(3, "Reschedule request for match 12"));

        await new RescheduleRequestPostHandler(_discord, _options).HandleAsync(message, Context, CancellationToken.None);

        Assert.That(message.Result, Is.EqualTo(JsonSerializer.Serialize(new RescheduleRequestPostResult(900, false), OutboxJson.Options)));
        await _discord.Received(1).SendMessageAsync(ReschedulesChannel, Arg.Is<MessageProperties>(m => m.Content == "Reschedule request for match 12"),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RequestPost_ARetry_KeepsThePostAnEarlierTryMade()
    {
        InChannel(ReschedulesChannel, DiscordMessage(901, "Reschedule request for match 12"));
        var message = Message(RescheduleOutbox.RequestPost, new RescheduleRequestPostPayload(3, "Reschedule request for match 12"), attempts: 2);

        await new RescheduleRequestPostHandler(_discord, _options).HandleAsync(message, Context, CancellationToken.None);

        Assert.That(message.Result, Is.EqualTo(JsonSerializer.Serialize(new RescheduleRequestPostResult(901, true), OutboxJson.Options)));
        await _discord.DidNotReceiveWithAnyArgs().SendMessageAsync(default, default!, default, default);
    }

    [Test]
    public void EachHandler_IsForItsOwnKind()
    {
        var discord = _discord;
        var options = _options;
        IOutboxHandler[] handlers =
        [
            new MatchCreatedPostHandler(discord, options, NullLogger<MatchCreatedPostHandler>.Instance),
            new MatchThreadCreateHandler(discord, options, NullLogger<MatchThreadCreateHandler>.Instance),
            new MatchThreadInviteHandler(discord, options, NullLogger<MatchThreadInviteHandler>.Instance),
            new MatchVotePostHandler(discord, options, NullLogger<MatchVotePostHandler>.Instance),
            new MatchEndedPostHandler(discord, options),
            new MatchCanceledPostHandler(discord, options),
            new MatchThreadCloseHandler(discord, _time),
            new RescheduleRequestPostHandler(discord, options),
            new RescheduleThreadCreateHandler(discord, options),
            new RescheduleThreadInviteHandler(discord, NullLogger<RescheduleThreadInviteHandler>.Instance),
            new RescheduleDecisionPostHandler(discord),
            new SeasonAnnouncementHandler(discord, options),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handlers.Select(h => h.Kind), Is.EqualTo(ExpectedKinds));
            Assert.That(handlers.Where(h => !h.DeadLetterSkipsGroup).Select(h => h.Kind), Is.EquivalentTo(KindsThatLetTheGroupGoOn));
        }
    }

    private static readonly string[] ExpectedKinds =
    [
        MatchOutbox.CreatedPost, MatchOutbox.ThreadCreate, MatchOutbox.ThreadInvite, MatchOutbox.VotePost, MatchOutbox.EndedPost,
        MatchOutbox.CanceledPost, MatchOutbox.ThreadClose, RescheduleOutbox.RequestPost, RescheduleOutbox.ThreadCreate,
        RescheduleOutbox.ThreadInvite, RescheduleOutbox.DecisionPost, EventsOutbox.SeasonUpsert,
    ];

    /// <summary>A dead letter of these doesn't hold back the rest of its group: the thread is closed even if the post never made it, a later
    /// announcement replaces the one that failed.</summary>
    private static readonly string[] KindsThatLetTheGroupGoOn = [MatchOutbox.EndedPost, MatchOutbox.CanceledPost, EventsOutbox.SeasonUpsert];
}
