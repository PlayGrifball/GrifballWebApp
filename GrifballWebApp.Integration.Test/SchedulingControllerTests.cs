using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Scheduling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The scheduling API (<see cref="SchedulingController"/>): a match's time checked and suggested, a team's timeslots (its
/// captain or staff set them, or reset them to its roster's), and a season's scheduling rules (staff change them).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SchedulingControllerTests
{
    private GrifballContext _context = null!;
    private SchedulingController _controller = null!;
    private SchedulingTestData _s = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _s = await SchedulingTestData.SeedAsync(_context);
        _controller = new SchedulingController(new SchedulingService(_context, new FakeTimeProvider(SchedulingTestData.Now)),
            new TeamAvailabilityService(_context), new SeasonSchedulingRulesService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private int RedsCaptain => SchedulingTestData.CaptainOf(_s.Reds);
    private int BluesCaptain => SchedulingTestData.CaptainOf(_s.Blues);

    private static T Body<T>(IActionResult result) => (T)((OkObjectResult)result).Value!;

    [Test]
    public async Task TimeSuggestions_TheBestFirst_AndNoSuchMatch_IsNotFound()
    {
        _controller.WithUser(RedsCaptain.ToString());

        var suggestions = Body<IReadOnlyList<TimeSuggestion>>(await _controller.GetTimeSuggestions(_s.Match.SeasonMatchID, null, null, 2, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(suggestions.Select(t => t.StartsAt), Is.EqualTo(new[] { SchedulingTestData.Monday, SchedulingTestData.Monday.AddDays(7) }));
            (await _controller.GetTimeSuggestions(999, null, null, 2, CancellationToken.None)).AssertProblem(StatusCodes.Status404NotFound);
        }
    }

    [TestCase(0, 1)]
    [TestCase(100, SchedulingController.MaxSuggestions)]
    public void TimeSuggestions_HowMany_IsKeptToAFew(int asked, int given)
        => Assert.That(SchedulingController.HowMany(asked), Is.EqualTo(given));

    [Test]
    public async Task TimeCheck_SaysWhatIsWrong_AndNoSuchMatch_IsNotFound()
    {
        _controller.WithUser(RedsCaptain.ToString());

        var clash = Body<IReadOnlyList<ScheduleConflict>>(await _controller.CheckTime(_s.Match.SeasonMatchID, SchedulingTestData.Tuesday, CancellationToken.None));
        var free = Body<IReadOnlyList<ScheduleConflict>>(await _controller.CheckTime(_s.Match.SeasonMatchID, SchedulingTestData.Monday, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clash.Select(c => c.Kind), Does.Contain(ScheduleConflictKind.TeamPlaysThen));
            Assert.That(free, Is.Empty);
            (await _controller.CheckTime(999, SchedulingTestData.Monday, CancellationToken.None)).AssertProblem(StatusCodes.Status404NotFound);
        }
    }

    [Test]
    public async Task TeamAvailability_TheRostersUntilTheCaptainSetsIt_ThenReset()
    {
        _controller.WithUser(RedsCaptain.ToString());

        var roster = Body<TeamSlots>(await _controller.GetTeamAvailability(_s.Reds.TeamID, CancellationToken.None));
        var set = await _controller.SetTeamAvailability(_s.Reds.TeamID, new TeamAvailabilityDto { AvailabilityOptionIDs = [_s.TuesdaysID] }, CancellationToken.None);
        var own = Body<TeamSlots>(await _controller.GetTeamAvailability(_s.Reds.TeamID, CancellationToken.None));
        var reset = await _controller.ResetTeamAvailability(_s.Reds.TeamID, CancellationToken.None);
        var again = Body<TeamSlots>(await _controller.GetTeamAvailability(_s.Reds.TeamID, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(roster.SetByCaptain, Is.False);
            Assert.That(roster.Slots.Where(t => t.Available).Select(t => t.AvailabilityOptionID), Is.EqualTo(new[] { _s.MondaysID }));
            Assert.That(set, Is.TypeOf<NoContentResult>());
            Assert.That(own.SetByCaptain, Is.True);
            Assert.That(own.Slots.Where(t => t.Available).Select(t => t.AvailabilityOptionID), Is.EqualTo(new[] { _s.TuesdaysID }));
            Assert.That(reset, Is.TypeOf<NoContentResult>());
            Assert.That(again.SetByCaptain, Is.False);
            (await _controller.GetTeamAvailability(999, CancellationToken.None)).AssertProblem(StatusCodes.Status404NotFound);
        }
    }

    [Test]
    public async Task TeamAvailability_AnotherTeamsCaptain_IsForbidden_StaffMay_AndTheRefusalsSayWhy()
    {
        _controller.WithUser(BluesCaptain.ToString());
        var forbidden = await _controller.SetTeamAvailability(_s.Reds.TeamID, new TeamAvailabilityDto { AvailabilityOptionIDs = [_s.MondaysID] }, CancellationToken.None);
        var resetForbidden = await _controller.ResetTeamAvailability(_s.Reds.TeamID, CancellationToken.None);

        _controller.WithUser(BluesCaptain.ToString(), null, "Commissioner");
        var staff = await _controller.SetTeamAvailability(_s.Reds.TeamID, new TeamAvailabilityDto { AvailabilityOptionIDs = [_s.MondaysID] }, CancellationToken.None);
        var none = await _controller.SetTeamAvailability(_s.Reds.TeamID, new TeamAvailabilityDto { AvailabilityOptionIDs = [] }, CancellationToken.None);
        var missing = await _controller.SetTeamAvailability(999, new TeamAvailabilityDto { AvailabilityOptionIDs = [_s.MondaysID] }, CancellationToken.None);
        var resetMissing = await _controller.ResetTeamAvailability(999, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            forbidden.AssertProblem(StatusCodes.Status403Forbidden, TeamAvailabilityService.NotTheCaptain);
            resetForbidden.AssertProblem(StatusCodes.Status403Forbidden, TeamAvailabilityService.NotTheCaptain);
            Assert.That(staff, Is.TypeOf<NoContentResult>());
            none.AssertProblem(StatusCodes.Status400BadRequest, TeamAvailabilityService.NoSlots);
            missing.AssertProblem(StatusCodes.Status404NotFound, TeamAvailabilityService.NoSuchTeam);
            resetMissing.AssertProblem(StatusCodes.Status404NotFound, TeamAvailabilityService.NoSuchTeam);
        }
    }

    [Test]
    public async Task TeamAvailability_SaysWhetherTheCallerMayChangeIt_AsTheServerDecides()
    {
        _controller.WithUser(RedsCaptain.ToString());
        var captain = Body<TeamSlots>(await _controller.GetTeamAvailability(_s.Reds.TeamID, CancellationToken.None));
        _controller.WithUser(BluesCaptain.ToString());
        var other = Body<TeamSlots>(await _controller.GetTeamAvailability(_s.Reds.TeamID, CancellationToken.None));
        _controller.WithUser(BluesCaptain.ToString(), null, "Sysadmin");
        var staff = Body<TeamSlots>(await _controller.GetTeamAvailability(_s.Reds.TeamID, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(captain.CanChange, Is.True);
            Assert.That(other.CanChange, Is.False, "Another team's captain");
            Assert.That(staff.CanChange, Is.True);
        }
    }

    [Test]
    public async Task SchedulingRules_TheDefaults_UntilStaffChangeThem_AndTheRefusalsSayWhy()
    {
        _controller.WithUser(RedsCaptain.ToString(), null, "Commissioner");

        var defaults = await _controller.GetSchedulingRules(_s.SeasonID, CancellationToken.None);
        var changed = await _controller.ChangeSchedulingRules(_s.SeasonID, Rules(3, 1, 3), CancellationToken.None);
        var now = await _controller.GetSchedulingRules(_s.SeasonID, CancellationToken.None);
        var refused = await _controller.ChangeSchedulingRules(_s.SeasonID, Rules(0, 1, 3), CancellationToken.None);
        var missing = await _controller.ChangeSchedulingRules(999, Rules(3, 1, 3), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaults.MinPlayersAvailable, Is.EqualTo(Domain.League.SeasonSchedulingRules.DefaultMinPlayersAvailable));
            Assert.That(changed, Is.TypeOf<NoContentResult>());
            Assert.That((now.MinPlayersAvailable, now.MaxMatchesPerTeamPerWeek), Is.EqualTo((3, 3)));
            refused.AssertProblem(StatusCodes.Status400BadRequest, SeasonSchedulingRulesService.NoPlayers);
            missing.AssertProblem(StatusCodes.Status404NotFound, SeasonSchedulingRulesService.NoSuchSeason);
        }
    }

    [Test]
    public void ChangingTheRules_IsStaffs_TheRestAnyoneSignedIns()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(SchedulingController)), Is.Not.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(SchedulingController), nameof(SchedulingController.ChangeSchedulingRules))!.Roles,
                Is.EqualTo("Commissioner,Sysadmin"));
        }
    }

    private static SeasonSchedulingRulesDto Rules(int players, int night, int week)
        => new() { MinPlayersAvailable = players, MaxMatchesPerTeamPerNight = night, MaxMatchesPerTeamPerWeek = week };
}
