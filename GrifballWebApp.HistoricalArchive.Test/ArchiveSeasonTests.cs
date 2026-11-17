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

    /// <summary>RDG NHL's first three picks on the Teams tab's draft board (column I, "Team Name"), as the stats spell them.</summary>
    private static readonly (string Gamertag, int? Round)[] RdgNhlPicks = [("DlGITAL PAIN", 1), ("MysticalNovice", 2), ("Myst the Cold", 3)];

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

    [Test]
    public async Task H3Action2022_EveryTeamHasItsDraftPicks_RdgNhlsTeamNameColumnToo()
    {
        await using var context = Archive.NewContext();
        // Teams with their players, then the picks in memory: SQLite has no APPLY for a projection of both
        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.Season.HistoricalSourceID != null && t.Season.SeasonName == "2022 H3 Action League")
            .Include(t => t.TeamPlayers).ThenInclude(p => p.User)
            .ToListAsync();
        var picks = teams.SelectMany(t => t.TeamPlayers.Where(p => p.DraftRound != null).Select(p => new { t.TeamName, p.User.DisplayName, p.DraftRound })).ToList();
        if (picks.Count == 0)
            Assert.Ignore("2022 H3 Action League isn't in this copy of the archive, or has no draft");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(picks.Select(p => p.TeamName).Distinct().Count(), Is.EqualTo(11), "Each of the 11 teams' picks from the Teams tab's board");
            Assert.That(picks.Where(p => p.TeamName == "RDG NHL").Select(p => (p.DisplayName!.ToUpperInvariant(), p.DraftRound)),
                Is.SupersetOf(RdgNhlPicks.Select(p => (p.Gamertag.ToUpperInvariant(), p.Round))), "Its board column says \"Team Name\"");
        }
    }

    [Test]
    public async Task H3Action2022_BracketsSixthSeed_IsRdgNhlsTeamName_AndEveryPlayoffSeriesHasItsSlot()
    {
        await using var context = Archive.NewContext();
        var series = await context.SeasonMatches.AsNoTracking()
            .Where(m => m.Season.HistoricalSourceID != null && m.Season.SeasonName == "2022 H3 Action League" && m.BracketMatch != null)
            .Select(m => new { Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName })
            .ToListAsync();
        if (series.Count == 0)
            Assert.Ignore("2022 H3 Action League isn't in this copy of the archive, or has no bracket");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(series.Select(s => (s.Home.ToUpperInvariant(), s.Away.ToUpperInvariant())), Has.Some.EqualTo(("ALPHA", "RDG NHL")),
                "The PLAYOFFS' 6th seed, \"Team Name\", against Alpha");
            Assert.That(series.Where(s => s.Home == "THE BOYS SZN 3" || s.Away == "THE BOYS SZN 3"), Is.Empty, "THE BOYS SZN 3 didn't make the playoffs");
            Assert.That(Archive.Report.Warnings.Where(w => w.StartsWith("2022-h3-action", StringComparison.Ordinal) && w.Contains("in no slot", StringComparison.Ordinal)), Is.Empty);
        }
    }

    /// <summary>
    /// No season has a team named by a number: FL21's "0" and "1" (both SML21 workbooks: game sides of hosts who played no
    /// game, below the stats) and SML20's "4" and "5" (its playoff bracket's seeds, below the playoff stats) were rows outside
    /// the stats tables.
    /// </summary>
    [Test]
    public async Task NoSeason_HasATeamNamedByANumber()
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking().Where(t => t.Season.HistoricalSourceID != null)
            .Select(t => new { t.Season.SeasonName, t.TeamName })
            .ToListAsync();

        Assert.That(teams.Where(t => t.TeamName.Trim().Length > 0 && t.TeamName.Trim().All(char.IsAsciiDigit)).Select(t => $"{t.SeasonName}: {t.TeamName}"), Is.Empty);
    }

    /// <summary>Winter League 2021 (Legacy)'s playoff teams, whose Playoff Stats tab both SML21 workbooks carry a copy of.</summary>
    private static readonly string[] Wl21LegacyPlayoffTeams = ["Fruit Gang", "Limitless", "Only in Dreams", "Rock Bottom", "Yung Humma"];

    [TestCase("Summer League 2021 (Pro)")]
    [TestCase("Summer League 2021 (Amateur)")]
    public async Task Sml21_HasNoneOfWinterLeague2021LegacysPlayoffTeams(string seasonName)
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking().Where(t => t.Season.HistoricalSourceID != null && t.Season.SeasonName == seasonName)
            .Select(t => t.TeamName).ToListAsync();
        if (teams.Count == 0)
            Assert.Ignore($"{seasonName} isn't in this copy of the archive");

        Assert.That(teams.Intersect(Wl21LegacyPlayoffTeams, StringComparer.OrdinalIgnoreCase), Is.Empty, "The hidden Playoff Stats tab is WL21 Legacy's");
    }
}
