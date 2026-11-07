using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The SeasonDatesInOrder migration settles the seasons its constraint would refuse before adding it (the editor took
/// dates in any order): signups that closed before they opened close as they open, a season that ended before it started
/// ends as it starts, a draft after the start is at the start; a season in order is left alone. Alike on whichever
/// database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonDatesInOrderMigrationTests
{
    private static readonly DateTime Open = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task SeasonsOutOfOrder_AreSettled_BeforeTheConstraint()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_SeasonDatesInOrder"))) - 1]);

        // Dates as the editor could save them before: past the private setters, as the old model's public ones were
        Season Old(string name, DateTime close, DateTime draft, DateTime start, DateTime end)
        {
            var season = new Season { SeasonName = name };
            var entry = _context.Seasons.Add(season);
            entry.Property(s => s.SignupsOpen).CurrentValue = Open;
            entry.Property(s => s.SignupsClose).CurrentValue = close;
            entry.Property(s => s.DraftStart).CurrentValue = draft;
            entry.Property(s => s.SeasonStart).CurrentValue = start;
            entry.Property(s => s.SeasonEnd).CurrentValue = end;
            return season;
        }
        var inOrder = Old("In order", Open.AddDays(14), Open.AddDays(16), Open.AddDays(21), Open.AddDays(90));
        var signups = Old("Signups backwards", Open.AddDays(-1), Open.AddDays(16), Open.AddDays(21), Open.AddDays(90));
        var draft = Old("Draft after the start", Open.AddDays(14), Open.AddDays(30), Open.AddDays(21), Open.AddDays(90));
        var ends = Old("Ends before it starts", Open.AddDays(14), Open.AddDays(16), Open.AddDays(21), Open.AddDays(20));
        await _context.SaveChangesAsync();
        var ids = (inOrder.SeasonID, signups.SeasonID, draft.SeasonID, ends.SeasonID);

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var seasons = await _context.Seasons.AsNoTracking().ToDictionaryAsync(s => s.SeasonID, s => (s.SignupsOpen, s.SignupsClose, s.DraftStart, s.SeasonStart, s.SeasonEnd));
        Assert.Multiple(() =>
        {
            Assert.That(seasons[ids.Item1], Is.EqualTo((Open, Open.AddDays(14), Open.AddDays(16), Open.AddDays(21), Open.AddDays(90))), "Left alone");
            Assert.That(seasons[ids.Item2], Is.EqualTo((Open, Open, Open.AddDays(16), Open.AddDays(21), Open.AddDays(90))), "Signups close as they open");
            Assert.That(seasons[ids.Item3], Is.EqualTo((Open, Open.AddDays(14), Open.AddDays(21), Open.AddDays(21), Open.AddDays(90))), "The draft at the start");
            Assert.That(seasons[ids.Item4], Is.EqualTo((Open, Open.AddDays(14), Open.AddDays(16), Open.AddDays(21), Open.AddDays(21))), "Ends as it starts");
        });
    }
}
