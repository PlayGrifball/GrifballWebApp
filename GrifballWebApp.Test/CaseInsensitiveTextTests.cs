using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// Text compares the same on both databases: case-insensitive, accent-sensitive, as SQL Server's default
/// collation does (on Postgres, GrifballContext.CaseInsensitiveCollation).
/// </summary>
[TestFixture]
public class CaseInsensitiveTextTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<Season> AddSeason()
    {
        var season = new Season { SeasonName = "Season", SignupsOpen = DateTime.UtcNow, SignupsClose = DateTime.UtcNow };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task Equality_And_Contains_IgnoreCase_ButNotAccents()
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 1, Gamertag = "NoahS" });
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 2, Gamertag = "Noé" });
        await _context.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.XboxUsers.Where(x => x.Gamertag == "noahs").Select(x => x.XboxUserID).ToListAsync(), Is.EqualTo(new[] { 1L }));
            Assert.That(await _context.XboxUsers.Where(x => x.Gamertag.Contains("OAH")).Select(x => x.XboxUserID).ToListAsync(), Is.EqualTo(new[] { 1L }));
            Assert.That(await _context.XboxUsers.Where(x => x.Gamertag.StartsWith("noah")).Select(x => x.XboxUserID).ToListAsync(), Is.EqualTo(new[] { 1L }));
            Assert.That(await _context.XboxUsers.Where(x => x.Gamertag == "NOÉ").Select(x => x.XboxUserID).ToListAsync(), Is.EqualTo(new[] { 2L }));
            Assert.That(await _context.XboxUsers.Where(x => x.Gamertag == "noe").Select(x => x.XboxUserID).ToListAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task UniqueIndex_IgnoresCase()
    {
        var season = await AddSeason();
        _context.Teams.Add(new Team { SeasonID = season.SeasonID, TeamName = "Team Grif" });
        await _context.SaveChangesAsync();

        _context.Teams.Add(new Team { SeasonID = season.SeasonID, TeamName = "team GRIF" });

        Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Test]
    public async Task OrderBy_SortsAsPeopleRead()
    {
        foreach (var (id, gamertag) in new[] { (1L, "bravo"), (2L, "Charlie"), (3L, "alpha") })
            _context.XboxUsers.Add(new XboxUser { XboxUserID = id, Gamertag = gamertag });
        await _context.SaveChangesAsync();

        var sorted = await _context.XboxUsers.OrderBy(x => x.Gamertag).Select(x => x.Gamertag).ToListAsync();

        Assert.That(sorted, Is.EqualTo(new[] { "alpha", "bravo", "Charlie" }));
    }
}
