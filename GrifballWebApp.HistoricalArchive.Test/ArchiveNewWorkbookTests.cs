using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.HistoricalArchive.Test;

/// <summary>
/// The workbooks #511's gap 7 gave a config (#522), imported from the real archive: the SL20 console seasons' teams from
/// their Rosters and Standings tabs, the DJBluePDX Summer Series' stats and games, and the champions record's Spring League
/// 2020 titles on the seasons they belong to. The numbers are the sheets'.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ArchiveNewWorkbookTests
{
    private sealed record TeamRow(string Name, int? Rank, int? Wins, int? Losses, string? Captain);

    private static readonly TeamRow[] Halo5Console =
    [
        new("Junkyard Dogs", 1, 8, 0, "Skiyttles"), new("High Voltage", 2, 5, 3, "Cavalier IV"), new("Endgame", 3, 5, 3, "TcT Avenger"),
        new("Legacy", 4, 5, 3, "Reyas"), new("Behind Enemy Lines", 5, 5, 3, "TcT Jbomb"), new("Impulse", 6, 3, 5, "MsMeowlla"),
        new("Power Outage", 7, 3, 5, "Activehate"), new("Oblivion", 8, 1, 7, "KindredDaedric"), new("Lag Spike", 9, 0, 8, "Stop Killing Me"),
    ];

    // The standings' "2 Hypre Crew" is the rosters' 2 Hype Crew; No Way Out's captain is "Ire Paroxysm (Hide)" on the sheet
    private static readonly TeamRow[] ReachConsole =
    [
        new("No Way Out", 1, 5, 0, "Ire Paroxysm"), new("Zero Gravity", 2, 5, 0, "xStarBlade"), new("Gaming So Hard", 3, 4, 2, "Elementary"),
        new("Junkyard Dogs", 4, 3, 2, "Grunt Padre"), new("Power Outage", 5, 2, 2, "Activehate"), new("2 Hype Crew", 6, 2, 3, "WreckinNerds"),
        new("Rule #76", 7, 0, 7, "Enfright"), new("Oblivion", 8, 0, 7, "IceyTanker"),
    ];

    private static readonly string[] DjBlueHalo3Teams =
    [
        "8th Place", ":POGGIES:", "Adventure Awaits!", "BOGO Margs", "Dynasty", "Eggdogs", "Halo 3 Sucks", "Jupiter Skock", "JYC", "Nightfall",
        "Overrated", "PiE was Silva's fault", "Twitch Prime",
    ];

    private static readonly (string, string, string, int)[] Sl20Titles =
    [
        ("2020-sl20-console-reach/Rosters", "Console", "Zero Gravity", 5),
        ("side-reach-pc-stats/Team & Player Stats", "PC", "Free Ship", 6),
    ];

    private static readonly (string, string, int, int, int, int, int, int?)[] DjBlueLines =
    [
        ("Fanciest Pants", "Jupiter Skock", 4, 2, 2, 253, 202, 2040),
        ("oDhmon", "Twitch Prime", 3, 1, 2, 291, 225, 3360),
        ("Fanciest Pants", "Like Clockwork", 2, 0, 2, 184, 173, 2364),
        ("oDhmon", "Eurostars", 4, 2, 2, 938, 765, 12414),
    ];

    private static readonly string[] DjBlueReachTeams =
    [
        "BOGO Margs", "Candice", "EU Squad w/o EUs", "Eurostars", "Fat Mav", "Goon Squad", "Goose's Girls", "Like Clockwork", "Minor Threat",
        "Noble Team", "Originals", "Salvatores", "Spam Folder",
    ];

    /// <summary>A source this copy has; the test is ignored for a copy without it (Copy_HasEverySourceTheImportReads says so).</summary>
    private static void Needs(params string[] sources)
    {
        var missing = sources.Where(s => !Archive.Snapshots.Manifest.ContainsKey(s)).ToList();
        if (missing.Count > 0)
            Assert.Ignore($"Not in this copy: {string.Join(", ", missing)}");
    }

    private static async Task<List<TeamRow>> Teams(string sourceId)
    {
        await using var context = Archive.NewContext();
        var teams = await context.Teams.AsNoTracking().Where(t => t.Season.HistoricalSourceID == sourceId)
            .Select(t => new { t.TeamName, t.ArchivedRank, t.RecordedWins, t.RecordedLosses, Captain = t.Captain.User.UserName })
            .ToListAsync();
        // A placeholder is named after the first spelling of its gamertag the import met ("ActiveHate" in another season)
        return [.. teams.OrderBy(t => t.ArchivedRank ?? int.MaxValue).ThenBy(t => t.TeamName, StringComparer.Ordinal)
            .Select(t => new TeamRow(t.TeamName, t.ArchivedRank, t.RecordedWins, t.RecordedLosses,
                t.Captain is { } name && name.StartsWith(User.ImportPlaceholderPrefix, StringComparison.Ordinal) ? name[User.ImportPlaceholderPrefix.Length..] : t.Captain))];
    }

    private static void AssertTeams(List<TeamRow> actual, TeamRow[] expected)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(actual.Select(t => (t.Name, t.Rank, t.Wins, t.Losses)), Is.EqualTo(expected.Select(t => (t.Name, t.Rank, t.Wins, t.Losses))),
                "Each team in its Standings order, with its record");
            Assert.That(actual.Select(t => t.Captain?.ToUpperInvariant()), Is.EqualTo(expected.Select(t => t.Captain?.ToUpperInvariant())),
                "Each team's captain from the Rosters tab");
        }
    }

    [Test]
    public async Task Sl20ConsoleHalo5_HasItsNineTeams_RankedWithTheirRecordsAndCaptains()
    {
        Needs("2020-sl20-console-h5");

        AssertTeams(await Teams("2020-sl20-console-h5/Rosters"), Halo5Console);
    }

    [Test]
    public async Task Sl20ConsoleReach_HasItsEightTeams_RankedWithTheirRecordsAndCaptains()
    {
        Needs("2020-sl20-console-reach");

        AssertTeams(await Teams("2020-sl20-console-reach/Rosters"), ReachConsole);
    }

    /// <summary>Each scored game of the Schedule calendar ("9:30pm: Impulse (5) vs. High Voltage (1)") is a match between two
    /// of the season's teams: 27 in Halo 5's, 12 in Reach's.</summary>
    [TestCase("2020-sl20-console-h5", 27)]
    [TestCase("2020-sl20-console-reach", 12)]
    public async Task Sl20Console_ImportsItsScheduleResults_BetweenItsTeams(string source, int games)
    {
        Needs(source);

        await using var context = Archive.NewContext();
        var matches = await context.SeasonMatches.AsNoTracking().Where(m => m.Season.HistoricalSourceID == source + "/Rosters")
            .Select(m => new { m.HomeTeamID, m.AwayTeamID }).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches, Has.Count.EqualTo(games));
            Assert.That(matches.Where(m => m.HomeTeamID == null || m.AwayTeamID == null), Is.Empty, "Each side one of the season's teams");
        }
    }

    [Test]
    public async Task Sl20Titles_TheConsoleTitleIsReachConsoles_ThePcTitleReachPcs_AndNoTitlesOnlySeasonIsLeft()
    {
        Needs("2020-sl20-console-reach", "side-reach-pc-stats", "history-agla-champions");
        await using var context = Archive.NewContext();

        // From the titles' side, joined to their season: SelectMany over a season's titles needs APPLY, which SQLite lacks
        var titles = await context.Set<PlayerChampionship>().AsNoTracking().Where(c => c.Season.SeasonName.StartsWith("Spring League 2020"))
            .Select(c => new { c.Season.HistoricalSourceID, c.Tier, c.Team.TeamName })
            .ToListAsync();
        var summary = titles.GroupBy(t => (t.HistoricalSourceID, t.Tier, t.TeamName)).Select(g => (g.Key.HistoricalSourceID, g.Key.Tier, g.Key.TeamName, g.Count()))
            .OrderBy(t => t.HistoricalSourceID, StringComparer.Ordinal).ToList();

        Assert.That(summary, Is.EqualTo(Sl20Titles), "Zero Gravity's five on Reach Console, Free Ship's six on Reach PC; the titles-only Spring League 2020 is gone");
    }

    [TestCase("2021-side-djblue-h3-stats")]
    [TestCase("2021-side-djblue-reach-stats")]
    public async Task DjBlueSummerSeries_HasItsThirteenTeams_FourPlayersEach_AndTwentyFourGames(string source)
    {
        Needs(source);
        await using var context = Archive.NewContext();
        var sourceId = $"{source}/Team Statistics";

        var teams = await context.Teams.AsNoTracking().Where(t => t.Season.HistoricalSourceID == sourceId).Select(t => t.TeamName).ToListAsync();
        var stats = await context.PlayerSeasonStats.AsNoTracking().Where(p => p.Season.HistoricalSourceID == sourceId)
            .GroupBy(p => p.Team.TeamName).Select(g => g.Count()).ToListAsync();
        var matches = await context.Seasons.AsNoTracking().Where(s => s.HistoricalSourceID == sourceId).Select(s => s.SeasonMatches.Count).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams, Is.EquivalentTo(source.Contains("h3") ? DjBlueHalo3Teams : DjBlueReachTeams));
            Assert.That(stats, Has.Count.EqualTo(13).And.All.EqualTo(4), "Each team's four players, on their team");
            // 24 game blocks each, all different games (a rematch has its own host and box score). The one without a box score
            // (Halo 3: Nightfall over 8th Place, its players with no stats; Reach: EU Squad w/o EUs over Goose's Girls, no players)
            // is a game from its title, one the Team Statistics count too (Nightfall's 5 games, Goose's Girls' 4)
            Assert.That(matches, Is.EqualTo(24));
        }
    }

    [Test]
    public async Task DjBlueSummerSeries_APlayersLine_IsTheSheets_LossesTheGamesNotWon_MinutesNotTheMedalSections()
    {
        Needs("2021-side-djblue-h3-stats", "2021-side-djblue-reach-stats");
        await using var context = Archive.NewContext();

        var lines = await context.PlayerSeasonStats.AsNoTracking()
            .Where(p => p.Season.HistoricalSourceID!.StartsWith("2021-side-djblue-") && (p.Gamertag == "oDhmon" || p.Gamertag == "Fanciest Pants"))
            .Select(p => new { p.Season.HistoricalSourceID, p.Gamertag, p.Team.TeamName, p.GamesPlayed, p.Wins, p.Losses, p.Kills, p.Deaths, p.TotalSecondsPlayed })
            .ToListAsync();

        // Fanciest Pants played 34 minutes of Halo 3, 14 of them with medals recorded: the medal section's Minutes is not read
        Assert.That(lines.OrderBy(l => l.HistoricalSourceID, StringComparer.Ordinal).ThenBy(l => l.Gamertag, StringComparer.Ordinal)
            .Select(l => (l.Gamertag, l.TeamName, l.GamesPlayed, l.Wins, l.Losses, l.Kills, l.Deaths, l.TotalSecondsPlayed)), Is.EqualTo(DjBlueLines));
    }
}
