using System.Text;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.HistoricalImport.Test;

/// <summary>
/// The archive imported whole: what every imported season should have. Each test names the seasons that fall short, so
/// a failure is the list of what to fix. <see cref="Summary_IsWritten"/> writes each season's counts beside the copy.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ArchiveImportTests
{
    private sealed record SeasonCounts(string Name, string SourceID, string Format, int Teams, int Captains, int Stats, int PlayoffStats,
        int Matches, int PlayoffMatches, int Titles);

    private sealed record LateLeagueCounts(string Name, string? SourceID, int Stats, int Games);

    // Read once: the database doesn't change after the import
    private static readonly Lazy<Task<List<SeasonCounts>>> SeasonsRead = new(async () =>
    {
        await using var context = Archive.NewContext();
        return await context.Seasons.AsNoTracking().Where(s => s.HistoricalSourceID != null)
            .OrderBy(s => s.SeasonStart).ThenBy(s => s.SeasonName)
            .Select(s => new SeasonCounts(s.SeasonName, s.HistoricalSourceID, s.Format.ToString(), s.Teams.Count, s.Teams.Count(t => t.CaptainID != null),
                context.PlayerSeasonStats.Count(p => p.SeasonID == s.SeasonID), context.PlayerSeasonStats.Count(p => p.SeasonID == s.SeasonID && p.IsPlayoffs),
                s.SeasonMatches.Count, s.SeasonMatches.Count(m => m.BracketMatch != null), s.PlayerChampionships.Count))
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

    [Test]
    public async Task EveryTeam_HasACaptain()
    {
        Assert.That((await Seasons()).Where(s => s.Captains < s.Teams).Select(s => $"{s.Name}: {s.Captains} of {s.Teams}"), Is.Empty);
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
        Assert.That((await LateLeagueRead.Value).Where(s => s.Stats == 0 || s.Games == 0).Select(s => $"{s.Name}: {s.Stats} stats, {s.Games} games"), Is.Empty);
    }

    /// <summary>Not a check: each season's counts, in <c>summary.md</c> beside the copy, to compare runs.</summary>
    [Test]
    public async Task Summary_IsWritten()
    {
        var summary = new StringBuilder("## Seasons\n\n| Season | Source | Format | Teams | Captains | Stats | Playoff stats | Matches | Playoff matches | Titles |\n|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (var s in await Seasons())
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Format} | {s.Teams} | {s.Captains} | {s.Stats} | {s.PlayoffStats} | {s.Matches} | {s.PlayoffMatches} | {s.Titles} |");
        summary.AppendLine().AppendLine("## Late League").AppendLine().AppendLine("| Season | Source | Stats | Games |").AppendLine("|---|---|---|---|");
        foreach (var s in await LateLeagueRead.Value)
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Stats} | {s.Games} |");
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

    private static void Section(StringBuilder summary, string title, IEnumerable<string> lines)
        => summary.AppendLine().AppendLine($"## {title}").AppendLine().AppendJoin('\n', lines.Select(l => $"- {l}")).AppendLine();
}
