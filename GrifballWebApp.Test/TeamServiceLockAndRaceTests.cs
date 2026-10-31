using GrifballWebApp.Database;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>
/// The draft rules TeamService enforces itself: Season.CaptainsLocked, one pick at a time per season, unique team names,
/// and the CaptainAdded broadcast.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamServiceLockAndRaceTests
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
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task CaptainChanges_AreRefused_WhileCaptainsAreLocked()
    {
        var season = await _context.AddSeason(captainsLocked: true);
        var cap = await _context.AddUser("cap", "Cap");
        await _context.AddTeam(season, "T", cap, 1);
        var newCap = await _context.AddUser("new", "New");
        await _context.AddSignup(season, newCap);

        var add = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = newCap.Id, OrderNumber = 1 }, resortOnly: false));
        var resort = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = cap.Id, OrderNumber = 1 }, resortOnly: true));
        var remove = await Assert.ThrowsAsync<TeamServiceException>(() => _service.RemoveCaptain(new RemoveCaptainDto { SeasonID = season.SeasonID, PersonID = cap.Id }));

        Assert.Multiple(async () =>
        {
            Assert.That(new[] { add!.Message, resort!.Message, remove!.Message }, Is.All.EqualTo("Captains are locked for this season. Unlock them to change the captains"));
            Assert.That(await _context.NewContextLike().Teams.CountAsync(), Is.EqualTo(1));
            Assert.That(_publisher.ReceivedCalls(), Is.Empty);
        });
    }

    [Test]
    public async Task CaptainPick_BeforeCaptainsAreLocked_IsRefused_ButACommissionerMayPlaceAPlayer()
    {
        var season = await _context.AddSeason(captainsLocked: false);
        var cap = await _context.AddUser("cap", "Cap");
        var team = await _context.AddTeam(season, "T", cap, 1);
        var p1 = await _context.AddUser("p1", "P1");
        var p2 = await _context.AddUser("p2", "P2");
        await _context.AddSignup(season, p1);
        await _context.AddSignup(season, p2);
        var commish = await _context.AddUser("commish", "Commish", commissioner: true);

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = cap.Id, PersonID = p1.Id }, cap.Id));
        await _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = cap.Id, PersonID = p2.Id }, commish.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(ex!.Message, Is.EqualTo("The draft has not started: the captains are not locked yet"));
            Assert.That(await _context.NewContextLike().TeamPlayers.Where(x => x.TeamID == team.TeamID).Select(x => x.UserID).ToListAsync(), Is.EquivalentTo(new[] { cap.Id, p2.Id }));
        });
    }

    [Test]
    public async Task SimultaneousPicksOfOnePlayer_PutThemOnOneTeamOnly()
    {
        var season = await _context.AddSeason(captainsLocked: true);
        var capA = await _context.AddUser("capA", "Cap A");
        var capB = await _context.AddUser("capB", "Cap B");
        await _context.AddTeam(season, "A", capA, 1);
        await _context.AddTeam(season, "B", capB, 2);
        var player = await _context.AddUser("p", "P");
        await _context.AddSignup(season, player);
        var commish = await _context.AddUser("commish", "Commish", commissioner: true);

        // Two requests at once, each with its own scope (DbContext), as two web requests have
        var contexts = Enumerable.Range(0, 6).Select(_ => _context.NewContextLike()).ToList();
        var results = await Task.WhenAll(contexts.Select(async (ctx, i) =>
        {
            var service = new TeamService(ctx, Substitute.For<IPublisher>());
            try
            {
                await service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = i % 2 == 0 ? capA.Id : capB.Id, PersonID = player.Id }, commish.Id);
                return null;
            }
            catch (TeamServiceException e)
            {
                return e.Message;
            }
        }));
        foreach (var ctx in contexts)
            await ctx.DisposeAsync();

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(await fresh.TeamPlayers.CountAsync(x => x.UserID == player.Id), Is.EqualTo(1));
            Assert.That(results.Count(x => x is null), Is.EqualTo(1), "One pick wins");
            Assert.That(results.Where(x => x is not null), Is.All.EqualTo("This player is already on a team"), "The others are told why");
        });
    }

    [Test]
    public async Task AddCaptain_RequestedTeamNameTaken_GetsASuffix_AndFitsTheColumn()
    {
        var season = await _context.AddSeason();
        var other = await _context.AddUser("other", "Other");
        const string name = "The Thirty Character Team Name";
        await _context.AddTeam(season, name, other, 1);
        var cap = await _context.AddUser("cap", "Cap");
        await _context.AddSignup(season, cap, teamName: name);

        var returned = await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = cap.Id, OrderNumber = 2 }, resortOnly: false);

        Assert.That(returned, Does.Match("^The Thirty Character Team [0-9a-f]{4}$"));
        Assert.That(returned!.Length, Is.LessThanOrEqualTo(30));
    }

    [Test]
    public async Task AddCaptain_Broadcast_NamesTheCaptainAsTheDraftDoes_WithTheRealTeamId()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("capuser", displayName: null, gamertag: "CapGT");
        await _context.AddSignup(season, cap);

        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = cap.Id, OrderNumber = 1 }, resortOnly: false);

        var note = (Notification<CaptainAddedDto>)_publisher.ReceivedCalls().Single().GetArguments()[0]!;
        var teamId = await _service.CaptainsTeamID(season.SeasonID, cap.Id);
        var teams = await _service.GetTeams(season.SeasonID);
        Assert.Multiple(() =>
        {
            Assert.That(note.Value.CaptainName, Is.EqualTo("CapGT"));
            Assert.That(note.Value.CaptainName, Is.EqualTo(teams.Single().Captain.Name));
            Assert.That(note.Value.TeamID, Is.Not.Zero.And.EqualTo(teamId));
            Assert.That(teams.Single().TeamID, Is.EqualTo(teamId));
        });
    }
}
