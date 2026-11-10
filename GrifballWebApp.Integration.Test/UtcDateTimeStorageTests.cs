using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>Through the database: what's saved comes back UTC, and a local time isn't saved.</summary>
[TestFixture]
public class UtcDateTimeStorageTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task SavedDateTimes_ComeBackUtc()
    {
        var open = new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Unspecified);
        _context.Seasons.Add(GrifballWebApp.Domain.League.Season.Seed(seasonName: "UTC").WithDates(signupsOpen: open, signupsClose: DateTime.UtcNow));
        await _context.SaveChangesAsync();

        var season = await _context.NewContextLike().Seasons.SingleAsync(s => s.SeasonName == "UTC");

        Assert.Multiple(() =>
        {
            Assert.That(season.SignupsOpen, Is.EqualTo(open));
            Assert.That(season.SignupsOpen.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(season.SignupsClose.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public async Task LocalDateTime_IsNotSaved()
    {
        _context.Seasons.Add(GrifballWebApp.Domain.League.Season.Seed(seasonName: "Local").WithDates(signupsOpen: DateTime.Now.AddDays(-1), signupsClose: DateTime.UtcNow));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());

        Assert.That(ex!.InnerException, Is.InstanceOf<InvalidOperationException>().With.Message.Contains("Kind Local"));
    }
}
