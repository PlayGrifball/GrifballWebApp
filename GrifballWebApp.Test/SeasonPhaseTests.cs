using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// A season's phase, worked out from its dates in one place (Season.PhaseAt, the bools and the query expressions), with
/// one boundary rule: each phase runs from its date, included, to the next, not included. And the dates' order, which
/// Season.Schedule keeps and CK_Event_Seasons_DatesInOrder holds the database to.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonPhaseTests
{
    private static readonly DateTime Open = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Close = Open.AddDays(14);
    private static readonly DateTime Draft = Open.AddDays(16);
    private static readonly DateTime Start = Open.AddDays(21);
    private static readonly DateTime End = Open.AddDays(90);

    private static Season Scheduled(string name = "Spring 2026") => new Season { SeasonName = name }
        .WithDates(signupsOpen: Open, signupsClose: Close, draftStart: Draft, seasonStart: Start, seasonEnd: End);

    /// <summary>Every date and a millisecond either side of it (what every database keeps): where the boundary rules differ.</summary>
    private static IEnumerable<DateTime> AroundEachDate()
        => new[] { Open, Close, Draft, Start, End }.SelectMany(d => new[] { d.AddMilliseconds(-1), d, d.AddMilliseconds(1) });

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void PhaseAt_EachPhaseRunsFromItsDate_UpToTheNext()
    {
        var season = Scheduled();
        Assert.Multiple(() =>
        {
            Assert.That(season.PhaseAt(Open.AddTicks(-1)), Is.EqualTo(SeasonPhase.Upcoming));
            Assert.That(season.PhaseAt(Open), Is.EqualTo(SeasonPhase.SignupsOpen));
            Assert.That(season.PhaseAt(Close.AddTicks(-1)), Is.EqualTo(SeasonPhase.SignupsOpen));
            Assert.That(season.PhaseAt(Close), Is.EqualTo(SeasonPhase.Drafting), "Signups closed: the draft is next");
            Assert.That(season.PhaseAt(Draft), Is.EqualTo(SeasonPhase.Drafting));
            Assert.That(season.PhaseAt(Start.AddTicks(-1)), Is.EqualTo(SeasonPhase.Drafting));
            Assert.That(season.PhaseAt(Start), Is.EqualTo(SeasonPhase.Running));
            Assert.That(season.PhaseAt(End.AddTicks(-1)), Is.EqualTo(SeasonPhase.Running));
            Assert.That(season.PhaseAt(End), Is.EqualTo(SeasonPhase.Ended), "Over at its end exactly");
            Assert.That(season.SignupsAreOpenAt(Close), Is.False, "Closed at the close exactly");
            Assert.That((season.IsRunningAt(Start), season.IsRunningAt(End), season.HasEndedAt(End), season.HasEndedAt(End.AddTicks(-1))),
                Is.EqualTo((true, false, true, false)));
        });
    }

    [Test]
    public void PhaseAt_LateSignups_TheLaterPhase_AndSignupsStillOpen()
    {
        // Signups open through the draft and into the season (the seeder's season)
        var season = new Season { SeasonName = "Late" }.WithDates(signupsOpen: Open, signupsClose: End, draftStart: Draft, seasonStart: Start, seasonEnd: End);
        Assert.Multiple(() =>
        {
            Assert.That(season.PhaseAt(Draft.AddTicks(-1)), Is.EqualTo(SeasonPhase.SignupsOpen));
            Assert.That(season.PhaseAt(Draft), Is.EqualTo(SeasonPhase.Drafting));
            Assert.That(season.PhaseAt(Start), Is.EqualTo(SeasonPhase.Running));
            Assert.That(season.SignupsAreOpenAt(Draft) && season.SignupsAreOpenAt(Start), Is.True);
        });
    }

    [Test]
    public void PhaseAt_AnImportedSeason_NeverHasSignupsOpen()
    {
        var season = new Season { SeasonName = "Summer 2019", HistoricalSourceID = "wb/sheet" };
        season.ScheduleArchived(Start, End);
        Assert.Multiple(() =>
        {
            Assert.That(season.PhaseAt(Start.AddTicks(-1)), Is.EqualTo(SeasonPhase.Upcoming));
            Assert.That(season.PhaseAt(Start), Is.EqualTo(SeasonPhase.Running));
            Assert.That(AroundEachDate().Any(season.SignupsAreOpenAt), Is.False, "Its signups close as they open");
            Assert.That(season.PublicAt, Is.EqualTo(Start), "Public since it began");
        });
    }

    [Test]
    public void IsPublicAsOf_OnceStarted_OrOnceAnnounced()
    {
        var season = Scheduled();
        var announced = Scheduled();
        announced.AnnounceAt(Open);
        Assert.Multiple(() =>
        {
            Assert.That((season.IsPublicAsOf(Start.AddTicks(-1)), season.IsPublicAsOf(Start)), Is.EqualTo((false, true)), "Not announced: public once it starts");
            Assert.That((announced.IsPublicAsOf(Open.AddTicks(-1)), announced.IsPublicAsOf(Open)), Is.EqualTo((false, true)));
        });
    }

    /// <summary>Each query expression and the bool it stands for, which must agree.</summary>
    private static IEnumerable<(string Name, Func<DateTime, Expression<Func<Season, bool>>> Query, Func<Season, DateTime, bool> Loaded)> Rules() =>
    [
        (nameof(Season.SignupsOpenAt), Season.SignupsOpenAt, (s, now) => s.SignupsAreOpenAt(now)),
        (nameof(Season.SignupsNotClosedAt), Season.SignupsNotClosedAt, (s, now) => s.PhaseAt(now) is SeasonPhase.Upcoming or SeasonPhase.SignupsOpen),
        (nameof(Season.NotStartedAt), Season.NotStartedAt, (s, now) => s.PhaseAt(now) < SeasonPhase.Running),
        (nameof(Season.StartedAt), Season.StartedAt, (s, now) => s.PhaseAt(now) >= SeasonPhase.Running),
        (nameof(Season.RunningAt), Season.RunningAt, (s, now) => s.IsRunningAt(now)),
        (nameof(Season.EndedAt), Season.EndedAt, (s, now) => s.HasEndedAt(now)),
        (nameof(Season.NotEndedAt), Season.NotEndedAt, (s, now) => !s.HasEndedAt(now)),
        (nameof(Season.AnnouncedAt), Season.AnnouncedAt, (s, now) => s.PublicAt <= now),
        (nameof(Season.PublicAsOf), Season.PublicAsOf, (s, now) => s.IsPublicAsOf(now)),
    ];

    [Test]
    public async Task TheQueryExpressions_AgreeWithTheLoadedSeasonsRules_OnTheDatabaseToo()
    {
        var season = Scheduled();
        season.AnnounceAt(Open.AddDays(-7));
        var unannounced = Scheduled("Unannounced");
        _context.Seasons.AddRange(season, unannounced);
        await _context.SaveChangesAsync();

        var disagreements = new List<string>();
        foreach (var (name, query, loaded) in Rules())
            foreach (var now in AroundEachDate().Append(Open.AddDays(-7)))
            {
                var found = await _context.Seasons.AsNoTracking().Where(query(now)).Select(s => s.SeasonID).ToListAsync();
                foreach (var s in new[] { season, unannounced })
                {
                    var expected = loaded(s, now);
                    if (found.Contains(s.SeasonID) != expected || query(now).Compile()(s) != expected)
                        disagreements.Add($"{name} at {now:o} for {s.SeasonName}: expected {expected}");
                }
            }

        Assert.That(disagreements, Is.Empty);
    }

    [Test]
    public void Schedule_RefusesAnOrderThatCantHappen_AndLeavesTheDates()
    {
        var season = Scheduled();
        Assert.Multiple(() =>
        {
            Assert.That(season.Schedule(Close, Open, Draft, Start, End), Is.EqualTo(SeasonScheduleResult.SignupsCloseBeforeTheyOpen));
            Assert.That(season.Schedule(Open, Close, Start.AddTicks(1), Start, End), Is.EqualTo(SeasonScheduleResult.DraftAfterSeasonStart));
            Assert.That(season.Schedule(Open, Close, Draft, End, Start), Is.EqualTo(SeasonScheduleResult.EndsBeforeItStarts));
            Assert.That((season.SignupsOpen, season.SignupsClose, season.DraftStart, season.SeasonStart, season.SeasonEnd), Is.EqualTo((Open, Close, Draft, Start, End)));
            // Equal dates are fine, and signups may close after the draft or the start (late signups)
            Assert.That(season.Schedule(Open, Open, Start, Start, Start), Is.EqualTo(SeasonScheduleResult.Scheduled));
            Assert.That(season.Schedule(Open, End, Draft, Start, End), Is.EqualTo(SeasonScheduleResult.Scheduled));
            Assert.That(season.SignupsClose, Is.EqualTo(End));
        });
    }

    [Test]
    public void ScheduleArchived_OnlyAnImportedSeason_AndInOrder()
    {
        var imported = new Season { SeasonName = "Summer 2019", HistoricalSourceID = "wb/sheet" };
        Assert.Multiple(() =>
        {
            Assert.That(() => Scheduled().ScheduleArchived(Start, End), Throws.InvalidOperationException.With.Message.Contains("not an imported season"));
            Assert.That(() => imported.ScheduleArchived(End, Start), Throws.InvalidOperationException.With.Message.Contains(nameof(SeasonScheduleResult.EndsBeforeItStarts)));
            Assert.That(imported.PublicAt, Is.Null, "Refused: unchanged");
        });
    }

    [Test]
    public async Task TheDatabase_RefusesDatesOutOfOrder()
    {
        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Season>> corrupt)
        {
            await using var ctx = _context.NewContextLike();
            corrupt(ctx.Seasons.Add(Scheduled()));
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(e => e.Property(s => s.SignupsClose).CurrentValue = Open.AddTicks(-10)), Is.True, "Signups close before they open");
            Assert.That(await Refused(e => e.Property(s => s.DraftStart).CurrentValue = Start.AddDays(1)), Is.True, "A draft after the start");
            Assert.That(await Refused(e => e.Property(s => s.SeasonEnd).CurrentValue = Start.AddDays(-1)), Is.True, "Ends before it starts");
            Assert.That(await Refused(e => e.Property(s => s.SignupsClose).CurrentValue = End.AddDays(1)), Is.False, "Late signups are fine");
        });
    }
}
