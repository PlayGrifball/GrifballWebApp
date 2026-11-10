using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The database's half of the lookup tables (#353): the seed data the factories write, and the constraints that refuse a
/// blank name or a negative rank threshold, alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ReferenceDataDatabaseTests
{
    private static readonly string[] SeededRegions = ["West North America", "Central North America", "East North America", "North Europe", "South Europe", "Australia", "Europe"];

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task The_seed_data_is_written_with_its_fixed_IDs()
    {
        var leagues = await _context.Leagues.AsNoTracking().OrderBy(l => l.LeagueID).Select(l => new { l.LeagueID, l.LeagueName, l.Slug }).ToListAsync();
        var games = await _context.GameVersions.AsNoTracking().OrderBy(g => g.GameVesionID).Select(g => new { g.GameVesionID, g.Slug }).ToListAsync();
        var regions = await _context.Regions.AsNoTracking().OrderBy(r => r.RegionID).Select(r => r.RegionName).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(leagues.Select(l => (l.LeagueID, l.LeagueName, l.Slug)),
                Is.EqualTo(new[] { (1, "AGLA", "agla"), (3, "GWL", "gwl"), (4, "IGL", "igl"), (5, "Community/Side", "side") }));
            Assert.That(games.Select(g => (g.GameVesionID, g.Slug)),
                Is.EqualTo(new[] { (1, "halo3"), (2, "reach"), (3, "reach-dash"), (4, "halo4"), (5, "halo5"), (6, "infinite") }));
            Assert.That(regions, Is.EqualTo(SeededRegions));
        }
    }

    // Past the factories, as only something outside the class could (raw SQL, a bad migration)
    private async Task<bool> Refused<T>(T entity, Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T>> change) where T : class
    {
        await using var ctx = _context.NewContextLike();
        var entry = ctx.Add(entity);
        change(entry);
        try
        {
            await ctx.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }

    [Test]
    public async Task The_database_refuses_a_blank_name_or_slug()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused(League.Seed("Blank Name", "blank-name"), e => e.Property(l => l.LeagueName).CurrentValue = ""), Is.True, "A league with no name");
            Assert.That(await Refused(League.Seed("Blank Slug", "blank-slug"), e => e.Property(l => l.Slug).CurrentValue = ""), Is.True, "A league with no slug");
            Assert.That(await Refused(League.Seed("New League", "new-league"), _ => { }), Is.False);

            Assert.That(await Refused(GameVersion.Seed("Blank Name", "blank-name"), e => e.Property(g => g.GameVersionName).CurrentValue = ""), Is.True, "A game with no name");
            Assert.That(await Refused(GameVersion.Seed("Blank Slug", "blank-slug"), e => e.Property(g => g.Slug).CurrentValue = ""), Is.True, "A game with no slug");
            Assert.That(await Refused(GameVersion.Seed("Halo 2", "halo2"), _ => { }), Is.False);

            Assert.That(await Refused(Region.Seed("Blank"), e => e.Property(r => r.RegionName).CurrentValue = ""), Is.True, "A region with no name");
            Assert.That(await Refused(Region.Seed("Asia"), _ => { }), Is.False);

            // Only spaces: blank on every database (SQL Server pads '' to compare, Postgres and SQLite don't)
            Assert.That(await Refused(League.Seed("Spaces Name", "spaces-name"), e => e.Property(l => l.LeagueName).CurrentValue = "  "), Is.True, "A league named only spaces");
            Assert.That(await Refused(League.Seed("Spaces Slug", "spaces-slug"), e => e.Property(l => l.Slug).CurrentValue = "  "), Is.True, "A league slug of only spaces");
            Assert.That(await Refused(GameVersion.Seed("Spaces Name", "spaces-name"), e => e.Property(g => g.GameVersionName).CurrentValue = "  "), Is.True, "A game named only spaces");
            Assert.That(await Refused(GameVersion.Seed("Spaces Slug", "spaces-slug"), e => e.Property(g => g.Slug).CurrentValue = "  "), Is.True, "A game slug of only spaces");
            Assert.That(await Refused(Region.Seed("Spaces"), e => e.Property(r => r.RegionName).CurrentValue = "  "), Is.True, "A region named only spaces");
        }
    }

    [Test]
    public async Task The_database_refuses_a_negative_rank_threshold()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused(Rank.Create("Below", 0, "#000", "x", [1]), e => e.Property(r => r.MmrThreshold).CurrentValue = -1), Is.True);
            Assert.That(await Refused(Rank.Create("Zero", 0, "#000", "x", [1]), _ => { }), Is.False);
        }
    }
}
