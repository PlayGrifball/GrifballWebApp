using System.Linq.Expressions;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using static GrifballWebApp.Domain.Test.SeasonPhaseTestData;

namespace GrifballWebApp.Integration.Test;

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
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused(e => e.Property(s => s.SignupsClose).CurrentValue = Open.AddTicks(-10)), Is.True, "Signups close before they open");
            Assert.That(await Refused(e => e.Property(s => s.DraftStart).CurrentValue = Start.AddDays(1)), Is.True, "A draft after the start");
            Assert.That(await Refused(e => e.Property(s => s.SeasonEnd).CurrentValue = Start.AddDays(-1)), Is.True, "Ends before it starts");
            Assert.That(await Refused(e => e.Property(s => s.SignupsClose).CurrentValue = End.AddDays(1)), Is.False, "Late signups are fine");
        }
    }
}
