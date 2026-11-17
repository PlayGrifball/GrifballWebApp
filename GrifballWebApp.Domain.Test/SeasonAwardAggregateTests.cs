using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A season's awards are parts of the season (#511): the import records them through it (<see cref="Season.RecordArchivedAwards"/>),
/// which keeps a winner once per award, writes only what changed and takes away what the archive no longer gives; a team
/// the import removes takes its awards along, and a user merge moves them.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonAwardAggregateTests
{
    private static Season Imported() => Season.Imported("wb/Stats", "Winter League 2023", 2, 1, null, "https://sheets/wb", SeasonFormat.LeagueSeason);

    private static Team TeamOf(Season season, int teamID) => Team.Seed(teamID: teamID, seasonID: season.SeasonID, teamName: $"Team {teamID}");

    [Test]
    public void RecordArchivedAwards_GivesEachWinner_InTheArchivesOrder()
    {
        var season = Imported();
        var team = TeamOf(season, 4);

        var changes = season.RecordArchivedAwards(
        [
            new SeasonAwardWinner("MVP", "yLuxe", 1, null),
            new SeasonAwardWinner("Most Kills (one game)", " Sisum ", 2, team, " 134 "),
            new SeasonAwardWinner("Team Most Kills", "Team 4", null, team, "385"),
        ]);

        var awards = season.Awards.OrderBy(a => a.Position).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new AwardChanges(3, 0, 0)));
            Assert.That(awards.Select(a => (a.Name, a.WinnerName, a.UserID, a.TeamID, a.Value, a.Position)), Is.EqualTo(new[]
            {
                ("MVP", "yLuxe", (int?)1, (int?)null, (string?)null, 1),
                ("Most Kills (one game)", "Sisum", 2, 4, "134", 2),
                ("Team Most Kills", "Team 4", null, 4, "385", 3),
            }));
            Assert.That(awards.All(a => a.Season == season && a.SeasonID == season.SeasonID && a.SeasonAwardID == 0), Is.True, "The season's, not saved yet");
            Assert.That(awards.All(a => a.User is null && a.Team is null), Is.True, "Its player and team by their keys: EF loads the navigations");
        }
    }

    [Test]
    public void RecordArchivedAwards_Again_WritesOnlyWhatChanged_AndTakesAwayTheRest()
    {
        var season = Imported();
        season.RecordArchivedAwards(
        [
            new SeasonAwardWinner("MVP", "yLuxe", 1, null),
            new SeasonAwardWinner("Best Duo", "Mavbrid", 2, null),
            new SeasonAwardWinner("Best Duo", "MysticalNovice", 3, null),
        ]);
        var mvp = season.Awards.Single(a => a.Name == "MVP");

        var changes = season.RecordArchivedAwards(
        [
            new SeasonAwardWinner("mvp", "YLUXE", 1, null),
            new SeasonAwardWinner("MVP", "yLuxe", 9, null),
            new SeasonAwardWinner("Best Duo", "Mavbrid", 2, null, "#1"),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new AwardChanges(0, 1, 1)), "The same MVP unchanged (listed twice: once), the duo's value changed, its other half gone");
            Assert.That(season.Awards, Has.Count.EqualTo(2));
            Assert.That(season.Awards, Does.Contain(mvp), "The same row, found again whatever the case");
            Assert.That(mvp.UserID, Is.EqualTo(1), "A winner listed twice for an award counts once, as first listed");
            Assert.That(season.Awards.Single(a => a.Name == "Best Duo").Value, Is.EqualTo("#1"));
        }
    }

    [Test]
    public void RecordArchivedAwards_CutsTextToItsColumns()
    {
        var season = Imported();

        season.RecordArchivedAwards([new SeasonAwardWinner(new string('n', 120), new string('w', 120), 1, null, new string('v', 60))]);
        var award = season.Awards.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(award.Name, Has.Length.EqualTo(SeasonAward.NameMaxLength));
            Assert.That(award.WinnerName, Has.Length.EqualTo(SeasonAward.WinnerNameMaxLength));
            Assert.That(award.Value, Has.Length.EqualTo(SeasonAward.ValueMaxLength));
            Assert.That(award.IsFor(new string('N', 130), new string('W', 101)), Is.True, "Found again as cut");
        }
    }

    [Test]
    public void RecordArchivedAwards_Refuses_WhatCantBeAnAward()
    {
        var season = Imported();
        var organized = Season.Organized("Spring 2026", 1, 1);
        var otherSeasons = Team.Seed(teamID: 5, seasonID: 99);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => organized.RecordArchivedAwards([new SeasonAwardWinner("MVP", "A", 1, null)]), Throws.InvalidOperationException, "A season of the app's");
            Assert.That(() => season.RecordArchivedAwards([new SeasonAwardWinner(" ", "A", 1, null)]), Throws.ArgumentException, "No name");
            Assert.That(() => season.RecordArchivedAwards([new SeasonAwardWinner("MVP", "", 1, null)]), Throws.ArgumentException, "No winner");
            Assert.That(() => season.RecordArchivedAwards([new SeasonAwardWinner("MVP", "A", null, null)]), Throws.ArgumentException, "Neither a player nor a team");
            Assert.That(() => season.RecordArchivedAwards([new SeasonAwardWinner("MVP", "A", 1, otherSeasons)]), Throws.ArgumentException, "Another season's team");
            Assert.That(season.Awards, Is.Empty, "Refused before anything changed");
        }
    }

    [Test]
    public void RemoveArchivedAwardsOf_TakesAwayTheTeamsAwards_AndLeavesItsPlayersWithoutIt()
    {
        var season = Imported();
        season.RecordArchivedAwards(
        [
            new SeasonAwardWinner("MVP", "yLuxe", 1, null),
            new SeasonAwardWinner("Most Kills", "Sisum", 2, TeamOf(season, 4)),
            new SeasonAwardWinner("Team Most Kills", "Team 5", null, TeamOf(season, 5)),
        ]);

        var removed = season.RemoveArchivedAwardsOf([4, 5]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.EqualTo(1), "The team's own award");
            Assert.That(season.Awards.Select(a => (a.Name, a.UserID, a.TeamID)), Is.EquivalentTo(new[] { ("MVP", (int?)1, (int?)null), ("Most Kills", 2, null) }),
                "A player's award naming the team keeps its player, as a stat row does");
            Assert.That(() => Season.Organized("Spring 2026", 1, 1).RemoveArchivedAwardsOf([4]), Throws.InvalidOperationException);
        }
    }

    [Test]
    public void MergeAwards_MovesTheSourcesAwardsToTheTarget()
    {
        var season = Imported();
        season.RecordArchivedAwards(
        [
            new SeasonAwardWinner("MVP", "yLuxe", 1, null),
            new SeasonAwardWinner("Finals MVP", "Luxe", 1, null),
            new SeasonAwardWinner("Best Tank", "Jussshhhyyy", 2, null),
        ]);

        var moved = season.MergeAwards(1, 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved, Is.EqualTo(2));
            Assert.That(season.Awards.Select(a => (a.Name, a.UserID)), Is.EquivalentTo(new[] { ("MVP", (int?)3), ("Finals MVP", 3), ("Best Tank", 2) }));
        }
    }
}
