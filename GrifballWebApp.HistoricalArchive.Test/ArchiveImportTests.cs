using System.Text;
using GrifballWebApp.Application.Historical;
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
        int Matches, int PlayoffMatches, int Titles);

    private sealed record LateLeagueCounts(string Name, string? SourceID, int Stats, int Games, int Ladder);

    // Read once: the database doesn't change after the import
    private static readonly Lazy<Task<List<SeasonCounts>>> SeasonsRead = new(async () =>
    {
        await using var context = Archive.NewContext();
        return await context.Seasons.AsNoTracking().Where(s => s.HistoricalSourceID != null)
            .OrderBy(s => s.SeasonStart).ThenBy(s => s.SeasonName)
            .Select(s => new SeasonCounts(s.SeasonName, s.HistoricalSourceID, s.Format.ToString(), s.Teams.Count, s.Teams.Count(t => t.CaptainID != null),
                s.Teams.Count(t => t.ArchivedRank != null), s.Teams.Count(t => t.ArchivedFinish != null),
                context.PlayerSeasonStats.Count(p => p.SeasonID == s.SeasonID), context.PlayerSeasonStats.Count(p => p.SeasonID == s.SeasonID && p.IsPlayoffs),
                s.SeasonMatches.Count, s.SeasonMatches.Count(m => m.BracketMatch != null), s.PlayerChampionships.Count))
            .ToListAsync();
    });

    private static readonly Lazy<Task<List<LateLeagueCounts>>> LateLeagueRead = new(async () =>
    {
        await using var context = Archive.NewContext();
        return await context.MatchmakingSeasons.AsNoTracking().Where(s => s.HistoricalSourceID != null)
            .OrderBy(s => s.StartsAt).ThenBy(s => s.Name)
            .Select(s => new LateLeagueCounts(s.Name, s.HistoricalSourceID, s.PlayerStats.Count, s.MatchedMatches.Count, s.LadderRanks.Count))
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

    [Test]
    public async Task EveryLateLeagueSeason_HasStatsAndGames()
    {
        // A season only its ladder records (the early seasons, kept before the stats sheets) has neither
        Assert.That((await LateLeagueRead.Value).Where(s => (s.Stats == 0 || s.Games == 0) && !(s.Stats == 0 && s.Games == 0 && s.Ladder > 0))
            .Select(s => $"{s.Name}: {s.Stats} stats, {s.Games} games"), Is.Empty);
    }

    /// <summary>
    /// Every ladder tab the catalog names is read onto its season, and each of its players has a row on that season's
    /// ladder: the ladder's rows less those the import reported as a second row for the same player.
    /// </summary>
    [Test]
    public async Task EveryLadderTable_ImportsItsPlayers()
    {
        var expected = Archive.Build.Input.Seasons.Seasons.Where(s => s.Ladder.Count > 0)
            .GroupBy(s => HistoricalKeys.SeasonSourceId(s.SourceWorkbook!, s.SourceSheet!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Ladder.Count), StringComparer.OrdinalIgnoreCase);
        var ladders = (await LateLeagueRead.Value).Where(s => s.SourceID is not null).ToDictionary(s => s.SourceID!, s => s.Ladder, StringComparer.OrdinalIgnoreCase);
        var tabs = ArchiveSnapshots.ImportedEntries().Where(e => Archive.Snapshots.Manifest.ContainsKey(e.HistoricalSourceID))
            .SelectMany(e => e.Config!.Ladders.Select(t => $"{e.HistoricalSourceID}: ladder '{t.Sheet}'"));

        int SecondRows(string key) => Archive.Report.Skipped.Count(s => s.StartsWith($"{key}: ", StringComparison.Ordinal)
            && s.EndsWith("is a second ladder row for the same player", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Archive.Build.Warnings.Where(w => tabs.Any(t => w.StartsWith(t, StringComparison.Ordinal))), Is.Empty, "Every ladder tab is read");
            Assert.That(expected, Has.Count.EqualTo(tabs.Count()), "A season per ladder tab");
            Assert.That(expected.Where(e => ladders.GetValueOrDefault(e.Key) + SecondRows(e.Key) != e.Value)
                .Select(e => $"{e.Key}: {ladders.GetValueOrDefault(e.Key)} rows and {SecondRows(e.Key)} second rows of {e.Value} players"), Is.Empty);
        }
    }

    /// <summary>Not a check: each season's counts, in <c>summary.md</c> beside the copy, to compare runs.</summary>
    [Test]
    public async Task Summary_IsWritten()
    {
        var summary = new StringBuilder("## Seasons\n\n| Season | Source | Format | Teams | Captains | Ranked | Finished | Stats | Playoff stats | Matches | Playoff matches | Titles |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (var s in await Seasons())
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Format} | {s.Teams} | {s.Captains} | {s.Ranked} | {s.Finished} | {s.Stats} | {s.PlayoffStats} | {s.Matches} | {s.PlayoffMatches} | {s.Titles} |");
        summary.AppendLine().AppendLine("## Late League").AppendLine().AppendLine("| Season | Source | Stats | Games | Ladder |").AppendLine("|---|---|---|---|---|");
        foreach (var s in await LateLeagueRead.Value)
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Stats} | {s.Games} | {s.Ladder} |");
        Section(summary, "Placings (the seasons whose standings place a team: rank / finish, record)", await Placings());
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

    private static void Section(StringBuilder summary, string title, IEnumerable<string> lines)
        => summary.AppendLine().AppendLine($"## {title}").AppendLine().AppendJoin('\n', lines.Select(l => $"- {l}")).AppendLine();
}
