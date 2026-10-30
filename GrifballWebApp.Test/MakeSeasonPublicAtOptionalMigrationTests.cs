using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>The MakeSeasonPublicAtOptional migration keeps the seasons there are public, on whichever database the suite runs on.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MakeSeasonPublicAtOptionalMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task SeasonsSavedBefore_StayPublic_FromWhenTheyWereMade()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var index = migrations.IndexOf(migrations.Single(m => m.EndsWith("_MakeSeasonPublicAtOptional")));
        await migrator.MigrateAsync(migrations[index - 1]);

        var created = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var announced = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        _context.Seasons.AddRange(
            new Season { SeasonName = "Never set", PublicAt = DateTime.MinValue, CreatedAt = created },
            new Season { SeasonName = "No creation time", PublicAt = DateTime.MinValue },
            new Season { SeasonName = "Set", PublicAt = announced, CreatedAt = created });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var seasons = await _context.Seasons.AsNoTracking().ToDictionaryAsync(s => s.SeasonName, s => s.PublicAt);
        Assert.Multiple(() =>
        {
            Assert.That(seasons["Never set"], Is.EqualTo(created));
            Assert.That(seasons["No creation time"], Is.EqualTo(DateTime.MinValue), "Still public");
            Assert.That(seasons["Set"], Is.EqualTo(announced));
        });
    }
}
