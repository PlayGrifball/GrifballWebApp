using System.Text;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Historical.Sources;
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
        int Matches, int PlayoffMatches, int Titles, int Awards, int Mvps, int CaptainsOrdered, int Picks, int PicksNumbered);

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
                s.SeasonMatches.Count, s.SeasonMatches.Count(m => m.BracketMatch != null), s.PlayerChampionships.Count, s.Awards.Count, s.Awards.Count(a => a.Name == "MVP"),
                s.Teams.Count(t => t.Captain != null && t.Captain.DraftCaptainOrder != null),
                s.Teams.SelectMany(t => t.TeamPlayers).Count(p => p.DraftRound != null), s.Teams.SelectMany(t => t.TeamPlayers).Count(p => p.DraftPick != null)))
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

    /// <summary>The seasons (their source IDs' workbook) whose catalog entries name a draft tab that isn't a bid draft's.</summary>
    private static HashSet<string> DraftedWorkbooks() => ArchiveSnapshots.ImportedEntries()
        .SelectMany(e => e.Config!.Drafts.Where(d => d.Order != DraftOrder.None).Select(d => d.Season ?? e.HistoricalSourceID))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Workbook(string sourceID) => sourceID.Split('/')[0];

    /// <summary>A season with a draft board or table: its captains placed in the draft order, and its picks by round.</summary>
    [Test]
    public async Task EverySeasonWithADraft_HasItsCaptainsOrderAndPicks()
    {
        var drafted = DraftedWorkbooks();

        Assert.That((await Seasons()).Where(s => drafted.Contains(Workbook(s.SourceID)) && (s.CaptainsOrdered == 0 || s.Picks == 0))
            .Select(s => $"{s.Name}: {s.CaptainsOrdered} captains ordered, {s.Picks} picks"), Is.Empty);
    }

    /// <summary>A season whose draft board numbers its picks (a snake draft's, or a linear one): its picks keep the numbers.</summary>
    [Test]
    public async Task EverySeasonWithNumberedPicks_KeepsTheirNumbers()
    {
        var numbered = Archive.Build.Input.Seasons.Seasons.Where(e => e.Teams.Any(t => t.Picks.Any(p => p.Pick is not null)))
            .Select(e => e.SourceWorkbook!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(numbered, Is.Not.Empty, "The snake draft board of Summer League 2025 (Classic Pro) numbers its picks");
            Assert.That((await Seasons()).Where(s => numbered.Contains(Workbook(s.SourceID)) && s.PicksNumbered == 0).Select(s => s.Name), Is.Empty);
        }
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

    /// <summary>The seasons of the website databases (grifball.com 2008-2010, grifball.info 2011-2013): each has the games
    /// and forfeits its database kept, as season matches.</summary>
    [Test]
    public async Task EveryWebsiteSeason_HasMatches()
    {
        Assert.That((await Seasons()).Where(s => (s.SourceID.StartsWith("history-grifball-com-db/", StringComparison.Ordinal)
            || s.SourceID.StartsWith("history-grifball-info-db/", StringComparison.Ordinal)) && s.Matches == 0).Select(s => s.Name), Is.Empty);
    }

    /// <summary>What a season with a bracket sheet got of it: its slots and teams beside the sheet's, and the final's winner
    /// beside its titles' team.</summary>
    private sealed record BracketCounts(string Name, int SheetSeries, int Slots, int SheetTeams, int Teams, string? Champion, List<string> TitleTeams);

    private static readonly Lazy<Task<List<BracketCounts>>> BracketsRead = new(async () =>
    {
        await using var context = Archive.NewContext();
        var counts = new List<BracketCounts>();
        foreach (var entry in Archive.Build.Input.Seasons.Seasons.Where(s => s.Bracket is { Series.Count: > 0 }))
        {
            var season = await context.Seasons.AsNoTracking().Where(s => s.HistoricalSourceID != null && s.SeasonName == entry.SeasonName)
                .Select(s => new
                {
                    Slots = s.SeasonMatches.Where(m => m.BracketMatch != null)
                        .Select(m => new { m.HomeTeamID, m.AwayTeamID, m.BracketMatch!.Bracket, m.BracketMatch.RoundNumber, m.BracketMatch.MatchNumber, Winner = m.HomeTeamResult == SeasonMatchResult.Won ? m.HomeTeam!.TeamName : m.AwayTeamResult == SeasonMatchResult.Won ? m.AwayTeam!.TeamName : null })
                        .ToList(),
                    Titles = s.PlayerChampionships.Select(c => c.Team.TeamName).Distinct().ToList(),
                })
                .FirstOrDefaultAsync();
            var sheet = entry.Bracket!.Series;
            // The final: the grand final (its sudden death) in double elimination, else the winners' bracket's last round's slot
            // (by round: a series no slot of the sheet took is numbered after the sheet's, in its own round)
            var doubleElimination = season?.Slots.Any(m => m.Bracket == Bracket.Loser) == true;
            var final = season?.Slots.Where(m => doubleElimination ? m.Bracket is Bracket.GrandFinal or Bracket.GrandFinalSuddenDeath : m.Bracket == Bracket.Winner)
                .OrderBy(m => m.RoundNumber).ThenBy(m => m.MatchNumber).LastOrDefault();
            counts.Add(new BracketCounts(entry.SeasonName, sheet.Count, season?.Slots.Count ?? 0,
                sheet.SelectMany(x => new[] { x.Home, x.Away }).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                season?.Slots.SelectMany(m => new[] { m.HomeTeamID, m.AwayTeamID }).OfType<int>().Distinct().Count() ?? 0,
                final?.Winner, [.. (season?.Titles ?? []).OfType<string>()]));
        }
        return counts;
    });

    /// <summary>A season whose sheets draw its bracket gets it: a slot per series of the sheet, every team it names.</summary>
    [Test]
    public async Task EverySeasonWithABracketSheet_HasItsBracket()
    {
        Assert.That((await BracketsRead.Value).Where(b => b.Slots < b.SheetSeries || b.Teams != b.SheetTeams)
            .Select(b => $"{b.Name}: {b.Slots} slots of the sheet's {b.SheetSeries}, {b.Teams} teams of its {b.SheetTeams}"), Is.Empty);
    }

    /// <summary>A bracket sheet whose final is decided crowns the team the champions record gives the title to.</summary>
    [Test]
    public async Task EveryDecidedBracketSheet_CrownsTheSeasonsChampion()
    {
        Assert.That((await BracketsRead.Value).Where(b => b.Champion is not null && b.TitleTeams.Count > 0 && !b.TitleTeams.Contains(b.Champion, StringComparer.OrdinalIgnoreCase))
            .Select(b => $"{b.Name}: the bracket's {b.Champion}, the titles' {string.Join(" / ", b.TitleTeams)}"), Is.Empty);
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
        var summary = new StringBuilder("## Seasons\n\n| Season | Source | Format | Teams | Captains | Ranked | Finished | Stats | Playoff stats | Matches | Playoff matches | Titles | Awards | Captains ordered | Picks | Picks numbered |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (var s in await Seasons())
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Format} | {s.Teams} | {s.Captains} | {s.Ranked} | {s.Finished} | {s.Stats} | {s.PlayoffStats} | {s.Matches} | {s.PlayoffMatches} | {s.Titles} | {s.Awards} | {s.CaptainsOrdered} | {s.Picks} | {s.PicksNumbered} |");
        summary.AppendLine().AppendLine("## Late League").AppendLine().AppendLine("| Season | Source | Stats | Games |").AppendLine("|---|---|---|---|");
        foreach (var s in await LateLeagueRead.Value)
            summary.AppendLine($"| {s.Name} | {s.SourceID} | {s.Stats} | {s.Games} |");
        Section(summary, "Placings (the seasons whose standings place a team: rank / finish, record)", await Placings());
        Section(summary, "Bracket sheets (slots of the sheet's series, teams of its teams, champion: the bracket's / the titles')",
            (await BracketsRead.Value).Select(b => $"{b.Name}: {b.Slots}/{b.SheetSeries} slots, {b.Teams}/{b.SheetTeams} teams, champion {b.Champion ?? "-"} / {(b.TitleTeams.Count == 0 ? "-" : string.Join(" / ", b.TitleTeams))}"));
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
