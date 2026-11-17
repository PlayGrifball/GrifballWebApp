using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A team's availability (<see cref="TeamAvailabilityService"/>): worked out from its roster's signup timeslots (a slot is
/// playable when enough of them can play it, the season's rule), unless its captain set its own; and the season's rules
/// (<see cref="SeasonSchedulingRulesService"/>).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamAvailabilityServiceUnitTests
{
    // A database of its own (the real providers: SQLite with the app's query rewrites, SQL Server, Postgres)
    private GrifballContext Context = null!;

    [SetUp]
    public async Task SetUpContext() => Context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDownContext() => await Context.DropDatabaseAndDispose();

    private int SeasonID;
    private static readonly int[] MondayOnly = [0];
    private static readonly int[] RosterCounts = [4, 3, 1];

    /// <summary>The season's timeslots: Monday, Tuesday and Wednesday at 20:00 (another season's Monday 22:00 is no slot of its).</summary>
    private AvailabilityOption[] _slots = null!;
    private AvailabilityOption _notTheSeasons = null!;
    private Team _team = null!;
    private int _captain;
    private int _player;

    /// <summary>
    /// A team of five: four of them can play Monday, three Tuesday, one Wednesday (the fifth signed up for no timeslot).
    /// </summary>
    private async Task Seed()
    {
        var season = Season.Seed(seasonName: "S1");
        Context.Seasons.Add(season);
        _slots = [AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Monday, time: new TimeOnly(20, 0)), AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Tuesday, time: new TimeOnly(20, 0)),
            AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Wednesday, time: new TimeOnly(20, 0))];
        _notTheSeasons = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Monday, time: new TimeOnly(22, 0));
        Context.Availability.AddRange([.. _slots, _notTheSeasons]);
        var users = Enumerable.Range(1, 5).Select(i => User.Register($"player{i}")).ToList();
        Context.Users.AddRange(users);
        await Context.SaveChangesAsync();
        SeasonID = season.SeasonID;
        Context.AddRange(_slots.Select(o => SeasonAvailability.Seed(SeasonID, o.AvailabilityOptionID)));

        _captain = users[0].Id;
        _player = users[1].Id;
        _team = Team.Drafted(SeasonID, "Reds", _captain, 1);
        foreach (var user in users.Skip(1))
            _team.Draft(user.Id);
        Context.Teams.Add(_team);
        // A captain only once the roster is saved (each points at the other)
        await Context.SaveChangesAsync();
        _team.SetCaptain(_captain);

        int[][] ticked = [[0, 1, 2], [0, 1], [0, 1], [0], []];
        Context.SeasonSignups.AddRange(users.Select((u, i) => SeasonSignup.Seed(SeasonID, u.Id, timeslots: ticked[i].Select(s => _slots[s].AvailabilityOptionID))));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private IEnumerable<int> Slots(params int[] indexes) => indexes.Select(i => _slots[i].AvailabilityOptionID);

    [Test]
    public async Task FromTheRoster_ASlotIsPlayable_WhenEnoughOfItCanPlay_TheSeasonsRule()
    {
        await Seed();

        var slots = (await new TeamAvailabilityService(Context).ForSeasonAsync(SeasonID))[_team.TeamID];

        using (Assert.EnterMultipleScope())
        {
            Assert.That((slots.SetByCaptain, slots.RosterSize), Is.EqualTo((false, 5)));
            Assert.That(slots.Slots.Select(s => (s.AvailabilityOptionID, s.PlayersAvailable, s.Available)), Is.EqualTo(new[]
            {
                (_slots[0].AvailabilityOptionID, 4, true),
                (_slots[1].AvailabilityOptionID, 3, false),
                (_slots[2].AvailabilityOptionID, 1, false),
            }), "Four make a slot by default; only the season's own slots");
        }
    }

    [Test]
    public async Task FromTheRoster_TheSeasonsRuleDecides_HowManyMakeASlot()
    {
        await Seed();
        await new SeasonSchedulingRulesService(Context).ChangeAsync(SeasonID, new SeasonSchedulingRulesDto
        {
            MinPlayersAvailable = 3, MaxMatchesPerTeamPerNight = 1, MaxMatchesPerTeamPerWeek = 2, MaxConcurrentMatches = null,
        });

        var slots = (await new TeamAvailabilityService(Context).ForSeasonAsync(SeasonID))[_team.TeamID];

        Assert.That(slots.Slots.Where(s => s.Available).Select(s => s.AvailabilityOptionID), Is.EqualTo(Slots(0, 1)));
    }

    [Test]
    public async Task TheCaptain_SetsTheTeamsOwn_InPlaceOfTheRosters_AndResetsBackToIt()
    {
        await Seed();
        var service = new TeamAvailabilityService(Context);

        var set = await service.SetAsync(_team.TeamID, [.. Slots(1, 2)], _captain, staff: false);
        var afterSet = await service.ForTeamAsync(_team.TeamID);
        var reset = await service.ResetAsync(_team.TeamID, _captain, staff: false);
        var afterReset = await service.ForTeamAsync(_team.TeamID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((set, reset), Is.EqualTo(((string?)null, (string?)null)));
            Assert.That(afterSet!.SetByCaptain, Is.True);
            Assert.That(afterSet.Slots.Where(s => s.Available).Select(s => s.AvailabilityOptionID), Is.EqualTo(Slots(1, 2)), "Theirs, though fewer players ticked them");
            Assert.That(afterSet.Slots.Select(s => s.PlayersAvailable), Is.EqualTo(RosterCounts), "The roster's counts still shown");
            Assert.That(afterReset!.SetByCaptain, Is.False);
            Assert.That(afterReset.Slots.Where(s => s.Available).Select(s => s.AvailabilityOptionID), Is.EqualTo(Slots(0)));
        }
    }

    [Test]
    public async Task OnlyTheCaptainOrStaff_SetIt_ToTheSeasonsOwnSlots()
    {
        await Seed();
        var service = new TeamAvailabilityService(Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await service.SetAsync(_team.TeamID, [.. Slots(0)], _player, staff: false), Is.EqualTo(TeamAvailabilityService.NotTheCaptain));
            Assert.That(await service.ResetAsync(_team.TeamID, _player, staff: false), Is.EqualTo(TeamAvailabilityService.NotTheCaptain));
            Assert.That(await service.SetAsync(_team.TeamID, [_notTheSeasons.AvailabilityOptionID], _captain, staff: false), Is.EqualTo(TeamAvailabilityService.NotTheSeasonsSlot));
            Assert.That(await service.SetAsync(_team.TeamID, [], _captain, staff: false), Is.EqualTo(TeamAvailabilityService.NoSlots));
            Assert.That(await service.SetAsync(999, [.. Slots(0)], _captain, staff: false), Is.EqualTo(TeamAvailabilityService.NoSuchTeam));
            Assert.That(await service.ResetAsync(999, _captain, staff: true), Is.EqualTo(TeamAvailabilityService.NoSuchTeam));
            Assert.That(await service.ForTeamAsync(999), Is.Null);
            Assert.That(await service.SetAsync(_team.TeamID, [.. Slots(0)], _player, staff: true), Is.Null, "Staff may");
        }
        Context.ChangeTracker.Clear();
        Assert.That(await Context.Set<TeamAvailability>().Select(a => a.AvailabilityOptionID).ToListAsync(), Is.EqualTo(MondayOnly.Select(i => _slots[i].AvailabilityOptionID)));
    }

    [Test]
    public async Task Rules_AreTheDefaults_UntilChanged_AndChangesAreChecked()
    {
        await Seed();
        var service = new SeasonSchedulingRulesService(Context);

        var defaults = await service.GetAsync(SeasonID);
        var refused = await service.ChangeAsync(SeasonID, new SeasonSchedulingRulesDto { MinPlayersAvailable = 4, MaxMatchesPerTeamPerNight = 3, MaxMatchesPerTeamPerWeek = 2 });
        var changed = await service.ChangeAsync(SeasonID, new SeasonSchedulingRulesDto { MinPlayersAvailable = 3, MaxMatchesPerTeamPerNight = 2, MaxMatchesPerTeamPerWeek = 3, MaxConcurrentMatches = 2 });
        var changedAgain = await service.ChangeAsync(SeasonID, new SeasonSchedulingRulesDto { MinPlayersAvailable = 5, MaxMatchesPerTeamPerNight = 1, MaxMatchesPerTeamPerWeek = 1 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That((defaults.MinPlayersAvailable, defaults.MaxMatchesPerTeamPerNight, defaults.MaxMatchesPerTeamPerWeek, defaults.MaxConcurrentMatches), Is.EqualTo((4, 1, 2, (int?)null)));
            Assert.That(refused, Is.Not.Null.And.Contains("week"));
            Assert.That((changed, changedAgain), Is.EqualTo(((string?)null, (string?)null)));
            Assert.That((await service.GetAsync(SeasonID)).MinPlayersAvailable, Is.EqualTo(5));
            Assert.That(await service.ChangeAsync(999, new SeasonSchedulingRulesDto { MinPlayersAvailable = 4, MaxMatchesPerTeamPerNight = 1, MaxMatchesPerTeamPerWeek = 2 }),
                Is.EqualTo(SeasonSchedulingRulesService.NoSuchSeason));
        }
    }

    [Test]
    public async Task ForSeasonAsync_ASlotNobodyTicked_IsUnavailable_AndATeamOfNoSignups_HasNone()
    {
        await Seed();
        var empty = Team.Seed(teamName: "Empty", seasonID: SeasonID);
        Context.Teams.Add(empty);
        await Context.SaveChangesAsync();

        var slots = (await new TeamAvailabilityService(Context).ForSeasonAsync(SeasonID))[empty.TeamID];

        Assert.That((slots.RosterSize, slots.Slots.Count(s => s.Available)), Is.EqualTo((0, 0)));
    }

    [TestCase(0, 1, 2, null, SeasonSchedulingRulesService.NoPlayers)]
    [TestCase(4, 0, 2, null, SeasonSchedulingRulesService.NoNight)]
    [TestCase(4, 2, 1, null, SeasonSchedulingRulesService.WeekUnderNight)]
    [TestCase(4, 1, 2, 0, SeasonSchedulingRulesService.NoConcurrency)]
    public async Task Rules_EachRefusal_SaysWhy_AndChangesNothing(int players, int night, int week, int? concurrent, string why)
    {
        await Seed();
        var service = new SeasonSchedulingRulesService(Context);

        var refused = await service.ChangeAsync(SeasonID, new SeasonSchedulingRulesDto
        {
            MinPlayersAvailable = players, MaxMatchesPerTeamPerNight = night, MaxMatchesPerTeamPerWeek = week, MaxConcurrentMatches = concurrent,
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(why));
            Assert.That(await Context.SeasonSchedulingRules.AnyAsync(), Is.False, "Nothing saved: still the defaults");
        }
    }
}
