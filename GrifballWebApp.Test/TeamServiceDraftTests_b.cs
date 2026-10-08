using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using GrifballWebApp.Test.CovB;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamServiceDraftTests_b
{
    private GrifballContext _context = null!;
    private IPublisher _publisher = null!;
    private TeamService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _publisher = Substitute.For<IPublisher>();
        _service = new TeamService(_context, _publisher);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private List<T> Published<T>()
    {
        return _publisher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPublisher.Publish))
            .Select(c => c.GetArguments()[0])
            .OfType<T>()
            .ToList();
    }

    private async Task<List<TeamPlayer>> TeamPlayersOf(int teamId)
    {
        await using var ctx = _context.NewContextLike();
        return await ctx.TeamPlayers.AsNoTracking().Where(tp => tp.TeamID == teamId).OrderBy(tp => tp.DraftRound).ToListAsync();
    }

    private async Task<List<(int UserID, int? Order)>> CaptainOrders(int seasonId)
    {
        await using var ctx = _context.NewContextLike();
        var list = await ctx.TeamPlayers.AsNoTracking()
            .Where(tp => tp.Team.SeasonID == seasonId && tp.CaptainTeam != null)
            .OrderBy(tp => tp.DraftCaptainOrder)
            .Select(tp => new { tp.UserID, tp.DraftCaptainOrder })
            .ToListAsync();
        return list.Select(x => (x.UserID, x.DraftCaptainOrder)).ToList();
    }

    // ---------------- GetTeams ----------------

    [Test]
    public async Task GetTeams_Should_OrderByCaptainOrder_ExcludeCaptainFromPlayers_And_FallBackOnNames()
    {
        var season = await _context.AddSeason();
        var otherSeason = await _context.AddSeason("Other");
        var capA = await _context.AddUser("capA", "Cap A Display", gamertag: "CapA GT");
        var capB = await _context.AddUser("capB", "Cap B Display", discordName: "capb_discord");
        var gt = await _context.AddUser("gtUser", "GT Display", gamertag: "Gamer", discordName: "gamer_discord");
        var disc = await _context.AddUser("discUser", "Disc Display", discordName: "only_discord");
        var disp = await _context.AddUser("dispUser", "Only Display");
        var uname = await _context.AddUser("onlyUserName");
        var capOther = await _context.AddUser("capOther");

        // Team B is created first but has captain order 1
        var teamB = await _context.AddTeam(season, "Team B", capB, 1, uname, disp);
        var teamA = await _context.AddTeam(season, "Team A", capA, 2, gt, disc);
        await _context.AddTeam(otherSeason, "Other Team", capOther, 1);

        var result = await _service.GetTeams(season.SeasonID);

        Assert.That(result.Select(t => t.TeamName), Is.EqualTo(new[] { "Team B", "Team A" }));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].TeamID, Is.EqualTo(teamB.TeamID));
            Assert.That(result[0].Captain.PersonID, Is.EqualTo(capB.Id));
            Assert.That(result[0].Captain.Name, Is.EqualTo("capb_discord"));
            Assert.That(result[0].Captain.Order, Is.EqualTo(1));
            Assert.That(result[0].Players.Select(p => p.Name), Is.EqualTo(new[] { "onlyUserName", "Only Display" }));
            Assert.That(result[0].Players.Select(p => p.Round), Is.EqualTo(new int?[] { 1, 2 }));

            Assert.That(result[1].TeamID, Is.EqualTo(teamA.TeamID));
            Assert.That(result[1].Captain.Name, Is.EqualTo("CapA GT"));
            Assert.That(result[1].Captain.Order, Is.EqualTo(2));
            Assert.That(result[1].Players.Select(p => p.Name), Is.EqualTo(new[] { "Gamer", "only_discord" }));
            Assert.That(result[1].Players.Select(p => p.PersonID), Is.EqualTo(new[] { gt.Id, disc.Id }));
            Assert.That(result[1].Players.Any(p => p.PersonID == capA.Id), Is.False, "captain must not be listed as a player");
        });
    }

    // ---------------- AddCaptain ----------------

    [Test]
    public async Task AddCaptain_Should_Throw_When_PlayerNotSignedUp()
    {
        var season = await _context.AddSeason();
        var user = await _context.AddUser("u1", "U1");

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = user.Id, OrderNumber = 1 }, resortOnly: false));

        Assert.That(ex!.Message, Is.EqualTo("Player is not signed up"));
        Assert.That(await _context.Teams.CountAsync(), Is.Zero);
        Assert.That(_publisher.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task AddCaptain_Should_Throw_When_PlayerAlreadyCaptain()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        await _context.AddSignup(season, cap);
        await _context.AddTeam(season, "Cap Team", cap, 1);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = cap.Id, OrderNumber = 1 }, resortOnly: false));

        Assert.That(ex!.Message, Is.EqualTo("Player is already a captain"));
    }

    [Test]
    public async Task AddCaptain_Should_Throw_When_PlayerAlreadyOnATeam()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        var player = await _context.AddUser("player", "Player");
        await _context.AddSignup(season, player);
        await _context.AddTeam(season, "Cap Team", cap, 1, player);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = player.Id, OrderNumber = 2 }, resortOnly: false));

        Assert.That(ex!.Message, Is.EqualTo("Player is already on a team"));
        Assert.That(await _context.Teams.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task AddCaptain_Should_UseSignupTeamName_InsertAtOrder_ResequenceOthers_And_Publish()
    {
        var season = await _context.AddSeason();
        var cap1 = await _context.AddUser("cap1", "Cap 1");
        var cap2 = await _context.AddUser("cap2", "Cap 2");
        var newCap = await _context.AddUser("newcap", "New Captain");
        await _context.AddTeam(season, "Team 1", cap1, 1);
        await _context.AddTeam(season, "Team 2", cap2, 2);
        await _context.AddSignup(season, newCap, teamName: "The Hammers");

        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = newCap.Id, OrderNumber = 2 }, resortOnly: false, "conn-1");

        Assert.That(await CaptainOrders(season.SeasonID), Is.EqualTo(new[] { (cap1.Id, (int?)1), (newCap.Id, (int?)2), (cap2.Id, (int?)3) }));

        await using var ctx = _context.NewContextLike();
        var team = await ctx.Teams.Include(t => t.Captain).SingleAsync(t => t.Captain.UserID == newCap.Id);
        Assert.That(team.TeamName, Is.EqualTo("The Hammers"));
        Assert.That(team.SeasonID, Is.EqualTo(season.SeasonID));

        var notes = Published<Notification<CaptainAddedDto>>();
        Assert.That(notes, Has.Count.EqualTo(1));
        Assert.That(notes[0].ConnectionId, Is.EqualTo("conn-1"));
        Assert.That(notes[0].Value, Is.EqualTo(new CaptainAddedDto
        {
            SeasonID = season.SeasonID,
            PersonID = newCap.Id,
            CaptainName = "New Captain",
            TeamName = "The Hammers",
            OrderNumber = 2,
        }));
    }

    [Test]
    public async Task AddCaptain_Should_NameTeamAfterDisplayName_When_SignupHasNoTeamName()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("capuser", "Disp");
        await _context.AddSignup(season, cap);

        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = cap.Id, OrderNumber = 1 }, resortOnly: false);

        var team = await _context.NewContextLike().Teams.SingleAsync();
        Assert.That(team.TeamName, Is.EqualTo("Disp's Team"));
        Assert.That(await CaptainOrders(season.SeasonID), Is.EqualTo(new[] { (cap.Id, (int?)1) }));
        var note = Published<Notification<CaptainAddedDto>>().Single();
        Assert.That(note.Value.TeamName, Is.EqualTo("Disp's Team"));
        Assert.That(note.ConnectionId, Is.Null);
    }

    [Test]
    public async Task AddCaptain_FallbackTeamName_IgnoresGamertag_CurrentBehaviour()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("capuser", "Disp", gamertag: "RealGT", discordName: "disc");
        var capNoDisplay = await _context.AddUser("capuser2", null, gamertag: "OtherGT");
        await _context.AddSignup(season, cap);
        await _context.AddSignup(season, capNoDisplay);

        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = cap.Id, OrderNumber = 1 }, resortOnly: false);
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = capNoDisplay.Id, OrderNumber = 2 }, resortOnly: false);

        await using var ctx = _context.NewContextLike();
        var names = await ctx.Teams.Include(t => t.Captain).OrderBy(t => t.Captain.DraftCaptainOrder).Select(t => t.TeamName).ToListAsync();
        // BUG: TeamService.AddCaptain uses `name = ...DisplayName` instead of `name ??= ...DisplayName`, which discards the
        // gamertag/discord name looked up just before. Expected "RealGT's Team" and "OtherGT's Team"; actual uses DisplayName,
        // or UserName when DisplayName is null.
        Assert.That(names, Is.EqualTo(new[] { "Disp's Team", "capuser2's Team" }));
    }

    [Test]
    public async Task AddCaptain_Should_AddRandomSuffix_When_FallbackTeamNameTaken()
    {
        var season = await _context.AddSeason();
        var otherCap = await _context.AddUser("other", "Someone");
        await _context.AddTeam(season, "Disp's Team", otherCap, 1);
        var cap = await _context.AddUser("capuser", "Disp");
        await _context.AddSignup(season, cap);

        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = cap.Id, OrderNumber = 2 }, resortOnly: false);

        await using var ctx = _context.NewContextLike();
        var team = await ctx.Teams.Include(t => t.Captain).SingleAsync(t => t.Captain.UserID == cap.Id);
        Assert.That(team.TeamName, Does.Match(@"^Disp's Team [0-9a-f]{4}$"));
        Assert.That(await CaptainOrders(season.SeasonID), Is.EqualTo(new[] { (otherCap.Id, (int?)1), (cap.Id, (int?)2) }));
    }

    [Test]
    public async Task AddCaptain_Resort_Should_Throw_When_NotACaptain()
    {
        var season = await _context.AddSeason();
        var user = await _context.AddUser("u", "U");
        await _context.AddSignup(season, user);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = user.Id, OrderNumber = 1 }, resortOnly: true));
        Assert.That(ex!.Message, Is.EqualTo("Player is not a captain"));
    }

    [Test]
    public async Task AddCaptain_Resort_Should_MoveCaptain_And_PublishPlacementDto()
    {
        var season = await _context.AddSeason();
        var c1 = await _context.AddUser("c1", "C1");
        var c2 = await _context.AddUser("c2", "C2");
        var c3 = await _context.AddUser("c3", "C3");
        await _context.AddTeam(season, "T1", c1, 1);
        await _context.AddTeam(season, "T2", c2, 2);
        await _context.AddTeam(season, "T3", c3, 3);

        var dto = new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = c3.Id, OrderNumber = 1 };
        await _service.AddCaptain(dto, resortOnly: true, "conn-x");

        Assert.That(await CaptainOrders(season.SeasonID), Is.EqualTo(new[] { (c3.Id, (int?)1), (c1.Id, (int?)2), (c2.Id, (int?)3) }));
        Assert.That(await _context.NewContextLike().Teams.CountAsync(), Is.EqualTo(3), "resort must not create teams");
        var note = Published<Notification<CaptainPlacementDto>>().Single();
        Assert.That(note.Value, Is.SameAs(dto));
        Assert.That(note.ConnectionId, Is.EqualTo("conn-x"));
        Assert.That(Published<Notification<CaptainAddedDto>>(), Is.Empty);
    }

    // ---------------- RemoveCaptain ----------------

    [Test]
    public async Task RemoveCaptain_Should_Throw_When_NotACaptain()
    {
        var season = await _context.AddSeason();
        var user = await _context.AddUser("u", "U");

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.RemoveCaptain(new RemoveCaptainDto { SeasonID = season.SeasonID, PersonID = user.Id }));
        Assert.That(ex!.Message, Is.EqualTo("Player is not a captain"));
        Assert.That(_publisher.ReceivedCalls(), Is.Empty);
    }

    // ---------------- RemovePlayerFromTeam ----------------

    [Test]
    public async Task RemovePlayerFromTeam_Should_Remove_And_ResequenceRounds()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        var p1 = await _context.AddUser("p1", "P1");
        var p2 = await _context.AddUser("p2", "P2");
        var p3 = await _context.AddUser("p3", "P3");
        var team = await _context.AddTeam(season, "T", cap, 1, p1, p2, p3);

        var dto = new RemovePlayerFromTeamRequestDto { SeasonID = season.SeasonID, CaptainID = cap.Id, PersonID = p1.Id };
        await _service.RemovePlayerFromTeam(dto, "conn");

        var players = (await TeamPlayersOf(team.TeamID)).Where(tp => tp.DraftRound != null).ToList();
        Assert.That(players.Select(p => (p.UserID, p.DraftRound)), Is.EqualTo(new[] { (p2.Id, (int?)1), (p3.Id, (int?)2) }));
        Assert.That((await TeamPlayersOf(team.TeamID)).Any(tp => tp.UserID == cap.Id), Is.True, "captain stays");
        var note = Published<Notification<RemovePlayerFromTeamRequestDto>>().Single();
        Assert.That(note.Value, Is.SameAs(dto));
        Assert.That(note.ConnectionId, Is.EqualTo("conn"));
    }

    [Test]
    public async Task RemovePlayerFromTeam_Should_Throw_When_PlayerNotOnTeam()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        var p1 = await _context.AddUser("p1", "P1");
        await _context.AddTeam(season, "T", cap, 1);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.RemovePlayerFromTeam(new RemovePlayerFromTeamRequestDto { SeasonID = season.SeasonID, CaptainID = cap.Id, PersonID = p1.Id }));
        Assert.That(ex!.Message, Is.EqualTo("Player is not on this team"));
    }

    [Test]
    public async Task RemovePlayerFromTeam_Should_NotRemoveCaptain()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        await _context.AddTeam(season, "T", cap, 1);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.RemovePlayerFromTeam(new RemovePlayerFromTeamRequestDto { SeasonID = season.SeasonID, CaptainID = cap.Id, PersonID = cap.Id }));
        Assert.That(ex!.Message, Is.EqualTo("Player is not on this team"));
    }

    // ---------------- AddPlayerToTeam / OnDeck ----------------

    private async Task<(Season season, User capA, User capB, Team teamA, Team teamB)> TwoTeamDraft()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA", "Cap A");
        var capB = await _context.AddUser("capB", "Cap B");
        var teamA = await _context.AddTeam(season, "Team A", capA, 1);
        var teamB = await _context.AddTeam(season, "Team B", capB, 2);
        return (season, capA, capB, teamA, teamB);
    }

    [Test]
    public async Task AddPlayerToTeam_Should_Throw_When_PlayerAlreadyOnATeam()
    {
        var (season, capA, capB, _, _) = await TwoTeamDraft();
        var commish = await _context.AddUser("commish", "Commish", commissioner: true);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capA.Id, PersonID = capB.Id }, commish.Id));
        Assert.That(ex!.Message, Is.EqualTo("This player is already on a team"));
    }

    [Test]
    public async Task AddPlayerToTeam_Should_Throw_When_PlayerNotSignedUp()
    {
        var (season, capA, _, _, _) = await TwoTeamDraft();
        var commish = await _context.AddUser("commish", "Commish", commissioner: true);
        var p = await _context.AddUser("p", "P");

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capA.Id, PersonID = p.Id }, commish.Id));
        Assert.That(ex!.Message, Is.EqualTo("This player has not signed up"));
    }

    [Test]
    public async Task AddPlayerToTeam_Should_Throw_When_TeamNotFound()
    {
        var (season, _, _, _, _) = await TwoTeamDraft();
        var commish = await _context.AddUser("commish", "Commish", commissioner: true);
        var p = await _context.AddUser("p", "P");
        await _context.AddSignup(season, p);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = commish.Id, PersonID = p.Id }, commish.Id));
        Assert.That(ex!.Message, Is.EqualTo("Team not found"));
    }

    [Test]
    public async Task AddPlayerToTeam_Should_Reject_NonCaptainNonCommissioner()
    {
        var (season, capA, _, teamA, _) = await TwoTeamDraft();
        var rando = await _context.AddUser("rando", "Rando");
        var p = await _context.AddUser("p", "P");
        await _context.AddSignup(season, p);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capA.Id, PersonID = p.Id }, rando.Id));
        Assert.That(ex!.Message, Is.EqualTo("You cannot make picks on behalf of another captain unless you are the commissioner"));
        Assert.That(await TeamPlayersOf(teamA.TeamID), Has.Count.EqualTo(1));
        Assert.That(_publisher.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task AddPlayerToTeam_Should_Reject_Captain_When_NotTheirTurn()
    {
        var (season, capA, capB, _, teamB) = await TwoTeamDraft();
        var p = await _context.AddUser("p", "P");
        await _context.AddSignup(season, p);

        // Both teams have one member, so captain order 1 (A) is on deck
        var onDeck = await _service.OnDeck(season.SeasonID, CancellationToken.None);
        Assert.That(onDeck!.Captain.UserID, Is.EqualTo(capA.Id));

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capB.Id, PersonID = p.Id }, capB.Id));
        Assert.That(ex!.Message, Is.EqualTo($"It is not your turn to be making a draft pick. It is {capA.Id}'s turn"));
        Assert.That(await TeamPlayersOf(teamB.TeamID), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AddPlayerToTeam_Should_Allow_Captain_OnTheirTurn_And_AdvanceOnDeck()
    {
        var (season, capA, capB, teamA, _) = await TwoTeamDraft();
        var p = await _context.AddUser("p", "P");
        await _context.AddSignup(season, p);

        var dto = new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capA.Id, PersonID = p.Id };
        await _service.AddPlayerToTeam(dto, capA.Id, "conn-a");

        var players = await TeamPlayersOf(teamA.TeamID);
        Assert.That(players.Single(tp => tp.UserID == p.Id).DraftRound, Is.EqualTo(1));
        var note = Published<Notification<AddPlayerToTeamRequestDto>>().Single();
        Assert.That(note.Value, Is.SameAs(dto));
        Assert.That(note.ConnectionId, Is.EqualTo("conn-a"));

        // Team A now has more members, so B is on deck
        var fresh = new TeamService(_context.NewContextLike(), _publisher);
        var onDeck = await fresh.OnDeck(season.SeasonID, CancellationToken.None);
        Assert.That(onDeck!.Captain.UserID, Is.EqualTo(capB.Id));
    }

    [Test]
    public async Task AddPlayerToTeam_Should_Allow_Commissioner_AnyTurn_And_AppendDraftRound()
    {
        var (season, _, capB, _, teamB) = await TwoTeamDraft();
        var commish = await _context.AddUser("commish", "Commish", commissioner: true);
        var p1 = await _context.AddUser("p1", "P1");
        var p2 = await _context.AddUser("p2", "P2");
        await _context.AddSignup(season, p1);
        await _context.AddSignup(season, p2);

        await _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capB.Id, PersonID = p1.Id }, commish.Id);
        await _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capB.Id, PersonID = p2.Id }, commish.Id);

        var players = (await TeamPlayersOf(teamB.TeamID)).Where(tp => tp.UserID != capB.Id).ToList();
        Assert.That(players.Select(x => (x.UserID, x.DraftRound)), Is.EqualTo(new[] { (p1.Id, (int?)1), (p2.Id, (int?)2) }));
        Assert.That(Published<Notification<AddPlayerToTeamRequestDto>>(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task OnDeck_Should_ReturnNull_When_NoTeams()
    {
        var season = await _context.AddSeason();
        Assert.That(await _service.OnDeck(season.SeasonID, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task OnDeck_Should_PickFewestPlayers_ThenLowestCaptainOrder()
    {
        var season = await _context.AddSeason();
        var c1 = await _context.AddUser("c1");
        var c2 = await _context.AddUser("c2");
        var c3 = await _context.AddUser("c3");
        var p1 = await _context.AddUser("p1");
        var p2 = await _context.AddUser("p2");
        await _context.AddTeam(season, "T1", c1, 1, p1);
        await _context.AddTeam(season, "T3", c3, 3);
        await _context.AddTeam(season, "T2", c2, 2, p2);

        var onDeck = await _service.OnDeck(season.SeasonID, CancellationToken.None);
        Assert.That(onDeck!.Captain.UserID, Is.EqualTo(c3.Id));
        Assert.That(onDeck.TeamName, Is.EqualTo("T3"));
    }

    // ---------------- MovePlayerToTeam ----------------

    [Test]
    public async Task MovePlayerToTeam_BetweenTeams_Should_ResequenceBothTeams_And_Publish()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA");
        var capB = await _context.AddUser("capB");
        var a1 = await _context.AddUser("a1");
        var a2 = await _context.AddUser("a2");
        var a3 = await _context.AddUser("a3");
        var b1 = await _context.AddUser("b1");
        var teamA = await _context.AddTeam(season, "A", capA, 1, a1, a2, a3);
        var teamB = await _context.AddTeam(season, "B", capB, 2, b1);

        var dto = new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = capA.Id, NewCaptainID = capB.Id, PersonID = a1.Id, RoundNumber = 1 };
        await _service.MovePlayerToTeam(dto, "conn-m");

        var a = (await TeamPlayersOf(teamA.TeamID)).Where(tp => tp.UserID != capA.Id).Select(tp => (tp.UserID, tp.DraftRound));
        var b = (await TeamPlayersOf(teamB.TeamID)).Where(tp => tp.UserID != capB.Id).Select(tp => (tp.UserID, tp.DraftRound));
        Assert.That(a, Is.EqualTo(new[] { (a2.Id, (int?)1), (a3.Id, (int?)2) }));
        Assert.That(b, Is.EqualTo(new[] { (a1.Id, (int?)1), (b1.Id, (int?)2) }));
        var note = Published<Notification<MovePlayerToTeamRequestDto>>().Single();
        Assert.That(note.Value, Is.SameAs(dto));
        Assert.That(note.ConnectionId, Is.EqualTo("conn-m"));
    }

    [Test]
    public async Task MovePlayerToTeam_WithinTeam_Should_Reorder()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap");
        var p1 = await _context.AddUser("p1");
        var p2 = await _context.AddUser("p2");
        var p3 = await _context.AddUser("p3");
        var team = await _context.AddTeam(season, "A", cap, 1, p1, p2, p3);

        await _service.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = cap.Id, NewCaptainID = cap.Id, PersonID = p3.Id, RoundNumber = 1 });

        var order = (await TeamPlayersOf(team.TeamID)).Where(tp => tp.UserID != cap.Id).Select(tp => (tp.UserID, tp.DraftRound));
        Assert.That(order, Is.EqualTo(new[] { (p3.Id, (int?)1), (p1.Id, (int?)2), (p2.Id, (int?)3) }));
    }

    [Test]
    public async Task MovePlayerToTeam_WithinTeam_Should_Throw_When_PlayerNotOnTeam()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap");
        var p1 = await _context.AddUser("p1");
        await _context.AddTeam(season, "A", cap, 1);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = cap.Id, NewCaptainID = cap.Id, PersonID = p1.Id, RoundNumber = 1 }));
        Assert.That(ex!.Message, Is.EqualTo("Player is not on that team"));
    }

    [Test]
    public async Task MovePlayerToTeam_BetweenTeams_Should_Throw_When_PlayerNotOnPreviousTeam()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA");
        var capB = await _context.AddUser("capB");
        var p1 = await _context.AddUser("p1");
        await _context.AddTeam(season, "A", capA, 1);
        await _context.AddTeam(season, "B", capB, 2, p1);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = capA.Id, NewCaptainID = capB.Id, PersonID = p1.Id, RoundNumber = 1 }));
        Assert.That(ex!.Message, Is.EqualTo("Player was not on that team"));
    }

    [Test]
    public async Task MovePlayerToTeam_Should_Throw_When_NewTeamDoesNotExist()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA");
        var notCap = await _context.AddUser("notCap");
        var p1 = await _context.AddUser("p1");
        var team = await _context.AddTeam(season, "A", capA, 1, p1);

        var ex = Assert.ThrowsAsync<TeamServiceException>(() =>
            _service.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = capA.Id, NewCaptainID = notCap.Id, PersonID = p1.Id, RoundNumber = 1 }));
        Assert.That(ex!.Message, Is.EqualTo("New team does not exist"));
        Assert.That((await TeamPlayersOf(team.TeamID)).Any(tp => tp.UserID == p1.Id), Is.True);
        Assert.That(_publisher.ReceivedCalls(), Is.Empty);
    }

    // ---------------- Lock ----------------

    [Test]
    public void LockCaptains_Should_Throw_When_SeasonMissing()
    {
        var ex = Assert.ThrowsAsync<TeamServiceException>(() => _service.LockCaptains(9999, true));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public void AreCaptainsLocked_Should_Throw_When_SeasonMissing()
    {
        var ex = Assert.ThrowsAsync<TeamServiceException>(() => _service.AreCaptainsLocked(9999));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task LockCaptains_Should_Persist_And_Publish_LockChanged()
    {
        var season = await _context.AddSeason();
        Assert.That(await _service.AreCaptainsLocked(season.SeasonID), Is.False);

        await _service.LockCaptains(season.SeasonID, true, "conn-l");
        Assert.That(await new TeamService(_context.NewContextLike(), _publisher).AreCaptainsLocked(season.SeasonID), Is.True);

        await _service.LockCaptains(season.SeasonID, false);
        Assert.That(await new TeamService(_context.NewContextLike(), _publisher).AreCaptainsLocked(season.SeasonID), Is.False);

        Assert.That(Published<LockChanged>(), Is.EqualTo(new[]
        {
            new LockChanged(true, season.SeasonID, "conn-l"),
            new LockChanged(false, season.SeasonID, null),
        }));
    }
}
