using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

// The class (Domain.Shared), not the namespace GrifballWebApp.Domain.League that this namespace sees first
using League = GrifballWebApp.Domain.Shared.League;

/// <summary>
/// A season is an aggregate (#323): it begins as an organizer's or as the archive's, an imported one is refused for the
/// organizer's edits and an organizer's for the import's, and its timeslots and titles are changed through it alone.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SeasonAggregateTests
{
    private static Season Organized() => Season.Organized("Spring 2026", League.DefaultLeagueID, 1, " Pro ");

    private static Season Imported(string source = "wb/Total Stats") => Season.Imported(source, "Summer League 2018", 2, 1, "Pro", "https://sheets/wb", SeasonFormat.LeagueSeason);

    private static Team TeamOf(Season season, int teamID) => Team.Seed(teamID: teamID, seasonID: season.SeasonID, teamName: $"Team {teamID}");

    [Test]
    public void Organized_IsTheAppsSeason_ItsDivisionTrimmed()
    {
        var season = Season.Organized("Spring 2026", 3, 1, " Pro ", SeasonFormat.Tournament);
        var open = Season.Organized("Fall 2026", 3, 1, "  ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((season.SeasonName, season.LeagueID, season.GameVersionID, season.Division, season.Format), Is.EqualTo(("Spring 2026", 3, (int?)1, "Pro", SeasonFormat.Tournament)));
            Assert.That(season.IsImported, Is.False);
            Assert.That(season.HistoricalSourceUrl, Is.Null);
            Assert.That(season.PublicAt, Is.Null, "Not public until announced");
            Assert.That(season.ChampionTier, Is.EqualTo("Pro"));
            Assert.That(open.Division, Is.Null, "A blank division is none");
            Assert.That(open.ChampionTier, Is.EqualTo(Season.DefaultChampionTier));
        }
    }

    [Test]
    public void Imported_IsTheArchives_AndNeedsItsKey()
    {
        var season = Imported();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((season.HistoricalSourceID, season.HistoricalSourceUrl, season.LeagueID, season.Division), Is.EqualTo(("wb/Total Stats", "https://sheets/wb", 2, "Pro")));
            Assert.That(season.IsImported, Is.True);
            Assert.That(() => Season.Imported(" ", "S", 1, null, null, null, SeasonFormat.LeagueSeason), Throws.ArgumentException);
        }
    }

    [Test]
    public void Edit_TheOrganizers_RefusedForAnImportedSeason()
    {
        var season = Organized();
        var imported = Imported();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.Edit("Spring 2027", 2, 3, " Rookie ", SeasonFormat.Tournament), Is.EqualTo(SeasonEditResult.Edited));
            Assert.That((season.SeasonName, season.LeagueID, season.GameVersionID, season.Division, season.Format), Is.EqualTo(("Spring 2027", 2, (int?)3, "Rookie", SeasonFormat.Tournament)));
            Assert.That(imported.Edit("Renamed", 1, null, null, SeasonFormat.Tournament), Is.EqualTo(SeasonEditResult.Imported));
            Assert.That((imported.SeasonName, imported.LeagueID, imported.Format), Is.EqualTo(("Summer League 2018", 2, SeasonFormat.LeagueSeason)), "Nothing changed");
        }
    }

    [Test]
    public void TheImportsUpdates_OnlyAnImportedSeason()
    {
        var season = Organized();
        var imported = Imported();
        imported.UpdateArchived("Summer League 2018 (Pro)", 3, 2, "Legacy", "https://sheets/other", SeasonFormat.Tournament);
        imported.AdoptArchiveSource("other/Stats");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((imported.SeasonName, imported.LeagueID, imported.GameVersionID, imported.Division, imported.HistoricalSourceUrl, imported.Format, imported.HistoricalSourceID),
                Is.EqualTo(("Summer League 2018 (Pro)", 3, (int?)2, "Legacy", "https://sheets/other", SeasonFormat.Tournament, "other/Stats")));
            Assert.That(() => season.UpdateArchived("X", 1, null, null, "https://sheets/x", SeasonFormat.LeagueSeason), Throws.InvalidOperationException.With.Message.Contains("not an imported season"));
            Assert.That(() => season.AdoptArchiveSource("x/Stats"), Throws.InvalidOperationException);
            Assert.That(() => imported.AdoptArchiveSource(""), Throws.ArgumentException);
            Assert.That(season.HistoricalSourceID, Is.Null, "An app season stays the app's");
        }
    }

    [Test]
    public void TheOrganizersCopies_RefusedForAnImportedSeason()
    {
        var imported = Imported();
        var other = Organized();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => imported.CopyAvailabilityFrom(other), Throws.InvalidOperationException.With.Message.Contains("only the import changes it"));
            Assert.That(() => imported.CopySignupsFrom(other), Throws.InvalidOperationException);
            Assert.That(() => imported.CopyTeamsFrom(other), Throws.InvalidOperationException);
        }
    }

    [Test]
    public void LockCaptains_LocksAndUnlocks()
    {
        var season = Organized();
        season.LockCaptains(true);
        var locked = season.CaptainsLocked;
        season.LockCaptains(false);

        Assert.That((locked, season.CaptainsLocked), Is.EqualTo((true, false)));
    }

    [Test]
    public void SetAvailability_KeepsThePickedSlots_AddsTheNew_AndDropsTheRest()
    {
        var season = Organized();
        var monday = AvailabilityOption.Seed(1, DayOfWeek.Monday, new TimeOnly(18, 0));
        var tuesday = AvailabilityOption.Seed(2, DayOfWeek.Tuesday, new TimeOnly(18, 0));
        season.SetAvailability([monday, tuesday]);
        var kept = season.SeasonAvailability.Single(a => a.AvailabilityOptionID == 1);
        var unsaved = AvailabilityOption.At(DayOfWeek.Friday, new TimeOnly(20, 0));

        season.SetAvailability([monday, unsaved]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.SeasonAvailability, Has.Count.EqualTo(2));
            Assert.That(season.SeasonAvailability, Does.Contain(kept), "A slot it had stays as it is");
            Assert.That(season.SeasonAvailability.Select(a => a.AvailabilityOption), Is.EquivalentTo(new[] { monday, unsaved }));
            Assert.That(season.SeasonAvailability.All(a => a.Season == season), Is.True);
        }
    }

    [Test]
    public void SetAvailability_RefusedForAnImportedSeason()
    {
        var season = Imported();
        var organized = Organized();
        var monday = AvailabilityOption.Seed(1, DayOfWeek.Monday, new TimeOnly(18, 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.SetAvailability([monday]), Is.EqualTo(SeasonEditResult.Imported));
            Assert.That(season.SeasonAvailability, Is.Empty, "Nothing changed");
            Assert.That(organized.SetAvailability([monday]), Is.EqualTo(SeasonEditResult.Edited));
        }
    }

    [Test]
    public void CopyAvailabilityFrom_TakesTheOthersSlots()
    {
        var from = Organized();
        from.SetAvailability([AvailabilityOption.Seed(1), AvailabilityOption.Seed(3)]);
        var season = Organized();
        season.SetAvailability([AvailabilityOption.Seed(1), AvailabilityOption.Seed(2)]);
        var kept = season.SeasonAvailability.Single(a => a.AvailabilityOptionID == 1);

        season.CopyAvailabilityFrom(from);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.SeasonAvailability.Select(a => a.AvailabilityOptionID), Is.EquivalentTo(new[] { 1, 3 }));
            Assert.That(season.SeasonAvailability, Does.Contain(kept));
        }
    }

    [Test]
    public void RecordChampions_TheSeasonsTier_AndNotForAnImportedSeason()
    {
        var season = Organized();
        var team = TeamOf(season, 7);

        var changes = season.RecordChampions(team, [new ChampionMember(1, "Captain", true), new ChampionMember(2, new string('x', 60), false)]);
        var imported = Imported();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new ChampionshipChanges(2, 0, 0)));
            Assert.That(season.PlayerChampionships.Select(c => (c.UserID, c.Tier, c.TeamID, c.IsCaptain, c.GameVersionID)),
                Is.EquivalentTo(new[] { (1, "Pro", 7, true, (int?)1), (2, "Pro", 7, false, (int?)1) }));
            Assert.That(season.PlayerChampionships.Single(c => c.UserID == 2).GamertagUsed, Has.Length.EqualTo(PlayerChampionship.GamertagUsedMaxLength), "Cut to its column");
            Assert.That(imported.RecordChampions(TeamOf(imported, 1), [new ChampionMember(1, "A", true)]), Is.Null, "Its titles are its record's");
            Assert.That(imported.PlayerChampionships, Is.Empty);
            Assert.That(() => season.RecordChampions(Team.Seed(teamID: 9, seasonID: 99), []), Throws.ArgumentException, "Another season's team");
        }
    }

    [Test]
    public void RecordArchivedChampions_AnyTier_OnlyAnImportedSeason()
    {
        var imported = Imported();
        var team = TeamOf(imported, 4);

        var pro = imported.RecordArchivedChampions("Pro", team, [new ChampionMember(1, "A", true)], 6);
        var amateur = imported.RecordArchivedChampions(" Amateur ", team, [new ChampionMember(2, "B", false)], null);
        var again = imported.RecordArchivedChampions("pro", team, [new ChampionMember(1, "A", true)], 6);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((pro, amateur, again), Is.EqualTo((new ChampionshipChanges(1, 0, 0), new ChampionshipChanges(1, 0, 0), new ChampionshipChanges(0, 1, 0))));
            Assert.That(imported.PlayerChampionships.Select(c => c.Tier), Is.EquivalentTo(new[] { "Pro", "Amateur" }), "Kept as the tier first spelled it, trimmed");
            Assert.That(() => Organized().RecordArchivedChampions("Pro", team, [], null), Throws.InvalidOperationException);
            Assert.That(() => imported.RecordArchivedChampions("Pro", null!, [], null), Throws.ArgumentNullException);
        }
    }

    [Test]
    public void TheImportRemovesTitles_OfTiersAndTeamsGone_OnlyFromAnImportedSeason()
    {
        var imported = Imported();
        imported.RecordArchivedChampions("Pro", TeamOf(imported, 4), [new ChampionMember(1, "A", true)], null);
        imported.RecordArchivedChampions("Amateur", TeamOf(imported, 5), [new ChampionMember(2, "B", true)], null);
        imported.RecordArchivedChampions("Rookie", TeamOf(imported, 6), [new ChampionMember(3, "C", true)], null);
        var season = Organized();

        var tiersGone = imported.RemoveArchivedTitlesExcept(new HashSet<string>(["pro", "Amateur"], StringComparer.OrdinalIgnoreCase));
        var teamsGone = imported.RemoveArchivedTitlesOf([5]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((tiersGone, teamsGone), Is.EqualTo((1, 1)));
            Assert.That(imported.PlayerChampionships.Select(c => c.Tier), Is.EqualTo(new[] { "Pro" }));
            Assert.That(() => season.RemoveArchivedTitlesExcept(new HashSet<string>()), Throws.InvalidOperationException);
            Assert.That(() => season.RemoveArchivedTitlesOf([1]), Throws.InvalidOperationException);
        }
    }

    [Test]
    public void MergeTitles_MovesTheSourcesTitles_KeepingOneTitlePerTier()
    {
        var season = Organized();
        var team = TeamOf(season, 7);
        season.SeedChampions("Pro", team, [new ChampionMember(1, "Target", false), new ChampionMember(2, "Source", true)], null);
        season.SeedChampions("Amateur", team, [new ChampionMember(2, "Source", false)], null);

        var moved = season.MergeTitles(fromUserID: 2, toUserID: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved, Is.EqualTo(1), "The Amateur title moves; the Pro one both held goes");
            Assert.That(season.PlayerChampionships.Select(c => (c.UserID, c.Tier, c.IsCaptain)), Is.EquivalentTo(new[] { (1, "Pro", true), (1, "Amateur", false) }),
                "The target keeps its own title, with the source's captaincy");
        }
    }
}
