using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.HistoricalArchive.Test;

/// <summary>Single seasons of the real archive whose sheets once lost teams: what they import now.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ArchiveSeasonTests
{
    /// <summary>
    /// The Teams tab's regular-season records, team per column, as the archive names the teams. RDG NHL's team was never
    /// named: the workbook calls it "Team Name" (TeamInfo, the Teams tab's column I), and the config names it after its
    /// captain. The tab has no ranks or final places.
    /// </summary>
    private static readonly (string Team, int? Wins, int? Losses)[] H3ActionRecords =
    [
        ("12 Inches of Tommy", 5, 15), ("42 Dugga’s (Yeah, aight)", 17, 3), ("ALPHA", 14, 6), ("GASLIGHT", 3, 17), ("JUNKYARD DOGS", 12, 8),
        ("LIKE CLOCKWORK", 14, 6), ("RDG NHL", 12, 8), ("sPeCiAl ReAsOnS", 7, 13), ("THE BOYS SZN 3", 4, 16), ("The Smurfs", 14, 6), ("ZEG", 6, 14),
    ];

    [Test]
    public async Task H3Action2022_StandingsCoverAllElevenTeams_TheTeamNameSlotToo()
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.Season.HistoricalSourceID != null && t.Season.SeasonName == "2022 H3 Action League")
            .Select(t => new { t.TeamName, t.RecordedWins, t.RecordedLosses, HasCaptain = t.CaptainID != null })
            .ToListAsync();
        if (teams.Count == 0)
            Assert.Ignore("2022 H3 Action League isn't in this copy of the archive");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.OrderBy(t => t.TeamName, StringComparer.Ordinal).Select(t => (t.TeamName, t.RecordedWins, t.RecordedLosses)),
                Is.EqualTo(H3ActionRecords.OrderBy(t => t.Team, StringComparer.Ordinal)), "Each team's record from the Teams tab");
            Assert.That(teams.Where(t => !t.HasCaptain).Select(t => t.TeamName), Is.Empty, "The Teams tab names every team's captain");
        }
    }
}
