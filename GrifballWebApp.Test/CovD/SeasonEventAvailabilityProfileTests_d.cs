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

namespace GrifballWebApp.Test.CovD;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class EventOrganizerControllerTests_d
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
        Assert.That(ControllerTestHelpers_d.ClassAuthorize(typeof(EventOrganizerController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
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
        var source = await SeedHelpers_d.Season(_context, "Source Season");
        var options = await SignupSeed_d.Options(_context, source, (DayOfWeek.Monday, new TimeOnly(3, 17)), (DayOfWeek.Tuesday, new TimeOnly(3, 17)));
        var team = await SeedHelpers_d.TeamWithCaptain(_context, source, "CapGT", 31);
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
        var source = await SeedHelpers_d.Season(_context, "Avail Source");
        var srcOpts = await SignupSeed_d.Options(_context, source, (DayOfWeek.Friday, new TimeOnly(5, 17)));
        var target = await SeedHelpers_d.Season(_context, "Avail Target");
        await SignupSeed_d.Options(_context, target, (DayOfWeek.Sunday, new TimeOnly(6, 17)));
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

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonAndAvailabilityControllerTests_d
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task SeasonController_ReturnsCurrentSeasonIdAndName()
    {
        var now = DateTime.UtcNow;
        var past = new Season { SeasonName = "Past", SeasonStart = now.AddDays(-60), SeasonEnd = now.AddDays(-30) };
        var current = new Season { SeasonName = "Current", SeasonStart = now.AddDays(-1), SeasonEnd = now.AddDays(10) };
        _context.Seasons.AddRange(past, current);
        await _context.SaveChangesAsync();
        var controller = new SeasonController(new EventOrganizerService(_context), new SeasonService(_context));

        Assert.That(await controller.GetCurrentSeasonID(CancellationToken.None), Is.EqualTo(current.SeasonID));
        Assert.That(await controller.GetSeasonName(past.SeasonID, CancellationToken.None), Is.EqualTo("Past"));
        Assert.That(await controller.GetSeasonName(123456, CancellationToken.None), Is.Null);
    }

    [Test]
    public void SeasonController_IsAnonymous()
    {
        Assert.That(ControllerTestHelpers_d.ClassAuthorize(typeof(SeasonController)), Is.Null);
    }

    [Test]
    public async Task AvailabilityController_UpdateThenGet_RoundTrips()
    {
        var season = await SeedHelpers_d.Season(_context);
        var controller = new AvailabilityController(Substitute.For<ILogger<AvailabilityController>>(), new AvailabilityService(_context));
        var slot = new AvailabilityTimeslotDto { DayOfWeek = DayOfWeek.Thursday, Time = new TimeOnly(7, 17) };

        await controller.UpdateSeasonAvailability(new SeasonAvailabilityDto { SeasonID = season.SeasonID, Timeslots = [slot] }, CancellationToken.None);
        var result = await controller.GetSeasonAvailability(season.SeasonID, CancellationToken.None);

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].DayOfWeek, Is.EqualTo(DayOfWeek.Thursday));
        Assert.That(result[0].Time, Is.EqualTo(new TimeOnly(7, 17)));
    }

    [Test]
    public void AvailabilityController_UpdateRequiresCommissioner()
    {
        var t = typeof(AvailabilityController);
        Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(AvailabilityController.UpdateSeasonAvailability))!.Roles, Is.EqualTo("Commissioner"));
        Assert.That(ControllerTestHelpers_d.ActionAuthorize(t, nameof(AvailabilityController.GetSeasonAvailability)), Is.Null);
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ProfileControllerSetGamertagTests_d
{
    private GrifballContext _context;
    private ISetGamertagService _setGamertag;
    private ProfileController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _setGamertag = Substitute.For<ISetGamertagService>();
        _controller = new ProfileController(_context, _setGamertag);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void SetGamertag_RequiresPlayerRole()
    {
        Assert.That(ControllerTestHelpers_d.ActionAuthorize(typeof(ProfileController), nameof(ProfileController.SetGamertag))!.Roles, Is.EqualTo("Player"));
    }

    [Test]
    public async Task SetGamertag_OwnUser_CallsService()
    {
        _controller.WithUser("42", "me", "Player");
        _setGamertag.SetGamertag(42, "NewGT", Arg.Any<CancellationToken>()).Returns((string?)null);

        await _controller.SetGamertag(42, "NewGT", CancellationToken.None);

        await _setGamertag.Received(1).SetGamertag(42, "NewGT", Arg.Any<CancellationToken>());
    }

    [Test]
    public void SetGamertag_OtherUser_Throws()
    {
        _controller.WithUser("42", "me", "Player");

        var ex = Assert.Throws<Exception>(() => _controller.SetGamertag(43, "NewGT", CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Cannot set another user's gamertag"));
        _setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!, default);
    }

    [TestCase(null)]
    [TestCase("garbage")]
    public void SetGamertag_NoOrInvalidId_Throws(string? claim)
    {
        if (claim is null) _controller.WithAnonymousUser(); else _controller.WithUser(claim);

        Assert.Throws<Exception>(() => _controller.SetGamertag(1, "NewGT", CancellationToken.None));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TransferLegacyDiscordServiceTests_d
{
    private GrifballContext _context;
    private const string NameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> UserWithLogin(string name, string provider, string key, string? claimName)
    {
        var user = new User { UserName = name };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.UserLogins.Add(new UserLogin { UserId = user.Id, LoginProvider = provider, ProviderKey = key, ProviderDisplayName = provider });
        if (claimName is not null)
            _context.UserClaims.Add(new UserClaim { UserId = user.Id, ClaimType = NameClaim, ClaimValue = claimName });
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task TransferAllAsync_CreatesDiscordUsersAndLinks()
    {
        var withClaim = await UserWithLogin("a", "Discord", "1001", "discordA");
        var noClaim = await UserWithLogin("b", "Discord", "1002", null);
        var google = await UserWithLogin("c", "Google", "1003", "googleC");
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 1004, DiscordUsername = "existing" });
        await _context.SaveChangesAsync();
        var existingDiscord = await UserWithLogin("d", "Discord", "1004", null);

        await new TransferLegacyDiscordService(_context).TransferAllAsync();

        _context.ChangeTracker.Clear();
        var users = await _context.Users.ToDictionaryAsync(u => u.UserName!);
        Assert.Multiple(() =>
        {
            Assert.That(users["a"].DiscordUserID, Is.EqualTo(1001));
            Assert.That(users["b"].DiscordUserID, Is.Null, "no name claim -> skipped");
            Assert.That(users["c"].DiscordUserID, Is.Null, "non-Discord provider ignored");
            Assert.That(users["d"].DiscordUserID, Is.EqualTo(1004), "existing discord user linked");
        });
        var discordUsers = await _context.DiscordUsers.ToDictionaryAsync(d => d.DiscordUserID);
        Assert.That(discordUsers[1001].DiscordUsername, Is.EqualTo("discordA"));
        Assert.That(discordUsers[1004].DiscordUsername, Is.EqualTo("existing"));
        Assert.That(discordUsers.ContainsKey(1002), Is.False);
    }

    [Test]
    public async Task TransferAllAsync_AlreadyLinkedUsersAreSkipped()
    {
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 2001, DiscordUsername = "orig" });
        await _context.SaveChangesAsync();
        var user = await UserWithLogin("linked", "Discord", "2002", "other");
        user.DiscordUserID = 2001;
        await _context.SaveChangesAsync();

        await new TransferLegacyDiscordService(_context).TransferAllAsync();

        _context.ChangeTracker.Clear();
        Assert.That((await _context.Users.SingleAsync(u => u.Id == user.Id)).DiscordUserID, Is.EqualTo(2001));
        Assert.That(await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == 2002), Is.False);
    }
}
