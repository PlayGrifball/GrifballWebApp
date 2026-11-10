using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.LateLeague;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.LateLeague;

namespace GrifballWebApp.Integration.Test;

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

/// <summary>What the database holds a matchmaking season to (#346).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingSeasonAggregateDatabaseTests
{
    private static readonly DateTime Jan1 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task AnImportedSeason_IsSavedAndReadBack()
    {
        var season = MatchmakingSeason.Imported("wb/Stats", "Late League Season 8 (Halo 3)", null, "https://sheets/wb");
        season.DateArchived(Jan1, Jan1.AddDays(30), true, Jan1.AddYears(5));
        _context.MatchmakingSeasons.Add(season);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var loaded = await _context.MatchmakingSeasons.AsNoTracking().SingleAsync();
        Assert.That((loaded.Name, loaded.StartsAt, loaded.EndsAt, loaded.HistoricalSourceID, loaded.HistoricalSourceUrl, loaded.IsImported),
            Is.EqualTo(("Late League Season 8 (Halo 3)", Jan1, Jan1.AddDays(30), "wb/Stats", "https://sheets/wb", true)));
    }

    [Test]
    public async Task ImportedFromArchive_TheQuery_AgreesWithIsImported_AndTheDtosIsHistorical()
    {
        var imported = MatchmakingSeason.Imported("wb/Stats", "Late League Season 8 (Halo 3)", null, "https://sheets/wb");
        imported.DateArchived(Jan1, Jan1.AddDays(30), true, Jan1.AddYears(5));
        _context.MatchmakingSeasons.AddRange(imported, MatchmakingSeason.Seed("Queue run", Jan1, Jan1.AddDays(30)));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var byQuery = await _context.MatchmakingSeasons.AsNoTracking().Where(MatchmakingSeason.ImportedFromArchive).Select(s => s.Name).ToListAsync();
        var loaded = await _context.MatchmakingSeasons.AsNoTracking().ToListAsync();
        var dtos = await _context.MatchmakingSeasons.AsNoTracking().Select(MatchmakingSeasonDto.Of).ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(byQuery, Is.EqualTo(new[] { "Late League Season 8 (Halo 3)" }));
            Assert.That(loaded.Where(s => s.IsImported).Select(s => s.Name), Is.EquivalentTo(byQuery), "The bool and the query are one rule");
            Assert.That(loaded.Count(s => !s.IsImported), Is.EqualTo(1));
            Assert.That(dtos.Where(d => d.IsHistorical).Select(d => d.Name), Is.EquivalentTo(byQuery), "The DTO's flag is the rule too");
            Assert.That(dtos.Single(d => d.IsHistorical).HistoricalSourceUrl, Is.EqualTo("https://sheets/wb"), "Its other fields are projected");
        });
    }

    [Test]
    public async Task TheDatabase_RefusesASeasonThatEndsBeforeItStarts()
    {
        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        await using var ctx = _context.NewContextLike();
        var entry = ctx.MatchmakingSeasons.Add(MatchmakingSeason.Seed("Late League", Jan1, Jan1.AddDays(30)));
        entry.Property(s => s.EndsAt).CurrentValue = Jan1.AddDays(-1);

        await Assert.ThatAsync(() => ctx.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task TheDatabase_RefusesASourceLinkOnASeasonNotTheArchives()
    {
        await using var ctx = _context.NewContextLike();
        var entry = ctx.MatchmakingSeasons.Add(MatchmakingSeason.Seed("Queue run", Jan1, Jan1.AddDays(30)));
        entry.Property(s => s.HistoricalSourceUrl).CurrentValue = "https://sheets/x";

        await Assert.ThatAsync(() => ctx.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }
}
