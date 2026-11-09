using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
public class UtcDateTimeConverterTests
{
    private static readonly UtcDateTimeConverter Converter = new();

    private static DateTime Write(DateTime value) => (DateTime)Converter.ConvertToProvider(value)!;
    private static DateTime Read(DateTime value) => (DateTime)Converter.ConvertFromProvider(value)!;

    [Test]
    public void Write_Utc_IsKept()
    {
        var value = new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Utc);

        var stored = Write(value);

        Assert.Multiple(() =>
        {
            Assert.That(stored.Ticks, Is.EqualTo(value.Ticks));
            Assert.That(stored.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Write_Unspecified_IsTakenAsUtc_WithoutShifting()
    {
        var value = new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Unspecified);

        var stored = Write(value);

        Assert.Multiple(() =>
        {
            Assert.That(stored.Ticks, Is.EqualTo(value.Ticks));
            Assert.That(stored.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Write_Local_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Write(new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Local)));

        Assert.That(ex!.Message, Does.Contain("Kind Local"));
    }

    [TestCase(DateTimeKind.Unspecified)]
    [TestCase(DateTimeKind.Utc)]
    public void Read_IsUtc(DateTimeKind kind)
    {
        var value = new DateTime(2026, 10, 11, 12, 30, 0, kind);

        var read = Read(value);

        Assert.Multiple(() =>
        {
            Assert.That(read.Ticks, Is.EqualTo(value.Ticks));
            Assert.That(read.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }
}

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
        _context.Seasons.Add(GrifballWebApp.Domain.Models.Season.Seed(seasonName: "UTC").WithDates(signupsOpen: open, signupsClose: DateTime.UtcNow));
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
        _context.Seasons.Add(GrifballWebApp.Domain.Models.Season.Seed(seasonName: "Local").WithDates(signupsOpen: DateTime.Now.AddDays(-1), signupsClose: DateTime.UtcNow));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());

        Assert.That(ex!.InnerException, Is.InstanceOf<InvalidOperationException>().With.Message.Contains("Kind Local"));
    }
}
