using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A season's schedule checked and suggested (<see cref="SchedulingService"/>): a match's time against the rules
/// (<see cref="ScheduleChecker"/>), and the season's timeslots ranked for a match, best first.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SchedulingServiceUnitTests
{
    // A database of its own (the real providers: SQLite with the app's query rewrites, SQL Server, Postgres)
    private GrifballContext Context = null!;

    [SetUp]
    public async Task SetUpContext() => Context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDownContext() => await Context.DropDatabaseAndDispose();

    private static readonly DateTime Monday = SchedulingTestData.Monday, Tuesday = SchedulingTestData.Tuesday;

    private readonly FakeTimeProvider _time = new(SchedulingTestData.Now);
    private SeasonMatch _match = null!;

    /// <summary>The season of <see cref="SchedulingTestData"/>; Reds vs Blues the match checked.</summary>
    private async Task Seed() => _match = (await SchedulingTestData.SeedAsync(Context)).Match;

    private SchedulingService Service() => new(Context, _time);

    private static DateTime[] Times(params DateTime[] times) => times;

    private static ScheduleConflictKind[] Kinds(params ScheduleConflictKind[] kinds) => kinds;

    [Test]
    public async Task CheckAsync_ATimeAtWhichBlueAlreadyPlays_IsRefused_AndAFreeOneIsFine()
    {
        await Seed();

        var clash = (await Service().CheckAsync(_match.SeasonMatchID, Tuesday))!;
        var free = await Service().CheckAsync(_match.SeasonMatchID, Monday);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clash.Select(c => c.Kind), Does.Contain(ScheduleConflictKind.TeamPlaysThen));
            Assert.That(clash.Any(c => c.Refuses), Is.True);
            Assert.That(free, Is.Empty);
            Assert.That(await Service().CheckAsync(999, Monday), Is.Null, "No such match");
        }
    }

    [Test]
    public async Task SuggestAsync_TheSeasonsSlotsFromNow_Best_First_TheRefusedLeftOut()
    {
        await Seed();

        var suggestions = (await Service().SuggestAsync(_match.SeasonMatchID))!;

        using (Assert.EnterMultipleScope())
        {
            // Mondays first (everyone can play), soonest first; the first Tuesday is refused (Blues play); the second Tuesday last (Reds short)
            Assert.That(suggestions.Select(s => s.StartsAt), Is.EqualTo(Times(Monday, Monday.AddDays(7), Tuesday.AddDays(7))));
            Assert.That(suggestions[0].Conflicts, Is.Empty);
            Assert.That((suggestions[0].HomePlayersAvailable, suggestions[0].AwayPlayersAvailable), Is.EqualTo((4, 4)));
            Assert.That(suggestions[2].Conflicts.Select(c => c.Kind), Is.EqualTo(Kinds(ScheduleConflictKind.HomeTeamUnavailable)), "Two of Reds' four: short of the season's four");
            Assert.That(suggestions[2].HomePlayersAvailable, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task SuggestAsync_FromAndTo_AndHowMany_NarrowIt_AndThePastIsLeftOut()
    {
        await Seed();
        _time.SetUtcNow(Monday.AddHours(1));

        var later = await Service().SuggestAsync(_match.SeasonMatchID, count: 1);
        var window = await Service().SuggestAsync(_match.SeasonMatchID, from: Monday.AddDays(6), to: Monday.AddDays(8));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(later!.Select(s => s.StartsAt), Is.EqualTo(Times(Monday.AddDays(7))), "This Monday's has begun");
            Assert.That(window!.Select(s => s.StartsAt), Is.EqualTo(Times(Monday.AddDays(7))));
            Assert.That(await Service().SuggestAsync(999), Is.Null);
        }
    }

    [Test]
    public async Task LoadAsync_NoSuchSeason_IsNone()
        => Assert.That(await Service().LoadAsync(999_999), Is.Null);
}
