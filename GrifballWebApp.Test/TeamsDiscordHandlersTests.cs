using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamsDiscordHandlersTests
{
    private const ulong DraftChannel = 777_000_222;
    private GrifballContext _context = null!;
    private FakeRestRequestHandler _handler = null!;
    private NetCord.Rest.RestClient _rest = null!;
    private IDbContextFactory _ = null!;

    // marker to keep the field list readable
    private interface IDbContextFactory { }

    private Microsoft.EntityFrameworkCore.IDbContextFactory<GrifballContext> _factory = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private IOptions<DiscordOptions> _options = null!;
    private Season _season = null!;
    private User _cap = null!;
    private User _cap2 = null!;
    private User _player = null!;
    private User _noName = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _rest = FakeRestRequestHandler.CreateClient(out _handler);
        _factory = GrifballContextTestExtensions.FactoryFor(_context);
        _options = Options.Create(new DiscordOptions { DraftChannel = DraftChannel });

        _season = await _context.AddSeason();
        _cap = await _context.AddUser("cap", "Cap Display");
        _context.UserClaims.Add(new UserClaim { UserId = _cap.Id, ClaimType = ClaimTypes.NameIdentifier, ClaimValue = "111222333" });
        await _context.SaveChangesAsync();
        _cap2 = await _context.AddUser("cap2", "Cap Two");
        _player = await _context.AddUser("player", "Player Display", gamertag: "PlayerGT");
        _noName = await _context.AddUser("noname");
        await _context.AddTeam(_season, "Team", _cap, 1);
        await _context.AddTeam(_season, "Team 2", _cap2, 2);
        // a remaining pool member so the on-deck message has a pick menu
        var poolUser = await _context.AddUser("pool", gamertag: "PoolGT");
        await _context.AddSignup(_season, poolUser);

        var onDeck = new DiscordOnDeckMessages(new TeamService(_context, Substitute.For<IPublisher>()), _rest, _context, _options);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(DiscordOnDeckMessages)).Returns(onDeck);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private List<string?> SentContents()
    {
        var sent = _handler.SentMessages();
        Assert.That(sent.Select(s => s.ChannelId), Is.All.EqualTo(DraftChannel));
        return sent.Select(s => DiscordOnDeckMessagesTests.Content(s.Body)).ToList();
    }

    [Test]
    public async Task AddPlayerDiscordHandler_Should_AnnouncePick_ThenPostOnDeck()
    {
        var sut = new AddPlayerDiscordHandler(_rest, _options, _factory, _scopeFactory);
        await sut.Handle(Notification.Create("c", new AddPlayerToTeamRequestDto { SeasonID = _season.SeasonID, CaptainID = _cap.Id, PersonID = _player.Id }), CancellationToken.None);

        Assert.That(SentContents(), Is.EqualTo(new[]
        {
            "Player Display has been added to <@111222333>'s team",
            "<@111222333> is on deck",
        }));
    }

    [Test]
    public async Task RemovePlayerDiscordHandler_Should_AnnounceReturnToPool_UsingIdFallback()
    {
        var sut = new RemovePlayerDiscordHandler(_rest, _options, _factory, _scopeFactory);
        await sut.Handle(Notification.Create("c", new RemovePlayerFromTeamRequestDto { SeasonID = _season.SeasonID, CaptainID = _cap.Id, PersonID = _noName.Id }), CancellationToken.None);

        Assert.That(SentContents(), Is.EqualTo(new[]
        {
            $"{_noName.Id} has returned to the player pool",
            "<@111222333> is on deck",
        }));
    }

    [Test]
    public async Task MovePlayerDiscordHandler_Should_AnnounceMove()
    {
        var sut = new MovePlayerDiscordHandler(_rest, _options, _factory, _scopeFactory);
        await sut.Handle(Notification.Create("c", new MovePlayerToTeamRequestDto { SeasonID = _season.SeasonID, PreviousCaptainID = _cap.Id, NewCaptainID = _cap2.Id, PersonID = _player.Id, RoundNumber = 1 }), CancellationToken.None);

        Assert.That(SentContents(), Is.EqualTo(new[]
        {
            "Player Display has been moved from <@111222333>'s team to Cap Two's team",
            "<@111222333> is on deck",
        }));
    }

    [Test]
    public async Task LockChangeDiscordHandler_Lock_Should_AnnounceAndPostOnDeck()
    {
        var sut = new LockChangeDiscordHandler(_rest, _options, _factory, _scopeFactory);
        await sut.Handle(new LockChanged(true, _season.SeasonID, null), CancellationToken.None);

        Assert.That(SentContents(), Is.EqualTo(new[] { "Captains are now locked", "<@111222333> is on deck" }));
    }

    [Test]
    public async Task LockChangeDiscordHandler_Unlock_Should_OnlyAnnouncePause()
    {
        var sut = new LockChangeDiscordHandler(_rest, _options, _factory, _scopeFactory);
        await sut.Handle(new LockChanged(false, _season.SeasonID, null), CancellationToken.None);

        Assert.That(SentContents(), Is.EqualTo(new[] { "Captains are now unlocked. Draft is paused until adjustments are complete" }));
    }

    [Test]
    public async Task DiscordHandlers_Should_DoNothing_When_DisabledGlobally()
    {
        var disabled = Options.Create(new DiscordOptions { DraftChannel = DraftChannel, DisableGlobally = true });
        await new LockChangeDiscordHandler(_rest, disabled, _factory, _scopeFactory).Handle(new LockChanged(true, _season.SeasonID, null), CancellationToken.None);
        await new AddPlayerDiscordHandler(_rest, disabled, _factory, _scopeFactory)
            .Handle(Notification.Create("c", new AddPlayerToTeamRequestDto { SeasonID = _season.SeasonID, CaptainID = _cap.Id, PersonID = _player.Id }), CancellationToken.None);

        Assert.That(_handler.Requests, Is.Empty);
        _scopeFactory.DidNotReceive().CreateScope();
    }

    [Test]
    public async Task CaptainDiscordHandlers_Should_CurrentlySendNothing()
    {
        await new AddCaptainDiscordHandler(_rest, _options, _factory)
            .Handle(Notification.Create("c", new CaptainAddedDto { SeasonID = 1, PersonID = 2, TeamName = "T", CaptainName = "C", OrderNumber = 1 }), CancellationToken.None);
        await new RemoveCaptainDiscordHandler(_rest, _options, _factory)
            .Handle(Notification.Create("c", new RemoveCaptainDto { SeasonID = 1, PersonID = 2 }), CancellationToken.None);
        await new ResortCaptainDiscordHandler(_rest, _options, _factory)
            .Handle(Notification.Create("c", new CaptainPlacementDto { SeasonID = 1, PersonID = 2, OrderNumber = 1 }), CancellationToken.None);

        Assert.That(_handler.Requests, Is.Empty);
    }
}
