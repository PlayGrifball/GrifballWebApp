using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// The lookup tables as aggregates (#353): each made only by its factory (the seeder's, the medal import's, a season's
/// timeslots) or its seed data, in a valid state, and changed by nothing after.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ReferenceDataAggregateTests
{
    [Test]
    public void A_rank_is_made_with_every_field()
    {
        var rank = Rank.Create("Gold 1", 1449, "#FFD700", "gold", [1, 2, 3]);

        Assert.That((rank.Name, rank.MmrThreshold, rank.Color, rank.Description, rank.Icon),
            Is.EqualTo(("Gold 1", 1449, "#FFD700", "gold", new byte[] { 1, 2, 3 })));
    }

    [Test]
    public void A_rank_refuses_a_blank_name_and_a_negative_threshold()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => Rank.Create(" ", 0, "#000", "x", []));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rank.Create("Iron", -1, "#000", "x", []));
            Assert.Throws<ArgumentNullException>(() => Rank.Create("Iron", 0, null!, "x", []));
            Assert.Throws<ArgumentNullException>(() => Rank.Create("Iron", 0, "#000", null!, []));
            Assert.Throws<ArgumentNullException>(() => Rank.Create("Iron", 0, "#000", "x", null!));
        });
    }

    [Test]
    public void A_medal_and_its_type_and_difficulty_are_imported_as_Halo_lists_them()
    {
        var type = MedalType.Imported(2, "Mode");
        var difficulty = MedalDifficulty.Imported(3, "Legendary");
        var medal = Medal.Imported(1512363953, "Perfection", "Win flawlessly", spriteIndex: 4, sortingWeight: 50, medalDifficultyID: 3, medalTypeID: 2, personalScore: 300);

        Assert.Multiple(() =>
        {
            Assert.That((type.MedalTypeID, type.MedalTypeName, type.Medals.Count), Is.EqualTo((2, "Mode", 0)));
            Assert.That((difficulty.MedalDifficultyID, difficulty.MedalDifficultyName, difficulty.Medals.Count), Is.EqualTo((3, "Legendary", 0)));
            Assert.That((medal.MedalID, medal.MedalName, medal.Description, medal.SpriteIndex, medal.SortingWeight, medal.MedalDifficultyID, medal.MedalTypeID, medal.PersonalScore),
                Is.EqualTo((1512363953L, "Perfection", "Win flawlessly", 4, 50, 3, 2, 300)));
            Assert.That(medal.MedalEarned, Is.Empty);
        });
    }

    [Test]
    public void A_timeslot_is_a_day_and_a_time()
    {
        var option = AvailabilityOption.At(DayOfWeek.Thursday, new TimeOnly(21, 30));

        Assert.Multiple(() =>
        {
            Assert.That((option.AvailabilityOptionID, option.DayOfWeek, option.Time), Is.EqualTo((0, DayOfWeek.Thursday, new TimeOnly(21, 30))));
            Assert.That(option.TeamAvailability, Is.Empty);
            Assert.That(option.SeasonAvailability, Is.Empty);
        });
    }

    [Test]
    public void A_timeslot_refuses_a_day_no_week_has()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AvailabilityOption.At((DayOfWeek)7, new TimeOnly(20, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => AvailabilityOption.At((DayOfWeek)(-1), new TimeOnly(20, 0)));
            Assert.That(AvailabilityOption.IsDay(DayOfWeek.Saturday), Is.True);
            Assert.That(AvailabilityOption.IsDay((DayOfWeek)9), Is.False);
        });
    }

    [Test]
    public void A_seeded_timeslot_holds_the_teams_given()
    {
        var option = AvailabilityOption.Seed(7, DayOfWeek.Friday, new TimeOnly(20, 0), [TeamAvailability.Seed(teamID: 1), TeamAvailability.Seed(teamID: 2)]);

        Assert.Multiple(() =>
        {
            Assert.That((option.AvailabilityOptionID, option.DayOfWeek, option.Time), Is.EqualTo((7, DayOfWeek.Friday, new TimeOnly(20, 0))));
            Assert.That(option.TeamAvailability.Select(t => t.TeamID), Is.EquivalentTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public void Seed_data_refuses_a_blank_name_or_slug()
    {
        Assert.Multiple(() =>
        {
            Assert.That(League.Seed("GWL", "gwl", leagueID: 3), Has.Property(nameof(League.LeagueID)).EqualTo(3));
            Assert.Throws<ArgumentException>(() => League.Seed("", "x"));
            Assert.Throws<ArgumentException>(() => League.Seed("x", " "));
            Assert.That(GameVersion.Seed("Halo 3", "halo3", gameVersionID: 1), Has.Property(nameof(GameVersion.GameVesionID)).EqualTo(1));
            Assert.Throws<ArgumentException>(() => GameVersion.Seed(" ", "x"));
            Assert.Throws<ArgumentNullException>(() => GameVersion.Seed("x", null!));
            Assert.That(Region.Seed("Europe", regionID: 7), Has.Property(nameof(Region.RegionName)).EqualTo("Europe"));
            Assert.Throws<ArgumentException>(() => Region.Seed(""));
        });
    }
}

/// <summary>
/// The database's half of the lookup tables (#353): the seed data the factories write, and the constraints that refuse a
/// blank name or a negative rank threshold, alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ReferenceDataDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task The_seed_data_is_written_with_its_fixed_IDs()
    {
        var leagues = await _context.Leagues.AsNoTracking().OrderBy(l => l.LeagueID).Select(l => new { l.LeagueID, l.LeagueName, l.Slug }).ToListAsync();
        var games = await _context.GameVersions.AsNoTracking().OrderBy(g => g.GameVesionID).Select(g => new { g.GameVesionID, g.Slug }).ToListAsync();
        var regions = await _context.Regions.AsNoTracking().OrderBy(r => r.RegionID).Select(r => r.RegionName).ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(leagues.Select(l => (l.LeagueID, l.LeagueName, l.Slug)),
                Is.EqualTo(new[] { (1, "AGLA", "agla"), (3, "GWL", "gwl"), (4, "IGL", "igl"), (5, "Community/Side", "side") }));
            Assert.That(games.Select(g => (g.GameVesionID, g.Slug)),
                Is.EqualTo(new[] { (1, "halo3"), (2, "reach"), (3, "reach-dash"), (4, "halo4"), (5, "halo5"), (6, "infinite") }));
            Assert.That(regions, Is.EqualTo(new[] { "West North America", "Central North America", "East North America", "North Europe", "South Europe", "Australia", "Europe" }));
        });
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
        Assert.Multiple(async () =>
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
        });
    }

    [Test]
    public async Task The_database_refuses_a_negative_rank_threshold()
    {
        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(Rank.Create("Below", 0, "#000", "x", [1]), e => e.Property(r => r.MmrThreshold).CurrentValue = -1), Is.True);
            Assert.That(await Refused(Rank.Create("Zero", 0, "#000", "x", [1]), _ => { }), Is.False);
        });
    }
}
