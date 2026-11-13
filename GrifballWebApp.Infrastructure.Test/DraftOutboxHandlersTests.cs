using System.Text.Json;
using DiscordInterface.Generated;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCord.Rest;
using NSubstitute;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The draft channel's messages: what TeamService's changes queue (DraftOutbox's Enqueue helpers, read back from a
/// real temp-file SQLite GrifballContext's outbox), and how the handlers send them to a substituted Discord client: a
/// post once (a retry finds the one an earlier try sent), and the on-deck messages one at a time, carrying on from
/// where a retry left off.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DraftOutboxHandlersTests : SqliteContextFixture
{
    private const ulong DraftChannel = 333UL;
    private const ulong BotId = 1UL;
    private static readonly DateTime QueuedAt = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
    private static readonly string[] PostThenOnDeck = [DraftOutbox.Post, DraftOutbox.OnDeck];
    private static readonly string[] PostOnly = [DraftOutbox.Post];

    private readonly IDiscordRestClient _discord = Substitute.For<IDiscordRestClient>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 9, 0, 30, TimeSpan.Zero));
    private TeamService _teams = null!;
    private IOutbox _outbox = null!;

    [SetUp]
    public void SetUp()
    {
        _teams = new TeamService((IApplicationDbContext)Context, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), Substitute.For<IDatabaseLocks>());
        _outbox = new ContextOutbox(Context);
        var me = Substitute.For<IDiscordCurrentUser>();
        me.Id.Returns(BotId);
        _discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
    }

    private IOptions<DiscordOptions> Options() => Microsoft.Extensions.Options.Options.Create(new DiscordOptions { DraftChannel = DraftChannel });

    private async Task<Season> AddSeason()
    {
        var season = Season.Seed(seasonName: "Draft Season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season;
    }

    private async Task<User> AddUser(string name, long? discordId = null)
    {
        var user = User.Seed(userName: name, displayName: name, discordUser: discordId is { } id ? DiscordUser.FromDiscord(id, name) : null);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    /// <summary>What was queued, in order: the kinds, and each post's content.</summary>
    private async Task<(List<string> Kinds, List<string> Posts)> QueuedAsync()
    {
        await Context.SaveChangesAsync();
        var queued = await Context.Outbox.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        return (queued.Select(m => m.Kind).ToList(),
            queued.Where(m => m.Kind == DraftOutbox.Post).Select(m => JsonSerializer.Deserialize<DraftPostPayload>(m.Payload, OutboxJson.Options)!.Content).ToList());
    }

    [Test]
    public async Task PlayerAdded_NamesThePlayerAndTheCaptain_ThenWhoIsOnDeck()
    {
        var season = await AddSeason();
        var captain = await AddUser("Cap", discordId: 4242);
        var player = await AddUser("Pick");

        await _outbox.EnqueuePlayerAddedAsync(Context, _teams, new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = captain.Id, PersonID = player.Id }, CancellationToken.None);

        var (kinds, posts) = await QueuedAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(kinds, Is.EqualTo(PostThenOnDeck));
            Assert.That(posts, Has.Count.EqualTo(1).And.Contains("Pick has been added to <@4242>'s team"), "a captain with Discord is mentioned");
        }
    }

    [Test]
    public async Task PlayerRemoved_SaysTheyReturnedToThePool()
    {
        var season = await AddSeason();
        var player = await AddUser("Pick");

        await _outbox.EnqueuePlayerRemovedAsync(Context, _teams, new RemovePlayerFromTeamRequestDto { SeasonID = season.SeasonID, PersonID = player.Id }, CancellationToken.None);

        var (kinds, posts) = await QueuedAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(kinds, Is.EqualTo(PostThenOnDeck));
            Assert.That(posts, Has.Count.EqualTo(1).And.Contains("Pick has returned to the player pool"));
        }
    }

    [Test]
    public async Task PlayerMoved_NamesBothCaptains()
    {
        var season = await AddSeason();
        var from = await AddUser("From");
        var to = await AddUser("To");
        var player = await AddUser("Pick");

        await _outbox.EnqueuePlayerMovedAsync(Context, _teams,
            new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = from.Id, NewCaptainID = to.Id, PersonID = player.Id }, CancellationToken.None);

        var (kinds, posts) = await QueuedAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(kinds, Is.EqualTo(PostThenOnDeck));
            Assert.That(posts, Has.Count.EqualTo(1).And.Contains("Pick has been moved from From's team to To's team"));
        }
    }

    [Test]
    public async Task CaptainsLocked_StartsTheDraft_WithWhoIsOnDeck()
    {
        var season = await AddSeason();

        await _outbox.EnqueueLockChangedAsync(Context, _teams, season.SeasonID, locked: true, CancellationToken.None);

        var (kinds, posts) = await QueuedAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(kinds, Is.EqualTo(PostThenOnDeck));
            Assert.That(posts, Has.Count.EqualTo(1).And.Contains("Captains are now locked"));
        }
    }

    [Test]
    public async Task CaptainsUnlocked_PausesTheDraft_WithNoOnDeck()
    {
        var season = await AddSeason();

        await _outbox.EnqueueLockChangedAsync(Context, _teams, season.SeasonID, locked: false, CancellationToken.None);

        var (kinds, posts) = await QueuedAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(kinds, Is.EqualTo(PostOnly));
            Assert.That(posts, Has.Count.EqualTo(1).And.Contains("Captains are now unlocked. Draft is paused until adjustments are complete"));
        }
    }

    // ---- handlers ----

    private static OutboxMessage Message<T>(string kind, T payload, int attempts = 1, object? result = null) => new()
    {
        Kind = kind,
        Payload = JsonSerializer.Serialize(payload, OutboxJson.Options),
        Attempts = attempts,
        CreatedAt = QueuedAt,
        Result = result is null ? null : JsonSerializer.Serialize(result, OutboxJson.Options),
    };

    /// <summary>The bot's earlier post in the draft channel, with this content, at this time.</summary>
    private void BotPosted(string content, DateTimeOffset at)
    {
        var message = Substitute.For<IDiscordRestMessage>();
        message.Id.Returns(55UL);
        message.Author.Id.Returns(BotId);
        message.Content.Returns(content);
        message.CreatedAt.Returns(at);
        _discord.GetMessagesAsync(DraftChannel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => new[] { message }.ToAsyncEnumerable());
    }

    [Test]
    public async Task PostHandler_FirstTry_SendsTheContentToTheDraftChannel()
    {
        var handler = new DraftPostHandler(_discord, Options());

        var outcome = await handler.HandleAsync(Message(DraftOutbox.Post, new DraftPostPayload(1, "Captains are now locked")), Context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.Received(1).SendMessageAsync(DraftChannel, Arg.Is<MessageProperties>(m => m.Content == "Captains are now locked"),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.DidNotReceiveWithAnyArgs().GetCurrentUserAsync(default, default);
    }

    [Test]
    public async Task PostHandler_ARetry_WhoseEarlierTrySent_SendsNothingMore()
    {
        BotPosted("Captains are now locked", new DateTimeOffset(QueuedAt).AddSeconds(2));
        var handler = new DraftPostHandler(_discord, Options());

        var outcome = await handler.HandleAsync(Message(DraftOutbox.Post, new DraftPostPayload(1, "Captains are now locked"), attempts: 2), Context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.DidNotReceiveWithAnyArgs().SendMessageAsync(default, default!, default, default);
    }

    [Test]
    public async Task PostHandler_ARetry_FindingTheSameTextFromBeforeItWasQueued_SendsItAgain()
    {
        // The same words, posted for an earlier lock: not this message's
        BotPosted("Captains are now locked", new DateTimeOffset(QueuedAt).AddMinutes(-10));
        var handler = new DraftPostHandler(_discord, Options());

        await handler.HandleAsync(Message(DraftOutbox.Post, new DraftPostPayload(1, "Captains are now locked"), attempts: 2), Context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(DraftChannel, Arg.Is<MessageProperties>(m => m.Content == "Captains are now locked"),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    private static DraftOnDeckPayload TwoMessages() => new(1,
    [
        new DraftMessage("<@4242> is on deck", [new DraftMenu("draft-pick:1:2:0", "Make pick - Abe - Zed", [new DraftMenuOption("Abe", "10"), new DraftMenuOption("Zed", "11")])]),
        new DraftMessage(null, [new DraftMenu("draft-pick:1:2:5", "Make pick - More - More", [new DraftMenuOption("More", "12")])]),
    ]);

    [Test]
    public async Task OnDeck_SendsTheFirstMessage_AndComesBackForTheNext()
    {
        var handler = new DiscordOnDeckMessages(_discord, Options(), _time);
        var message = Message(DraftOutbox.OnDeck, TwoMessages());

        var outcome = await handler.HandleAsync(message, Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.ContinueLater(_time.GetUtcNow().UtcDateTime)));
            Assert.That(message.Result, Is.EqualTo(JsonSerializer.Serialize(new DraftOnDeckResult(1), OutboxJson.Options)), "how many went, for the next try");
        }
        await _discord.Received(1).SendMessageAsync(DraftChannel, Arg.Is<MessageProperties>(m => m.Content == "<@4242> is on deck"),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OnDeck_CarriesOnFromTheFirstNotSent_AndIsDoneAfterTheLast()
    {
        var handler = new DiscordOnDeckMessages(_discord, Options(), _time);
        var message = Message(DraftOutbox.OnDeck, TwoMessages(), result: new DraftOnDeckResult(1));

        var outcome = await handler.HandleAsync(message, Context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.Received(1).SendMessageAsync(DraftChannel, Arg.Is<MessageProperties>(m => m.Content == null && m.Components != null),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OnDeck_EverythingSentAlready_SendsNothing()
    {
        var handler = new DiscordOnDeckMessages(_discord, Options(), _time);

        var outcome = await handler.HandleAsync(Message(DraftOutbox.OnDeck, TwoMessages(), result: new DraftOnDeckResult(2)), Context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.DidNotReceiveWithAnyArgs().SendMessageAsync(default, default!, default, default);
    }

    [Test]
    public void Render_TheMenusAsPickMenus_WithTheirOptions()
    {
        var rendered = DiscordOnDeckMessages.Render(TwoMessages().Messages[0]);

        var menu = rendered.Components!.OfType<StringMenuProperties>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rendered.Content, Is.EqualTo("<@4242> is on deck"));
            Assert.That((menu.CustomId, menu.Placeholder), Is.EqualTo(("draft-pick:1:2:0", (string?)"Make pick - Abe - Zed")));
            Assert.That(menu.Options.Select(o => (o.Label, o.Value)), Is.EqualTo(new[] { ("Abe", "10"), ("Zed", "11") }));
        }
    }

    [Test]
    public void Render_NoMenus_IsTheContentAlone()
    {
        var rendered = DiscordOnDeckMessages.Render(new DraftMessage("Draft has been completed", []));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rendered.Content, Is.EqualTo("Draft has been completed"));
            Assert.That(rendered.Components, Is.Null);
        }
    }

    [Test]
    public void Handlers_AreTheDraftKinds_AndADeadLetterLetsTheRestOfTheGroupGo()
    {
        var post = new DraftPostHandler(_discord, Options());
        var onDeck = new DiscordOnDeckMessages(_discord, Options(), _time);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((post.Kind, post.DeadLetterSkipsGroup), Is.EqualTo((DraftOutbox.Post, false)));
            Assert.That((onDeck.Kind, onDeck.DeadLetterSkipsGroup), Is.EqualTo((DraftOutbox.OnDeck, false)));
        }
    }
}
