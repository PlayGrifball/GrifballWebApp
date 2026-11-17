using System.Text;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.HistoricalArchive.Test;

/// <summary>
/// The archive imported whole: what every imported season should have. Each test names the seasons that fall short, so
/// a failure is the list of what to fix. <see cref="Summary_IsWritten"/> writes each season's counts beside the copy.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ArchiveImportTests
{
    private sealed record SeasonCounts(string Name, string SourceID, string Format, int Teams, int Captains, int Ranked, int Finished, int Stats, int PlayoffStats,
        int Matches, int PlayoffMatches, int Titles, int Awards, int Mvps);

    private sealed record LateLeagueCounts(string Name, string? SourceID, int Stats, int Games);

    // Read once: the database doesn't change after the import
    private static readonly Lazy<Task<List<SeasonCounts>>> SeasonsRead = new(async () =>
    {
        await using var context = Archive.NewContext();
        return await context.Seasons.AsNoTracking().Where(s => s.HistoricalSourceID != null)
            .OrderBy(s => s.SeasonStart).ThenBy(s => s.SeasonName)
            .Select(s => new SeasonCounts(s.SeasonName, s.HistoricalSourceID, s.Format.ToString(), s.Teams.Count, s.Teams.Count(t => t.CaptainID != null),
                s.Teams.Count(t => t.ArchivedRank != null), s.Teams.Count(t => t.ArchivedFinish != null),
                context.PlayerSeasonStats.Count(p => p.SeasonID == s.SeasonID), context.PlayerSeasonStats.Count(p => p.SeasonID == s.SeasonID && p.IsPlayoffs),
                s.SeasonMatches.Count, s.SeasonMatches.Count(m => m.BracketMatch != null), s.PlayerChampionships.Count, s.Awards.Count, s.Awards.Count(a => a.Name == "MVP")))
            .ToListAsync();
    });

    private static readonly Lazy<Task<List<LateLeagueCounts>>> LateLeagueRead = new(async () =>
    {
        await using var context = Archive.NewContext();
        return await context.MatchmakingSeasons.AsNoTracking().Where(s => s.HistoricalSourceID != null)
            .OrderBy(s => s.StartsAt).ThenBy(s => s.Name)
            .Select(s => new LateLeagueCounts(s.Name, s.HistoricalSourceID, s.PlayerStats.Count, s.MatchedMatches.Count))
            .ToListAsync();
    });

    private static Task<List<SeasonCounts>> Seasons() => SeasonsRead.Value;

    /// <summary>What the import left out, but the Waypoint matches no run here can pull (no Halo API).</summary>
    private static IEnumerable<string> Skipped() => Archive.Report.Skipped.Where(s => !Archive.WaypointNotPulled(s));

    [Test]
    public void Import_HasNoErrors()
    {
        Assert.That(Archive.Report.Errors, Is.Empty);
    }

    [Test]
    public void Import_HasNoWarnings()
    {
        Assert.That(Archive.Report.Warnings, Is.Empty);
    }

    [Test]
    public void Import_SkipsNothing()
    {
        Assert.That(Skipped(), Is.Empty, "What the import left out (a season of an unknown league or game, a game with no season)");
    }

    /// <summary>Not the seasons whose games are Waypoint matches: no run here has them.</summary>
    [Test]
    public void Import_MissesNoMatch()
    {
        var waypoint = Archive.WaypointSeasons;

        Assert.That(Archive.Report.MissingMatches.Where(m => !waypoint.Any(w => m.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase))), Is.Empty);
    }

    [Test]
    public async Task EverySeason_HasTeams()
    {
        Assert.That((await Seasons()).Where(s => s.Teams == 0).Select(s => s.Name), Is.Empty);
    }

    [Test]
    public async Task EverySeason_HasStats()
    {
        Assert.That((await Seasons()).Where(s => s.Stats == 0).Select(s => s.Name), Is.Empty);
    }

    /// <summary>A line from a tab that counts rounds, not games (the Infinite stats tool's, 2025 on: its rounds are kept in
    /// the line's extended metrics) has its games played, wins and losses from the season's games tab
    /// (StatsTab.GamesSheet): not all three 0, for a line that played a round.</summary>
    [Test]
    public async Task EveryStatLineWithRounds_HasGames()
    {
        await using var context = Archive.NewContext();
        var lines = await context.PlayerSeasonStats.AsNoTracking()
            .Where(p => p.Season.HistoricalSourceID != null && p.ExtendedMetricsJson != null && p.ExtendedMetricsJson.Contains("\"roundsWon\""))
            .Select(p => new { p.Season.SeasonName, p.IsPlayoffs, NoGames = p.GamesPlayed == 0 && p.Wins == 0 && p.Losses == 0, p.ExtendedMetricsJson })
            .ToListAsync();

        Assert.That(lines.Where(l => Rounds(l.ExtendedMetricsJson) > 0).GroupBy(l => (l.SeasonName, l.IsPlayoffs)).Where(g => g.Any(l => l.NoGames))
            .Select(g => $"{g.Key.SeasonName}{(g.Key.IsPlayoffs ? " (playoffs)" : "")}: {g.Count(l => l.NoGames)} of {g.Count()} lines"), Is.Empty);
    }

    private static readonly string[] RoundMetrics = ["roundsWon", "roundsLost", "roundsTied"];

    /// <summary>A line's rounds won, lost and tied, from its extended metrics.</summary>
    private static decimal Rounds(string json)
    {
        using var metrics = System.Text.Json.JsonDocument.Parse(json);
        return RoundMetrics
            .Sum(name => metrics.RootElement.TryGetProperty(name, out var value) ? value.GetDecimal() : 0);
    }

    [Test]
    public async Task EveryTeam_HasACaptain()
    {
        Assert.That((await Seasons()).Where(s => s.Captains < s.Teams).Select(s => $"{s.Name}: {s.Captains} of {s.Teams}"), Is.Empty);
    }

    /// <summary>Its standings' rank, or its final place, for each team.</summary>
    [Test]
    public async Task EverySeason_PlacesItsTeams()
    {
        Assert.That((await Seasons()).Where(s => s.Ranked == 0 && s.Finished == 0).Select(s => s.Name), Is.Empty);
    }

    /// <summary>A season whose standings rank some teams ranks every one: a team left out is a name its standings spell
    /// another way, or a team they don't list.</summary>
    [Test]
    public async Task EveryTeamOfARankedSeason_IsRanked()
    {
        Assert.That((await Seasons()).Where(s => s.Ranked > 0 && s.Ranked < s.Teams).Select(s => $"{s.Name}: {s.Ranked} of {s.Teams}"), Is.Empty);
    }

    [Test]
    public async Task EverySeason_HasAChampion()
    {
        Assert.That((await Seasons()).Where(s => s.Titles == 0).Select(s => s.Name), Is.Empty);
    }

    /// <summary>Not the seasons whose games are Waypoint matches: no run here has them.</summary>
    [Test]
    public async Task EverySeasonWithPlayoffs_HasABracket()
    {
        var waypoint = Archive.WaypointSeasons;

        Assert.That((await Seasons()).Where(s => s.PlayoffStats > 0 && s.PlayoffMatches == 0 && !waypoint.Contains(s.SourceID)).Select(s => s.Name), Is.Empty);
    }

    /// <summary>The Halo Infinite seasons of Luxe's Record Book (Season 1 to 7, Season 6 Pro and Legacy): their stats workbooks.</summary>
    private static readonly string[] RecordBookSeasons =
    [
        "2023-da-wl23-stats", "2024-da-sl24-stats", "2024-igl-evolved-fl24-stats", "2025-octane-wl25-stats", "2025-octane-sl25-bid-stats",
        "2025-classic-sml25-pro-stats", "2025-classic-sml25-legacy-stats", "2026-sl26-major-stats",
    ];

    /// <summary>Each Infinite season of the record book has its MVP.</summary>
    [Test]
    public async Task EveryInfiniteSeasonOfTheRecordBook_HasAnMvp()
    {
        var seasons = (await Seasons()).Where(s => RecordBookSeasons.Any(w => s.SourceID.StartsWith(w + "/", StringComparison.OrdinalIgnoreCase))).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons, Has.Count.EqualTo(RecordBookSeasons.Length), "Every season of the record book imported");
            Assert.That(seasons.Where(s => s.Mvps != 1).Select(s => $"{s.Name}: {s.Mvps} MVPs"), Is.Empty);
        }
    }

    [Test]
    public async Task EveryLateLeagueSeason_HasStatsAndGames()
    {
        Assert.That((await LateLeagueRead.Value).Where(s => s.Stats == 0 || s.Games == 0).Select(s => $"{s.Name}: {s.Stats} stats, {s.Games} games"), Is.Empty);
    }

    /// <summary>Not a check: each season's counts, in <c>summary.md</c> beside the copy, to compare runs.</summary>
    [Test]
    public async Task Summary_IsWritten()
    {
        var summary = new StringBuilder("## Seasons\n\n| Season | Source | Format | Teams | Captains | Ranked | Finished | Stats | Playoff stats | Matches | Playoff matches | Titles | Awards |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (var s in await Seasons())
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Format} | {s.Teams} | {s.Captains} | {s.Ranked} | {s.Finished} | {s.Stats} | {s.PlayoffStats} | {s.Matches} | {s.PlayoffMatches} | {s.Titles} | {s.Awards} |");
        summary.AppendLine().AppendLine("## Late League").AppendLine().AppendLine("| Season | Source | Stats | Games |").AppendLine("|---|---|---|---|");
        foreach (var s in await LateLeagueRead.Value)
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Stats} | {s.Games} |");
        Section(summary, "Placings (the seasons whose standings place a team: rank / finish, record)", await Placings());
        Section(summary, "Awards (each season's, in its order: award, winner, team or value)", await Awards());
        Section(summary, "Build warnings", Archive.Build.Warnings);
        Section(summary, "Import warnings", Archive.Report.Warnings);
        Section(summary, "Skipped", Skipped());
        Section(summary, "Missing matches", Archive.Report.MissingMatches);
        Section(summary, "Not filled", Archive.FillProblems);
        summary.AppendLine().AppendLine($"Waypoint matches not pulled (no Halo API in this run): {Archive.Report.Skipped.Count(Archive.WaypointNotPulled)}");
        var path = Path.Combine(Archive.Snapshots.Folder, "summary.md");
        await File.WriteAllTextAsync(path, summary.ToString());

        Assert.That(path, Does.Exist);
    }

    /// <summary>Each season whose standings place a team: its teams in their order, the unplaced last.</summary>
    private static async Task<List<string>> Placings()
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking()
            .Where(t => t.Season.HistoricalSourceID != null && t.Season.Teams.Any(o => o.ArchivedRank != null || o.ArchivedFinish != null))
            .Select(t => new { t.Season.SeasonName, t.Season.SeasonStart, t.TeamName, t.ArchivedRank, t.ArchivedFinish, t.RecordedWins, t.RecordedLosses })
            .ToListAsync();
        return teams.GroupBy(t => (t.SeasonStart, t.SeasonName)).OrderBy(g => g.Key)
            .Select(g => $"{g.Key.SeasonName}: " + string.Join("; ", g.OrderBy(t => t.ArchivedRank ?? int.MaxValue).ThenBy(t => t.ArchivedFinish ?? int.MaxValue).ThenBy(t => t.TeamName)
                .Select(t => $"{t.TeamName} {t.ArchivedRank?.ToString() ?? "-"}/{t.ArchivedFinish?.ToString() ?? "-"}{(t.RecordedWins is null ? "" : $" ({t.RecordedWins}-{t.RecordedLosses})")}")))
            .ToList();
    }

    /// <summary>Each season's awards, in its order: the award, its winner, their team (when not the winner) and its value.</summary>
    private static async Task<List<string>> Awards()
    {
        await using var context = Archive.NewContext();
        // From the awards, joined to their season and team (a SelectMany from the seasons would need APPLY, which SQLite lacks)
        var awards = await context.Set<GrifballWebApp.Domain.League.SeasonAward>().AsNoTracking().Where(a => a.Season.HistoricalSourceID != null)
            .Select(a => new { a.Season.SeasonName, a.Season.SeasonStart, a.Position, a.Name, a.WinnerName, Team = a.Team != null ? a.Team.TeamName : null, a.Value })
            .ToListAsync();
        return awards.GroupBy(a => (a.SeasonStart, a.SeasonName)).OrderBy(g => g.Key)
            .Select(g => $"{g.Key.SeasonName}: " + string.Join("; ", g.OrderBy(a => a.Position)
                .Select(a => $"{a.Name}: {a.WinnerName}{(a.Team is null || a.Team == a.WinnerName ? "" : $" ({a.Team})")}{(a.Value is null ? "" : $" [{a.Value}]")}")))
            .ToList();
    }

    private static void Section(StringBuilder summary, string title, IEnumerable<string> lines)
        => summary.AppendLine().AppendLine($"## {title}").AppendLine().AppendJoin('\n', lines.Select(l => $"- {l}")).AppendLine();
}
