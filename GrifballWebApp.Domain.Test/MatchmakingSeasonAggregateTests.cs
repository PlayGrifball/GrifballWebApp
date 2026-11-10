using GrifballWebApp.Domain.LateLeague;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A matchmaking season is an aggregate (#346): it begins as the archive's, only the import renames and dates it, and its
/// dates never run backwards.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchmakingSeasonAggregateTests
{
    private static readonly DateTime Jan1 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime YearEnd = Jan1.AddYears(1).AddSeconds(-1);
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static MatchmakingSeason Imported() => MatchmakingSeason.Imported("late-league-season-8-halo-3/Stats", "Late League Season 8 (Halo 3)", 2, "https://sheets/late");

    [Test]
    public void Imported_IsTheArchives_WithItsNameGameAndLink()
    {
        var season = Imported();

        Assert.Multiple(() =>
        {
            Assert.That((season.HistoricalSourceID, season.Name, season.GameVersionID, season.HistoricalSourceUrl),
                Is.EqualTo(("late-league-season-8-halo-3/Stats", "Late League Season 8 (Halo 3)", (int?)2, "https://sheets/late")));
            Assert.That(season.IsImported, Is.True);
            Assert.That(season.MatchedMatches, Is.Empty);
            Assert.That(season.PlayerStats, Is.Empty);
        });
    }

    [TestCase(null, "Late League")]
    [TestCase(" ", "Late League")]
    [TestCase("wb/Stats", null)]
    [TestCase("wb/Stats", " ")]
    public void Imported_NeedsItsKeyAndName(string? source, string? name)
        => Assert.That(() => MatchmakingSeason.Imported(source!, name!, null, null), Throws.InstanceOf<ArgumentException>());

    [Test]
    public void UpdateArchived_RenamesIt_AndSetsItsGameAndLink()
    {
        var season = Imported();

        season.UpdateArchived("Late League Season 8 (Halo 3) corrected", null, null);

        Assert.That((season.Name, season.GameVersionID, season.HistoricalSourceUrl, season.HistoricalSourceID),
            Is.EqualTo(("Late League Season 8 (Halo 3) corrected", (int?)null, (string?)null, "late-league-season-8-halo-3/Stats")));
    }

    [Test]
    public void UpdateArchived_RefusesABlankName()
        => Assert.That(() => Imported().UpdateArchived(" ", null, null), Throws.InstanceOf<ArgumentException>());

    [Test]
    public void ASeasonNotTheArchives_IsRefusedTheImportsChanges()
    {
        var season = MatchmakingSeason.Seed("Queue run", Jan1, YearEnd);

        Assert.Multiple(() =>
        {
            Assert.That(season.IsImported, Is.False);
            Assert.That(() => season.UpdateArchived("Renamed", null, null), Throws.InvalidOperationException);
            Assert.That(() => season.DateArchived(Jan1, YearEnd, true, Now), Throws.InvalidOperationException);
            Assert.That((season.Name, season.StartsAt, season.EndsAt), Is.EqualTo(("Queue run", Jan1, YearEnd)), "Unchanged");
        });
    }

    [Test]
    public void DateArchived_OpensAndClosesIt_OnTheDaysItsGamesWerePlayed()
    {
        var season = Imported();

        season.DateArchived(Jan1.AddDays(10), Jan1.AddDays(40), true, Now);

        Assert.That((season.StartsAt, season.EndsAt), Is.EqualTo((Jan1.AddDays(10), Jan1.AddDays(40))));
    }

    [Test]
    public void DateArchived_Dated_TakesTheNewDays()
    {
        var season = Imported();
        season.DateArchived(Jan1, Jan1.AddDays(40), true, Now);

        season.DateArchived(Jan1, Jan1.AddDays(100), true, Now);

        Assert.That(season.EndsAt, Is.EqualTo(Jan1.AddDays(100)));
    }

    [Test]
    public void DateArchived_Undated_KeepsTheEndAnEarlierRunGave_WithinTheYear()
    {
        var season = Imported();
        season.DateArchived(Jan1, Jan1.AddDays(40), true, Now);

        season.DateArchived(Jan1, YearEnd, false, Now);

        Assert.That((season.StartsAt, season.EndsAt), Is.EqualTo((Jan1, Jan1.AddDays(40))));
    }

    [Test]
    public void DateArchived_Undated_TakesTheYear_WhenTheEarlierEndIsOutsideIt()
    {
        var season = Imported();
        season.DateArchived(Jan1.AddYears(-1), Jan1.AddDays(-1), true, Now);

        season.DateArchived(Jan1, YearEnd, false, Now);

        Assert.That((season.StartsAt, season.EndsAt), Is.EqualTo((Jan1, YearEnd)));
    }

    [Test]
    public void DateArchived_Undated_TakesTheYear_WhenTheEarlierEndIsAfterIt()
    {
        var season = Imported();
        season.DateArchived(Jan1, YearEnd.AddDays(20), true, Now);

        season.DateArchived(Jan1, YearEnd, false, Now);

        Assert.That((season.StartsAt, season.EndsAt), Is.EqualTo((Jan1, YearEnd)));
    }

    [Test]
    public void DateArchived_EndsByNow_AndAStartPastNowIsNow()
    {
        var thisYear = Imported();
        var future = Imported();

        thisYear.DateArchived(Now.AddDays(-10), Now.AddDays(30), true, Now);
        future.DateArchived(Now.AddDays(5), Now.AddDays(30), true, Now);

        Assert.Multiple(() =>
        {
            Assert.That((thisYear.StartsAt, thisYear.EndsAt), Is.EqualTo((Now.AddDays(-10), Now)), "Played by now");
            Assert.That((future.StartsAt, future.EndsAt), Is.EqualTo((Now, Now)), "A sheet's date past now is its mistake: never backwards");
        });
    }

    [Test]
    public void DateArchived_RefusesDatesThatRunBackwards()
    {
        var season = Imported();
        season.DateArchived(Jan1, YearEnd, true, Now);

        Assert.That(() => season.DateArchived(YearEnd, Jan1, true, Now), Throws.ArgumentException);
        Assert.That((season.StartsAt, season.EndsAt), Is.EqualTo((Jan1, YearEnd)), "Unchanged");
    }

    [Test]
    public void Seed_RefusesDatesThatRunBackwards()
        => Assert.That(() => MatchmakingSeason.Seed("Late League", YearEnd, Jan1), Throws.ArgumentException);
}
