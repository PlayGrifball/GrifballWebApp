using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.LateLeague;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.LateLeague;

namespace GrifballWebApp.Integration.Test;

/// <summary>What the database holds a matchmaking season to (#346).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingSeasonAggregateDatabaseTests
{
    private static readonly string[] ImportedSeasonNames = ["Late League Season 8 (Halo 3)"];

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byQuery, Is.EqualTo(ImportedSeasonNames));
            Assert.That(loaded.Where(s => s.IsImported).Select(s => s.Name), Is.EquivalentTo(byQuery), "The bool and the query are one rule");
            Assert.That(loaded.Count(s => !s.IsImported), Is.EqualTo(1));
            Assert.That(dtos.Where(d => d.IsHistorical).Select(d => d.Name), Is.EquivalentTo(byQuery), "The DTO's flag is the rule too");
            Assert.That(dtos.Single(d => d.IsHistorical).HistoricalSourceUrl, Is.EqualTo("https://sheets/wb"), "Its other fields are projected");
        }
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
