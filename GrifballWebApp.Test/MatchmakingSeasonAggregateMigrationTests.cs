using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The MatchmakingSeasonAggregate migration settles the matchmaking seasons its constraints would refuse before adding
/// them: an imported season the old import dated after its import (its start past the import, its end cut back to it)
/// starts at that end, as DateArchived dates one now; any other season that ends before it starts ends as it starts; a
/// source link on a season not the archive's goes. A season in order is left alone. Alike on whichever database the suite
/// runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingSeasonAggregateMigrationTests
{
    private static readonly DateTime Jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task SeasonsTheConstraintsWouldRefuse_AreSettled_BeforeThem()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_MatchmakingSeasonAggregate"))) - 1]);

        // As the old model's public setters could leave them: past the private setters
        MatchmakingSeason Old(string name, DateTime start, DateTime end, string? source = null, string? url = null)
        {
            var season = MatchmakingSeason.Seed(name, start, start, historicalSourceID: source);
            var entry = _context.MatchmakingSeasons.Add(season);
            entry.Property(s => s.EndsAt).CurrentValue = end;
            entry.Property(s => s.HistoricalSourceUrl).CurrentValue = url;
            return season;
        }
        var inOrder = Old("In order", Jan1, Jan1.AddDays(30), "a/Stats", "https://sheets/a");
        var importedAt = Jan1.AddDays(100);
        var importedEarly = Old("Imported before its first game", Jan1.AddDays(200), importedAt, "b/Stats");
        var backwards = Old("Ends before it starts", Jan1.AddDays(30), Jan1.AddDays(10));
        var strayLink = Old("Stray link", Jan1, Jan1.AddDays(30), url: "https://sheets/stray");
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var seasons = await _context.MatchmakingSeasons.AsNoTracking()
            .ToDictionaryAsync(s => s.MatchmakingSeasonID, s => (s.StartsAt, s.EndsAt, s.HistoricalSourceUrl));
        Assert.Multiple(() =>
        {
            Assert.That(seasons[inOrder.MatchmakingSeasonID], Is.EqualTo((Jan1, Jan1.AddDays(30), (string?)"https://sheets/a")), "Left alone");
            Assert.That(seasons[importedEarly.MatchmakingSeasonID], Is.EqualTo((importedAt, importedAt, (string?)null)), "Starts at the end it was cut back to");
            Assert.That(seasons[backwards.MatchmakingSeasonID], Is.EqualTo((Jan1.AddDays(30), Jan1.AddDays(30), (string?)null)), "Ends as it starts");
            Assert.That(seasons[strayLink.MatchmakingSeasonID], Is.EqualTo((Jan1, Jan1.AddDays(30), (string?)null)), "The stray link goes");
        });
    }
}
