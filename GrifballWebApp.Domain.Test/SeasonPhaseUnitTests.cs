using GrifballWebApp.Domain.League;
using static GrifballWebApp.Domain.Test.SeasonPhaseTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>SeasonPhaseTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonPhaseUnitTests
{
    private static readonly DateTime Later = End.AddYears(1);

    [Test]
    public void PhaseAt_EachPhaseRunsFromItsDate_UpToTheNext()
    {
        var season = Scheduled();
        using (Assert.EnterMultipleScope())
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
        }
    }

    [Test]
    public void PhaseAt_LateSignups_TheLaterPhase_AndSignupsStillOpen()
    {
        // Signups open through the draft and into the season (the seeder's season)
        var season = Season.Seed(seasonName: "Late").WithDates(signupsOpen: Open, signupsClose: End, draftStart: Draft, seasonStart: Start, seasonEnd: End);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.PhaseAt(Draft.AddTicks(-1)), Is.EqualTo(SeasonPhase.SignupsOpen));
            Assert.That(season.PhaseAt(Draft), Is.EqualTo(SeasonPhase.Drafting));
            Assert.That(season.PhaseAt(Start), Is.EqualTo(SeasonPhase.Running));
            Assert.That(season.SignupsAreOpenAt(Draft) && season.SignupsAreOpenAt(Start), Is.True);
        }
    }

    [Test]
    public void PhaseAt_SignupsOpenAfterTheDraftStarts_DraftingFromTheDraft()
    {
        // Schedule and the constraint allow signups that open after the draft: the latest date passed decides
        var opens = Draft.AddDays(1);
        var season = Season.Seed(seasonName: "Signups after the draft").WithDates(signupsOpen: opens, signupsClose: End, draftStart: Draft, seasonStart: Start, seasonEnd: End);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.PhaseAt(Draft.AddTicks(-1)), Is.EqualTo(SeasonPhase.Upcoming));
            Assert.That(season.PhaseAt(Draft), Is.EqualTo(SeasonPhase.Drafting), "The draft has started, signups not yet open");
            Assert.That(season.PhaseAt(opens.AddTicks(-1)), Is.EqualTo(SeasonPhase.Drafting));
            Assert.That(season.PhaseAt(opens), Is.EqualTo(SeasonPhase.Drafting), "Signups open during the draft: still drafting");
            Assert.That(season.PhaseAt(Start), Is.EqualTo(SeasonPhase.Running));
            Assert.That((season.SignupsAreOpenAt(Draft), season.SignupsAreOpenAt(opens), season.SignupsAreOpenAt(Start)), Is.EqualTo((false, true, true)));
        }
    }

    [Test]
    public void PhaseAt_AnImportedSeason_NeverHasSignupsOpen()
    {
        var season = Season.Seed(seasonName: "Summer 2019", historicalSourceID: "wb/sheet");
        season.ScheduleArchived(Start, End, true, Later);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.PhaseAt(Start.AddTicks(-1)), Is.EqualTo(SeasonPhase.Upcoming));
            Assert.That(season.PhaseAt(Start), Is.EqualTo(SeasonPhase.Running));
            Assert.That(AroundEachDate().Any(season.SignupsAreOpenAt), Is.False, "Its signups close as they open");
            Assert.That(season.PublicAt, Is.EqualTo(Start), "Public since it began");
        }
    }

    [Test]
    public void IsPublicAsOf_OnceStarted_OrOnceAnnounced()
    {
        var season = Scheduled();
        var announced = Scheduled();
        announced.AnnounceAt(Open);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((season.IsPublicAsOf(Start.AddTicks(-1)), season.IsPublicAsOf(Start)), Is.EqualTo((false, true)), "Not announced: public once it starts");
            Assert.That((announced.IsPublicAsOf(Open.AddTicks(-1)), announced.IsPublicAsOf(Open)), Is.EqualTo((false, true)));
        }
    }

    [Test]
    public void Schedule_RefusesAnOrderThatCantHappen_AndLeavesTheDates()
    {
        var season = Scheduled();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.Schedule(Close, Open, Draft, Start, End), Is.EqualTo(SeasonScheduleResult.SignupsCloseBeforeTheyOpen));
            Assert.That(season.Schedule(Open, Close, Start.AddTicks(1), Start, End), Is.EqualTo(SeasonScheduleResult.DraftAfterSeasonStart));
            Assert.That(season.Schedule(Open, Close, Draft, End, Start), Is.EqualTo(SeasonScheduleResult.EndsBeforeItStarts));
            Assert.That((season.SignupsOpen, season.SignupsClose, season.DraftStart, season.SeasonStart, season.SeasonEnd), Is.EqualTo((Open, Close, Draft, Start, End)));
            // Equal dates are fine, and signups may close after the draft or the start (late signups)
            Assert.That(season.Schedule(Open, Open, Start, Start, Start), Is.EqualTo(SeasonScheduleResult.Scheduled));
            Assert.That(season.Schedule(Open, End, Draft, Start, End), Is.EqualTo(SeasonScheduleResult.Scheduled));
            Assert.That(season.SignupsClose, Is.EqualTo(End));
        }
    }

    [Test]
    public void Schedule_RefusesAnImportedSeason_ScheduleArchivedDatesIt()
    {
        var imported = Season.Seed(seasonName: "Summer 2019", historicalSourceID: "wb/sheet");
        imported.ScheduleArchived(Start, End, true, Later);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(imported.Schedule(Open, Close, Draft, Start, End), Is.EqualTo(SeasonScheduleResult.Imported));
            Assert.That(imported.Schedule(Close, Open, Draft, Start, End), Is.EqualTo(SeasonScheduleResult.Imported), "Imported decides before the order");
            Assert.That((imported.SignupsOpen, imported.SignupsClose, imported.DraftStart, imported.SeasonStart, imported.SeasonEnd), Is.EqualTo((Start, Start, Start, Start, End)), "Refused: unchanged");
        }
        imported.ScheduleArchived(Open, End, true, Later);
        Assert.That((imported.SignupsOpen, imported.SeasonStart, imported.SeasonEnd, imported.PublicAt), Is.EqualTo((Open, Open, End, (DateTime?)Open)), "The import re-dates it");
    }

    [Test]
    public void ScheduleArchived_OnlyAnImportedSeason_AndInOrder()
    {
        var imported = Season.Seed(seasonName: "Summer 2019", historicalSourceID: "wb/sheet");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => Scheduled().ScheduleArchived(Start, End, true, Later), Throws.InvalidOperationException.With.Message.Contains("not an imported season"));
            Assert.That(() => imported.ScheduleArchived(End, Start, true, Later), Throws.InvalidOperationException.With.Message.Contains(nameof(SeasonScheduleResult.EndsBeforeItStarts)));
            Assert.That(imported.PublicAt, Is.Null, "Refused: unchanged");
        }
    }

    private static readonly DateTime YearStart = new(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime YearEnd = YearStart.AddYears(1).AddSeconds(-1);

    [Test]
    public void ScheduleArchived_Undated_KeepsTheEndAnEarlierRunGave_WithinTheYear()
    {
        var imported = Season.Seed(seasonName: "Summer 2019", historicalSourceID: "wb/sheet");
        imported.ScheduleArchived(YearStart, YearStart.AddDays(40), true, Later);

        imported.ScheduleArchived(YearStart, YearEnd, false, Later);

        Assert.That((imported.SeasonStart, imported.SeasonEnd), Is.EqualTo((YearStart, YearStart.AddDays(40))));
    }

    [TestCase(-400, -1, TestName = "ScheduleArchived_Undated_TakesTheYear_WhenTheEarlierEndIsBeforeIt")]
    [TestCase(0, 400, TestName = "ScheduleArchived_Undated_TakesTheYear_WhenTheEarlierEndIsAfterIt")]
    public void ScheduleArchived_Undated_TakesTheYear_WhenTheEarlierEndIsOutsideIt(int startDays, int endDays)
    {
        var imported = Season.Seed(seasonName: "Summer 2019", historicalSourceID: "wb/sheet");
        imported.ScheduleArchived(YearStart.AddDays(startDays), YearStart.AddDays(endDays), true, Later);

        imported.ScheduleArchived(YearStart, YearEnd, false, Later);

        Assert.That((imported.SeasonStart, imported.SeasonEnd), Is.EqualTo((YearStart, YearEnd)));
    }

    [Test]
    public void ScheduleArchived_Dated_TakesTheNewDates()
    {
        var imported = Season.Seed(seasonName: "Summer 2019", historicalSourceID: "wb/sheet");
        imported.ScheduleArchived(YearStart, YearStart.AddDays(40), true, Later);

        imported.ScheduleArchived(YearStart, YearStart.AddDays(100), true, Later);

        Assert.That(imported.SeasonEnd, Is.EqualTo(YearStart.AddDays(100)));
    }

    [Test]
    public void ScheduleArchived_EndsByNow_AndAStartPastNowIsNow()
    {
        var now = Start.AddDays(10);
        var running = Season.Seed(seasonName: "Summer 2026", historicalSourceID: "wb/a");
        var future = Season.Seed(seasonName: "Fall 2026", historicalSourceID: "wb/b");

        running.ScheduleArchived(Start, End, true, now);
        future.ScheduleArchived(now.AddDays(5), End, true, now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((running.SeasonStart, running.SeasonEnd, running.PublicAt), Is.EqualTo((Start, now, (DateTime?)Start)), "Played by now");
            Assert.That((future.SignupsOpen, future.SeasonStart, future.SeasonEnd, future.PublicAt), Is.EqualTo((now, now, now, (DateTime?)now)),
                "A sheet's date past now is its mistake: never backwards");
            Assert.That(future.HasEndedAt(now), Is.True);
        }
    }
}

/// <summary>SeasonPhaseTests' helpers, shared with its integration tests.</summary>
internal static class SeasonPhaseTestData
{
    internal static readonly DateTime Open = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    internal static readonly DateTime Close = Open.AddDays(14);
    internal static readonly DateTime Draft = Open.AddDays(16);
    internal static readonly DateTime Start = Open.AddDays(21);
    internal static readonly DateTime End = Open.AddDays(90);

    internal static Season Scheduled(string name = "Spring 2026") => Season.Seed(seasonName: name)
        .WithDates(signupsOpen: Open, signupsClose: Close, draftStart: Draft, seasonStart: Start, seasonEnd: End);

    /// <summary>Every date and a millisecond either side of it (what every database keeps): where the boundary rules differ.</summary>
    internal static IEnumerable<DateTime> AroundEachDate()
        => new[] { Open, Close, Draft, Start, End }.SelectMany(d => new[] { d.AddMilliseconds(-1), d, d.AddMilliseconds(1) });
}
