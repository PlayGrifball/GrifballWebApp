using GrifballWebApp.Database;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The SeasonAggregate migration settles the seasons its constraint (CK_Event_Seasons_SourceUrlOnlyImported) would refuse
/// before adding it: a source link on a season of the app's goes, an imported season's stays. Alike on whichever database
/// the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonAggregateMigrationTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task ASourceLinkOnAnAppSeason_IsSettled_BeforeTheConstraint()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_SeasonAggregate"))) - 1]);

        // As the old model's public setters could leave it
        var app = Season.Seed(seasonName: "App season", historicalSourceUrl: "https://sheets/stray");
        var imported = Season.Seed(seasonName: "Imported season", historicalSourceID: "wb/Stats", historicalSourceUrl: "https://sheets/wb");
        _context.Seasons.AddRange(app, imported);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var links = await _context.Seasons.AsNoTracking().ToDictionaryAsync(s => s.SeasonID, s => s.HistoricalSourceUrl);
        Assert.Multiple(() =>
        {
            Assert.That(links[app.SeasonID], Is.Null, "The app season's stray link goes");
            Assert.That(links[imported.SeasonID], Is.EqualTo("https://sheets/wb"), "The imported season keeps its own");
        });
    }
}
