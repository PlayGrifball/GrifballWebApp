using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.Extensions;
using static GrifballWebApp.Server.Test.DiscordComponentContexts;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The draft through TeamsController (as a commissioner) and the captains' Discord pick menu (DraftStringMenuHandler),
/// over the real TeamService on a temp-file SQLite GrifballContext: captains Alpha and Bravo and player Player1 signed
/// up for the season; the draft channel, the SignalR publisher and the season lock (SQLite needs none) substituted.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamsDraftUnitTests : SqliteGrifballContextFixture
{
    private readonly IDraftChannel _draftChannel = Substitute.For<IDraftChannel>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly RecordingLogger<DraftStringMenuHandler> _logger = new();

    private TeamService Teams() => new(Db, _publisher, _draftChannel, Substitute.For<IDatabaseLocks>());

    private sealed record Draft(Season Season, User Alpha, User Bravo, User Player, TeamsController Controller);

    private async Task<Draft> Signed()
    {
        var season = await AddSeason("Winter");
        var alpha = await AddUser(1, "Alpha");
        var bravo = await AddUser(2, "Bravo");
        var player = await AddUser(3, "Player1");
        var commissioner = await AddUser(4, "Commish", DiscordCaller.Roles.Commissioner);
        Db.SeasonSignups.AddRange(SeasonSignup.Seed(season.SeasonID, alpha.Id), SeasonSignup.Seed(season.SeasonID, bravo.Id), SeasonSignup.Seed(season.SeasonID, player.Id));
        await Db.SaveChangesAsync();
        return new Draft(season, alpha, bravo, player, new TeamsController(Teams()).WithUser(commissioner.Id.ToString(), "Commish", "Commissioner"));
    }

    private static Task<ActionResult<CaptainAddedResponseDto>> AddCaptain(Draft draft, User captain, int order)
        => draft.Controller.AddCaptain(draft.Season.SeasonID, new CaptainPlacementDto { PersonID = captain.Id, OrderNumber = order }, null, CancellationToken.None);

    /// <summary>Each captain's draft order, by user.</summary>
    private async Task<Dictionary<int, int?>> CaptainOrder()
    {
        Db.ChangeTracker.Clear();
        return await Db.Teams.Select(t => new { t.Captain!.UserID, t.Captain.DraftCaptainOrder }).ToDictionaryAsync(x => x.UserID, x => x.DraftCaptainOrder);
    }

    /// <summary>The captain of the team <paramref name="user"/> was picked for (not as its captain), if any.</summary>
    private async Task<int?> PickedBy(User user)
    {
        Db.ChangeTracker.Clear();
        return await Db.Set<TeamPlayer>().Where(tp => tp.UserID == user.Id).Select(tp => (int?)tp.Team.Captain!.UserID).FirstOrDefaultAsync();
    }

    [Test]
    public async Task AddCaptain_MakesTheirTeam_AndAnswersItsNameAndID()
    {
        var draft = await Signed();

        var added = (await AddCaptain(draft, draft.Alpha, 1)).Value!;

        Db.ChangeTracker.Clear();
        var team = await Db.Teams.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((added.TeamID, added.TeamName), Is.EqualTo((team.TeamID, team.TeamName)));
            Assert.That(team.TeamName, Does.Contain("Alpha"));
            Assert.That(await CaptainOrder(), Is.EqualTo(new Dictionary<int, int?> { [draft.Alpha.Id] = 1 }));
        }
    }

    [Test]
    public async Task ResortCaptain_MovesTheCaptainInTheOrder()
    {
        var draft = await Signed();
        await AddCaptain(draft, draft.Alpha, 1);
        await AddCaptain(draft, draft.Bravo, 2);

        var result = await draft.Controller.ResortCaptain(draft.Season.SeasonID, new CaptainPlacementDto { PersonID = draft.Bravo.Id, OrderNumber = 1 }, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(await CaptainOrder(), Is.EqualTo(new Dictionary<int, int?> { [draft.Bravo.Id] = 1, [draft.Alpha.Id] = 2 }));
        }
    }

    [Test]
    public async Task RemoveCaptain_RemovesTheirTeam_AndTheOthersMoveUp()
    {
        var draft = await Signed();
        await AddCaptain(draft, draft.Alpha, 1);
        await AddCaptain(draft, draft.Bravo, 2);

        var result = await draft.Controller.RemoveCaptain(draft.Season.SeasonID, new RemoveCaptainDto { PersonID = draft.Alpha.Id }, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(await CaptainOrder(), Is.EqualTo(new Dictionary<int, int?> { [draft.Bravo.Id] = 1 }));
        }
    }

    [Test]
    public async Task AddMoveAndRemoveAPlayer_AsTheCommissioner()
    {
        var draft = await Signed();
        await AddCaptain(draft, draft.Alpha, 1);
        await AddCaptain(draft, draft.Bravo, 2);
        var season = draft.Season.SeasonID;

        var added = await draft.Controller.AddPlayerToTeam(season, new AddPlayerToTeamRequestDto { CaptainID = draft.Alpha.Id, PersonID = draft.Player.Id }, null, CancellationToken.None);
        var onAlphas = await PickedBy(draft.Player);
        var moved = await draft.Controller.MovePlayerToTeam(season,
            new MovePlayerToTeamRequestDto { PreviousCaptainID = draft.Alpha.Id, NewCaptainID = draft.Bravo.Id, PersonID = draft.Player.Id, RoundNumber = 1 }, null, CancellationToken.None);
        var onBravos = await PickedBy(draft.Player);
        var removed = await draft.Controller.RemovePlayerFromTeam(season, new RemovePlayerFromTeamRequestDto { CaptainID = draft.Bravo.Id, PersonID = draft.Player.Id }, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new[] { added, moved, removed }, Is.All.InstanceOf<NoContentResult>());
            Assert.That(onAlphas, Is.EqualTo(draft.Alpha.Id));
            Assert.That(onBravos, Is.EqualTo(draft.Bravo.Id));
            Assert.That(await PickedBy(draft.Player), Is.Null);
        }
        await _draftChannel.Received(1).PlayerAddedAsync(Arg.Any<TeamService>(), Arg.Is<AddPlayerToTeamRequestDto>(x => x.SeasonID == season && x.PersonID == draft.Player.Id), Arg.Any<CancellationToken>());
        await _draftChannel.Received(1).PlayerMovedAsync(Arg.Any<TeamService>(), Arg.Is<MovePlayerToTeamRequestDto>(x => x.SeasonID == season), Arg.Any<CancellationToken>());
        await _draftChannel.Received(1).PlayerRemovedAsync(Arg.Any<TeamService>(), Arg.Is<RemovePlayerFromTeamRequestDto>(x => x.SeasonID == season), Arg.Any<CancellationToken>());
    }

    /// <summary>What the pick menu's deferred reply was edited to.</summary>
    private static string? Edited(IDiscordStringMenuInteractionContext context)
    {
        var edit = context.Interaction.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "ModifyResponseAsync");
        var options = Substitute.For<IDiscordMessageOptions>();
        options.ReturnsForAll<IDiscordMessageOptions>(options);
        ((Action<IDiscordMessageOptions>)edit.GetArguments()[0]!)(options);
        return (string?)options.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordMessageOptions.WithContent)).GetArguments()[0];
    }

    /// <summary>Alpha captains the only team, and the draft has started (the captains locked): it is Alpha's pick.</summary>
    private async Task<Draft> AlphasPick()
    {
        var draft = await Signed();
        await AddCaptain(draft, draft.Alpha, 1);
        await draft.Controller.LockCaptains(draft.Season.SeasonID, null, CancellationToken.None);
        return draft;
    }

    [Test]
    public async Task DraftPick_TheCaptainsPick_IsMade()
    {
        var draft = await AlphasPick();
        var context = StringMenuContext(1, draft.Player.Id.ToString());

        await new DraftStringMenuHandler(Db, Teams(), _logger).DraftPick(context, draft.Season.SeasonID, draft.Alpha.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Edited(context), Is.EqualTo("<@1>, your pick was made successfully"));
            Assert.That(await PickedBy(draft.Player), Is.EqualTo(draft.Alpha.Id));
        }
    }

    [Test]
    public async Task DraftPick_APlayerAlreadyPicked_IsRefusedWithTheServicesReason()
    {
        var draft = await AlphasPick();
        await new DraftStringMenuHandler(Db, Teams(), _logger).DraftPick(StringMenuContext(1, draft.Player.Id.ToString()), draft.Season.SeasonID, draft.Alpha.Id);
        var again = StringMenuContext(1, draft.Player.Id.ToString());

        await new DraftStringMenuHandler(Db, Teams(), _logger).DraftPick(again, draft.Season.SeasonID, draft.Alpha.Id);

        Assert.That(Edited(again), Is.EqualTo("Hey <@1>, This player is already on a team"));
    }

    [Test]
    public async Task DraftPick_AnUnexpectedFailure_IsLogged_AndTheCaptainToldToTryAgain()
    {
        var draft = await AlphasPick();
        _draftChannel.PlayerAddedAsync(Arg.Any<TeamService>(), Arg.Any<AddPlayerToTeamRequestDto>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("outbox down"));
        var context = StringMenuContext(1, draft.Player.Id.ToString());

        await new DraftStringMenuHandler(Db, Teams(), _logger).DraftPick(context, draft.Season.SeasonID, draft.Alpha.Id);

        var entry = _logger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Edited(context), Is.EqualTo("Hey <@1>, something bad happened. Please try again and contact sysadmin if problem persists"));
            Assert.That((entry.Level, entry.Exception?.Message), Is.EqualTo((LogLevel.Error, (string?)"outbox down")));
            Assert.That(entry.Message, Is.EqualTo($"DraftPick failed for Discord user 1, season {draft.Season.SeasonID}, captain {draft.Alpha.Id}"));
            Assert.That(await PickedBy(draft.Player), Is.Null, "Rolled back");
        }
    }

    [Test]
    public async Task TeamsHub_AddPlayerToTeam_PicksForTheCaptain_AsTheSignedInCommissioner()
    {
        var draft = await Signed();
        await AddCaptain(draft, draft.Alpha, 1);
        var commissioner = await Db.Users.Where(u => u.UserName == "Commish").Select(u => u.Id).SingleAsync();
        var hub = new TeamsHub(Teams());
        var caller = Substitute.For<Microsoft.AspNetCore.SignalR.HubCallerContext>();
        caller.User.Returns(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, commissioner.ToString())], "Test")));
        caller.ConnectionId.Returns("connection-1");
        hub.Context = caller;

        await hub.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = draft.Season.SeasonID, CaptainID = draft.Alpha.Id, PersonID = draft.Player.Id });

        Assert.That(await PickedBy(draft.Player), Is.EqualTo(draft.Alpha.Id));
        await _publisher.Received(1).Publish(Arg.Is<Notification<AddPlayerToTeamRequestDto>>(n => n.ConnectionId == "connection-1" && n.Value.PersonID == draft.Player.Id), Arg.Any<CancellationToken>());
    }
}
