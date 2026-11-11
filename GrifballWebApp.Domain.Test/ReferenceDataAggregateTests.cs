using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

// The class (Domain.Shared), not the namespace GrifballWebApp.Domain.League that this namespace sees first
using League = GrifballWebApp.Domain.Shared.League;

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
        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentException>(() => Rank.Create(" ", 0, "#000", "x", []));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rank.Create("Iron", -1, "#000", "x", []));
            Assert.Throws<ArgumentNullException>(() => Rank.Create("Iron", 0, null!, "x", []));
            Assert.Throws<ArgumentNullException>(() => Rank.Create("Iron", 0, "#000", null!, []));
            Assert.Throws<ArgumentNullException>(() => Rank.Create("Iron", 0, "#000", "x", null!));
        }
    }

    [Test]
    public void A_medal_and_its_type_and_difficulty_are_imported_as_Halo_lists_them()
    {
        var type = MedalType.Imported(2, "Mode");
        var difficulty = MedalDifficulty.Imported(3, "Legendary");
        var medal = Medal.Imported(1512363953, "Perfection", "Win flawlessly", spriteIndex: 4, sortingWeight: 50, medalDifficultyID: 3, medalTypeID: 2, personalScore: 300);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((type.MedalTypeID, type.MedalTypeName, type.Medals.Count), Is.EqualTo((2, "Mode", 0)));
            Assert.That((difficulty.MedalDifficultyID, difficulty.MedalDifficultyName, difficulty.Medals.Count), Is.EqualTo((3, "Legendary", 0)));
            Assert.That((medal.MedalID, medal.MedalName, medal.Description, medal.SpriteIndex, medal.SortingWeight, medal.MedalDifficultyID, medal.MedalTypeID, medal.PersonalScore),
                Is.EqualTo((1512363953L, "Perfection", "Win flawlessly", 4, 50, 3, 2, 300)));
            Assert.That(medal.MedalEarned, Is.Empty);
        }
    }

    [Test]
    public void A_timeslot_is_a_day_and_a_time()
    {
        var option = AvailabilityOption.At(DayOfWeek.Thursday, new TimeOnly(21, 30));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((option.AvailabilityOptionID, option.DayOfWeek, option.Time), Is.EqualTo((0, DayOfWeek.Thursday, new TimeOnly(21, 30))));
            Assert.That(option.TeamAvailability, Is.Empty);
            Assert.That(option.SeasonAvailability, Is.Empty);
        }
    }

    [Test]
    public void A_timeslot_refuses_a_day_no_week_has()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AvailabilityOption.At((DayOfWeek)7, new TimeOnly(20, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => AvailabilityOption.At((DayOfWeek)(-1), new TimeOnly(20, 0)));
            Assert.That(AvailabilityOption.IsDay(DayOfWeek.Saturday), Is.True);
            Assert.That(AvailabilityOption.IsDay((DayOfWeek)9), Is.False);
        }
    }

    private static readonly int[] expected = new[] { 1, 2 };

    [Test]
    public void A_seeded_timeslot_holds_the_teams_given()
    {
        var option = AvailabilityOption.Seed(7, DayOfWeek.Friday, new TimeOnly(20, 0), [TeamAvailability.Seed(teamID: 1), TeamAvailability.Seed(teamID: 2)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((option.AvailabilityOptionID, option.DayOfWeek, option.Time), Is.EqualTo((7, DayOfWeek.Friday, new TimeOnly(20, 0))));
            Assert.That(option.TeamAvailability.Select(t => t.TeamID), Is.EquivalentTo(expected));
        }
    }

    [Test]
    public void Seed_data_refuses_a_blank_name_or_slug()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(League.Seed("GWL", "gwl", leagueID: 3), Has.Property(nameof(League.LeagueID)).EqualTo(3));
            Assert.Throws<ArgumentException>(() => League.Seed("", "x"));
            Assert.Throws<ArgumentException>(() => League.Seed("x", " "));
            Assert.That(GameVersion.Seed("Halo 3", "halo3", gameVersionID: 1), Has.Property(nameof(GameVersion.GameVesionID)).EqualTo(1));
            Assert.Throws<ArgumentException>(() => GameVersion.Seed(" ", "x"));
            Assert.Throws<ArgumentNullException>(() => GameVersion.Seed("x", null!));
            Assert.That(Region.Seed("Europe", regionID: 7), Has.Property(nameof(Region.RegionName)).EqualTo("Europe"));
            Assert.Throws<ArgumentException>(() => Region.Seed(""));
        }
    }
}
