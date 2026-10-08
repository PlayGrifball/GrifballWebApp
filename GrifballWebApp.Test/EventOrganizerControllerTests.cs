using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AvailabilityTimeslotDto = GrifballWebApp.Server.Availability.TimeslotDto;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class EventOrganizerControllerTests
{
    private GrifballContext _context;
    private EventOrganizerController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new EventOrganizerController(new EventOrganizerService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static SeasonDto Dto(string name, int id = 0) => new()
    {
        SeasonID = id,
        SeasonName = name,
        SignupsOpen = new DateTime(2030, 1, 1),
        SignupsClose = new DateTime(2030, 1, 10),
        DraftStart = new DateTime(2030, 1, 12),
        SeasonStart = new DateTime(2030, 1, 15),
        SeasonEnd = new DateTime(2030, 3, 1),
    };

    [Test]
    public void Controller_RequiresCommissionerOrSysadmin()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(EventOrganizerController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
    }

    [Test]
    public async Task UpsertThenGet_RoundTripsSeason()
    {
        var id = (int)((OkObjectResult)await _controller.UpsertSeason(Dto("Round Trip"), CancellationToken.None)).Value!;

        var one = (SeasonDto?)((OkObjectResult)await _controller.GetSeason(id, CancellationToken.None)).Value;
        var all = (List<SeasonDto>)((OkObjectResult)await _controller.GetSeasons(CancellationToken.None)).Value!;

        Assert.That(one, Is.Not.Null);
        Assert.That(one!.SeasonName, Is.EqualTo("Round Trip"));
        Assert.That(one.DraftStart, Is.EqualTo(new DateTime(2030, 1, 12)));
        Assert.That(all.Select(s => s.SeasonID), Does.Contain(id));
    }

    [Test]
    public async Task GetSeason_Missing_ReturnsOkWithNull()
    {
        var result = (OkObjectResult)await _controller.GetSeason(98765, CancellationToken.None);
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public void UpsertSeason_CopyFromMissingSeason_Throws()
    {
        var dto = Dto("New");
        dto.CopyFrom = "Does Not Exist";

        var ex = Assert.ThrowsAsync<Exception>(() => _controller.UpsertSeason(dto, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Cannot copy from season Does Not Exist, it was not found"));
    }

    [Test]
    public async Task UpsertSeason_CopyAll_CopiesAvailabilitySignupsTeamsAndCaptain()
    {
        var source = await TeamsTestData.AddCurrentSeason(_context, "Source Season");
        var options = await SignupTestData.Options(_context, source, (DayOfWeek.Monday, new TimeOnly(3, 17)), (DayOfWeek.Tuesday, new TimeOnly(3, 17)));
        var team = await TeamsTestData.AddTeamWithCaptain(_context, source, "CapGT", 31);
        _context.TeamAvailability.Add(new TeamAvailability { TeamID = team.TeamID, AvailabilityOptionID = options[0].AvailabilityOptionID });
        var signupUser = new User { UserName = "signupper" };
        _context.Users.Add(signupUser);
        await _context.SaveChangesAsync();
        var signup = new SeasonSignup { SeasonID = source.SeasonID, UserID = signupUser.Id, TeamName = "SignupTeam", Timestamp = DateTime.UtcNow, WillCaptain = true };
        signup.SignupAvailability.Add(new SignupAvailability { AvailabilityOptionID = options[1].AvailabilityOptionID });
        _context.SeasonSignups.Add(signup);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dto = Dto("Copied Season");
        dto.CopyFrom = "Source Season";
        dto.CopyAvailability = true;
        dto.CopySignups = true;
        dto.CopyTeams = true;
        var newId = (int)((OkObjectResult)await _controller.UpsertSeason(dto, CancellationToken.None)).Value!;

        _context.ChangeTracker.Clear();
        var copied = await _context.Seasons
            .Include(s => s.SeasonAvailability)
            .Include(s => s.SeasonSignups).ThenInclude(s => s.SignupAvailability)
            .Include(s => s.Teams).ThenInclude(t => t.TeamPlayers)
            .Include(s => s.Teams).ThenInclude(t => t.TeamAvailability)
            .Include(s => s.Teams).ThenInclude(t => t.Captain)
            .SingleAsync(s => s.SeasonID == newId);

        Assert.That(newId, Is.Not.EqualTo(source.SeasonID));
        Assert.Multiple(() =>
        {
            Assert.That(copied.SeasonName, Is.EqualTo("Copied Season"));
            Assert.That(copied.SeasonAvailability.Select(a => a.AvailabilityOptionID), Is.EquivalentTo(options.Select(o => o.AvailabilityOptionID)));
            Assert.That(copied.SeasonSignups.Single().TeamName, Is.EqualTo("SignupTeam"));
            Assert.That(copied.SeasonSignups.Single().SignupAvailability.Single().AvailabilityOptionID, Is.EqualTo(options[1].AvailabilityOptionID));
            var copiedTeam = copied.Teams.Single();
            Assert.That(copiedTeam.TeamID, Is.Not.EqualTo(team.TeamID));
            Assert.That(copiedTeam.TeamName, Is.EqualTo("Team CapGT"));
            Assert.That(copiedTeam.TeamAvailability.Single().AvailabilityOptionID, Is.EqualTo(options[0].AvailabilityOptionID));
            Assert.That(copiedTeam.Captain, Is.Not.Null);
            Assert.That(copiedTeam.Captain.TeamID, Is.EqualTo(copiedTeam.TeamID));
        });
        // source untouched
        Assert.That(await _context.Teams.CountAsync(t => t.SeasonID == source.SeasonID), Is.EqualTo(1));
    }

    [Test]
    public async Task UpsertSeason_ExistingSeason_CopyAvailabilityOnly_ReplacesAvailability()
    {
        var source = await TeamsTestData.AddCurrentSeason(_context, "Avail Source");
        var srcOpts = await SignupTestData.Options(_context, source, (DayOfWeek.Friday, new TimeOnly(5, 17)));
        var target = await TeamsTestData.AddCurrentSeason(_context, "Avail Target");
        await SignupTestData.Options(_context, target, (DayOfWeek.Sunday, new TimeOnly(6, 17)));
        _context.ChangeTracker.Clear();

        var dto = Dto("Avail Target", target.SeasonID);
        dto.CopyFrom = "Avail Source";
        dto.CopyAvailability = true;
        await _controller.UpsertSeason(dto, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var ids = await _context.SeasonAvailability.Where(x => x.SeasonID == target.SeasonID).Select(x => x.AvailabilityOptionID).ToListAsync();
        Assert.That(ids, Is.EqualTo(new[] { srcOpts[0].AvailabilityOptionID }));
    }
}
