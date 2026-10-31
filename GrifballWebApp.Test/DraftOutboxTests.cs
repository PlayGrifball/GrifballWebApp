using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Teams;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>The draft channel's messages, queued with the draft change (<see cref="DraftOutbox"/>) and sent by the outbox.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DraftOutboxTests
{
    private const ulong DraftChannel = 777_000_222;
    private GrifballContext _context = null!;
    private FakeRestRequestHandler _handler = null!;
    private OutboxTestDriver _outbox = null!;
    private TeamService _teams = null!;
    private Season _season = null!;
    private User _cap = null!;
    private User _cap2 = null!;
    private User _player = null!;
    private User _noName = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var rest = FakeRestRequestHandler.CreateClient(out _handler);
        _outbox = new OutboxTestDriver(_context, new DiscordRestClient(rest), new DiscordOptions { DraftChannel = DraftChannel });
        _teams = new TeamService(_context, Substitute.For<IPublisher>(), new ContextOutbox(_context));

        _season = await _context.AddSeason();
        _cap = await _context.AddUser("cap", "Cap Display");
        // Linked to Discord the way the bot and the profile page link accounts: a DiscordUserID, no claim.
        _cap.DiscordUser = new GrifballWebApp.Database.Models.DiscordUser { DiscordUserID = 111222333, DiscordUsername = "capdisc" };
        await _context.SaveChangesAsync();
        _cap2 = await _context.AddUser("cap2", "Cap Two");
        _player = await _context.AddUser("player", "Player Display", gamertag: "PlayerGT");
        _noName = await _context.AddUser("noname");
        await _context.AddTeam(_season, "Team", _cap, 1);
        await _context.AddTeam(_season, "Team 2", _cap2, 2);
        // a remaining pool member so the on-deck message has a pick menu
        var poolUser = await _context.AddUser("pool", gamertag: "PoolGT");
        await _context.AddSignup(_season, poolUser);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _outbox.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

    private async Task Send(Func<IOutbox, Task> enqueue)
    {
        await enqueue(new ContextOutbox(_context));
        await _context.SaveChangesAsync();
        await _outbox.DrainAsync();
    }

    private List<string?> SentContents()
    {
        var sent = _handler.SentMessages();
        Assert.That(sent.Select(s => s.ChannelId), Is.All.EqualTo(DraftChannel));
        return sent.Select(s => DiscordOnDeckMessagesTests.Content(s.Body)).ToList();
    }

    [Test]
    public async Task PlayerAdded_AnnouncesThePick_ThenWhoIsOnDeck()
    {
        await Send(o => o.EnqueuePlayerAddedAsync(_context, _teams, new AddPlayerToTeamRequestDto { SeasonID = _season.SeasonID, CaptainID = _cap.Id, PersonID = _player.Id }, CancellationToken.None));

        Assert.That(SentContents(), Is.EqualTo(new[]
        {
            "Player Display has been added to <@111222333>'s team",
            "<@111222333> is on deck",
        }));
        var messages = await _outbox.MessagesAsync();
        Assert.That(messages.Select(x => (x.Kind, x.GroupKey, x.Status)), Is.EqualTo(new[]
        {
            (DraftOutbox.Post, DraftOutbox.Group(_season.SeasonID), OutboxStatus.Sent),
            (DraftOutbox.OnDeck, DraftOutbox.Group(_season.SeasonID), OutboxStatus.Sent),
        }));
    }

    [Test]
    public async Task PlayerRemoved_AnnouncesTheReturnToThePool_UsingTheIdFallback()
    {
        await Send(o => o.EnqueuePlayerRemovedAsync(_context, _teams, new RemovePlayerFromTeamRequestDto { SeasonID = _season.SeasonID, CaptainID = _cap.Id, PersonID = _noName.Id }, CancellationToken.None));

        Assert.That(SentContents(), Is.EqualTo(new[]
        {
            $"{_noName.Id} has returned to the player pool",
            "<@111222333> is on deck",
        }));
    }

    [Test]
    public async Task PlayerMoved_AnnouncesTheMove()
    {
        await Send(o => o.EnqueuePlayerMovedAsync(_context, _teams, new MovePlayerToTeamRequestDto { SeasonID = _season.SeasonID, PreviousCaptainID = _cap.Id, NewCaptainID = _cap2.Id, PersonID = _player.Id, RoundNumber = 1 }, CancellationToken.None));

        Assert.That(SentContents(), Is.EqualTo(new[]
        {
            "Player Display has been moved from <@111222333>'s team to Cap Two's team",
            "<@111222333> is on deck",
        }));
    }

    [Test]
    public async Task Lock_AnnouncesAndPostsWhoIsOnDeck_Unlock_OnlyThePause()
    {
        await Send(o => o.EnqueueLockChangedAsync(_context, _teams, _season.SeasonID, true, CancellationToken.None));
        await Send(o => o.EnqueueLockChangedAsync(_context, _teams, _season.SeasonID, false, CancellationToken.None));

        Assert.That(SentContents(), Is.EqualTo(new[]
        {
            "Captains are now locked",
            "<@111222333> is on deck",
            "Captains are now unlocked. Draft is paused until adjustments are complete",
        }));
    }

    [Test]
    public async Task LockCaptains_QueuesItsMessagesWithTheChange()
    {
        await _teams.LockCaptains(_season.SeasonID, true);

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That((await fresh.Seasons.SingleAsync()).CaptainsLocked, Is.True);
            Assert.That((await fresh.Outbox.OrderBy(x => x.Id).ToListAsync()).Select(x => x.Kind), Is.EqualTo(new[] { DraftOutbox.Post, DraftOutbox.OnDeck }));
        });
    }

    [Test]
    public async Task PreviewWithoutDiscord_SendsNothing()
    {
        await using var preview = new OutboxTestDriver(_context, Substitute.For<IDiscordRestClient>(), new DiscordOptions { DraftChannel = DraftChannel, DisableGlobally = true });
        await new ContextOutbox(_context).EnqueueLockChangedAsync(_context, _teams, _season.SeasonID, true, CancellationToken.None);
        await _context.SaveChangesAsync();

        await preview.DrainAsync();

        Assert.That((await preview.MessagesAsync()).Select(x => x.Status), Is.All.EqualTo(OutboxStatus.Skipped));
    }

    [Test]
    public async Task OnDeck_Retry_CarriesOnFromTheFirstMessageNotSent()
    {
        var discord = Substitute.For<IDiscordRestClient>();
        var sent = new List<string?>();
        var calls = 0;
        discord.SendMessageAsync(DraftChannel, Arg.Any<NetCord.Rest.MessageProperties>(), Arg.Any<NetCord.Rest.RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                // The second message fails once, before it is sent.
                if (++calls == 2)
                    return Task.FromException<IDiscordRestMessage>(new HttpRequestException("503"));
                sent.Add(c.ArgAt<NetCord.Rest.MessageProperties>(1).Content);
                return Task.FromResult(Substitute.For<IDiscordRestMessage>());
            });
        await using var outbox = new OutboxTestDriver(_context, discord, new DiscordOptions { DraftChannel = DraftChannel });
        var payload = new DraftOnDeckPayload(_season.SeasonID,
        [
            new DraftMessage("first", []),
            new DraftMessage(null, [new DraftMenu("menu", "Make pick", [new DraftMenuOption("a", "1")])]),
            new DraftMessage("third", []),
        ]);
        await new ContextOutbox(_context).EnqueueAsync(DraftOutbox.OnDeck, payload, DraftOutbox.Group(_season.SeasonID));
        await _context.SaveChangesAsync();

        await outbox.DrainAsync();

        var message = (await outbox.MessagesAsync()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.EqualTo(new[] { "first", null, "third" }), "Each sent once, in order");
            Assert.That(message.Status, Is.EqualTo(OutboxStatus.Sent));
            Assert.That(message.Result, Is.EqualTo("""{"posted":3}"""));
        });
    }

    [Test]
    public async Task Post_AlreadySentByAFailedTry_IsNotSentAgain_ButAnOlderOneWithTheSameText_DoesNotCount()
    {
        var discord = Substitute.For<IDiscordRestClient>();
        discord.GetCurrentUserAsync(Arg.Any<NetCord.Rest.RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<IDiscordCurrentUser>());
        var older = Substitute.For<IDiscordRestMessage>();
        older.Content.Returns("Captains are now locked");
        older.CreatedAt.Returns(DateTimeOffset.UtcNow.AddHours(-1));
        var posted = new List<IDiscordRestMessage> { older };
        discord.GetMessagesAsync(DraftChannel, Arg.Any<NetCord.Rest.PaginationProperties<ulong>>(), Arg.Any<NetCord.Rest.RestRequestProperties>())
            .Returns(_ => posted.ToArray().ToAsyncEnumerable());
        var calls = 0;
        discord.SendMessageAsync(DraftChannel, Arg.Any<NetCord.Rest.MessageProperties>(), Arg.Any<NetCord.Rest.RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                calls++;
                var m = Substitute.For<IDiscordRestMessage>();
                m.Content.Returns(c.ArgAt<NetCord.Rest.MessageProperties>(1).Content);
                m.CreatedAt.Returns(DateTimeOffset.UtcNow);
                posted.Insert(0, m);
                // The first send goes out, but its answer is lost.
                return calls == 1 ? Task.FromException<IDiscordRestMessage>(new TaskCanceledException("timed out")) : Task.FromResult(m);
            });
        await using var outbox = new OutboxTestDriver(_context, discord, new DiscordOptions { DraftChannel = DraftChannel });
        await new ContextOutbox(_context).EnqueuePostAsync(_season.SeasonID, "Captains are now locked", CancellationToken.None);
        await _context.SaveChangesAsync();

        await outbox.DrainAsync();

        Assert.That(calls, Is.EqualTo(1));
        Assert.That((await outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Sent));
    }

    [Test]
    public async Task Post_GivenUpOn_DoesNotHoldBackTheOnDeckMessages_AndOnesAllSent_AreDone()
    {
        var discord = Substitute.For<IDiscordRestClient>();
        var sent = new List<string?>();
        discord.SendMessageAsync(DraftChannel, Arg.Any<NetCord.Rest.MessageProperties>(), Arg.Any<NetCord.Rest.RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                var content = c.ArgAt<NetCord.Rest.MessageProperties>(1).Content;
                if (content == "Captains are now locked")
                    return Task.FromException<IDiscordRestMessage>(new HttpRequestException("403"));
                sent.Add(content);
                return Task.FromResult(Substitute.For<IDiscordRestMessage>());
            });
        discord.GetCurrentUserAsync(Arg.Any<NetCord.Rest.RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<IDiscordCurrentUser>());
        discord.GetMessagesAsync(DraftChannel, Arg.Any<NetCord.Rest.PaginationProperties<ulong>>(), Arg.Any<NetCord.Rest.RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        await using var outbox = new OutboxTestDriver(_context, discord, new DiscordOptions { DraftChannel = DraftChannel });
        await new ContextOutbox(_context).EnqueueLockChangedAsync(_context, _teams, _season.SeasonID, true, CancellationToken.None);
        _context.Outbox.Add(new OutboxMessage
        {
            Kind = DraftOutbox.OnDeck,
            Payload = """{"seasonId":1,"messages":[{"content":"x","menus":[]}]}""",
            Result = """{"posted":1}""",
        });
        await _context.SaveChangesAsync();

        await outbox.DrainAsync();

        var messages = await outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(messages.Select(x => x.Status), Is.EqualTo(new[] { OutboxStatus.DeadLettered, OutboxStatus.Sent, OutboxStatus.Sent }));
            Assert.That(sent, Is.EqualTo(new[] { "<@111222333> is on deck" }), "The on-deck message still went; the one already sent did not go again");
        });
    }
}
