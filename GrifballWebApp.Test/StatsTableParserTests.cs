using System.Text.Json;
using static GrifballWebApp.Test.HistoricalXlsx;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Infrastructure.Historical.Parsing;
using GrifballWebApp.Infrastructure.Historical.Sources;

namespace GrifballWebApp.Test;

/// <summary>
/// A database table of stats (StatsTableParser) split into seasons, divisions and phases: the grifball.info backup's
/// catalog entry against a small workbook shaped like it (HistoricalXlsx.GrifballInfo), never the real one.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StatsTableParserTests
{
    private const string SourceId = "history-grifball-info-db";

    private static HistoricalSourceConfig CatalogConfig()
        => HistoricalSourceCatalog.Sources.Single(s => s.HistoricalSourceID == SourceId).Config!;

    private static HistoricalArchiveBuild Build(HistoricalSourceConfig? config = null, byte[]? workbook = null)
        => HistoricalArchiveBuilder.Build([new HistoricalSourceContent(
            HistoricalTestData.Source(SourceId, "https://example.org/grifball-info", (config ?? CatalogConfig()).ToJson()),
            1, workbook ?? GrifballInfo())]);

    private static List<ArchiveSeason> Seasons(HistoricalArchiveBuild build) => build.Input.Seasons!.Seasons;

    [Test]
    public void TheCatalogEntry_IsParsed_AnEntryPerSeasonDivisionAndPhase_NamedFromItsConfig()
    {
        var seasons = Seasons(Build());

        Assert.That(seasons.Select(s => (s.SourceSheet, s.SeasonName, s.GameVersion, s.Division, s.IsPlayoffs, s.League)), Is.EqualTo(new[]
        {
            ("hr_pstats#s12_Pro", "Winter League 2011 (Pro)", "reach", "Pro", false, "AGLA"),
            ("hr_pstats#s12_Amateur", "Winter League 2011 (Amateur)", "reach", "Amateur", false, "AGLA"),
            ("hr_pstats#s13", "Spring League 2011", "reach", "Open", false, "AGLA"),
            ("hr_pstats#s13", "Spring League 2011", "reach", "Open", true, "AGLA"),
            ("h4_pstats#s20", "Winter League 2013", "halo4", "Open", false, "AGLA"),
        }));
        Assert.That(seasons.Select(s => s.SourceWorkbook), Is.All.EqualTo(SourceId));
    }

    [Test]
    public void ADivision_IsItsTeamsColumn_AndTheTeamsNameComesFromTheTeamsTab()
    {
        var seasons = Seasons(Build());

        Assert.Multiple(() =>
        {
            Assert.That(seasons[0].Players.Select(p => (p.Gamertag, p.TeamName)), Is.EqualTo(new[] { ("halfbreed105", "Cabs Are Here") }));
            Assert.That(seasons[1].Players.Select(p => (p.Gamertag, p.TeamName)), Is.EqualTo(new[] { ("Hoya16", "Downstait") }));
            Assert.That(seasons[2].Players.Select(p => (p.Gamertag, p.TeamName)), Is.EqualTo(new[] { ("PriestXYZ", "Way to Fail"), ("runNOKYARDrun", "Red Ray") }));
            Assert.That(seasons[3].Players.Select(p => (p.Gamertag, p.Kills)), Is.EqualTo(new[] { ("PriestXYZ", (decimal?)80) }), "The playoffs' row");
        });
    }

    [Test]
    public void ARow_IsReadAsASeasonStatsTabsRow_GamesAreWinsAndLosses_AndTheExtraColumnsAreKept()
    {
        var player = Seasons(Build())[0].Players.Single();

        Assert.Multiple(() =>
        {
            Assert.That((player.GamesPlayed, player.Wins, player.Losses, player.Goals, player.Kills, player.Deaths, player.Assists),
                Is.EqualTo(((decimal?)8, (decimal?)5, (decimal?)3, (decimal?)3, (decimal?)553, (decimal?)602, (decimal?)28)));
            Assert.That((player.KdRatio, player.KdSpread, player.TotalSecondsPlayed, player.MultiKills, player.Betrayals, player.StintIndex),
                Is.EqualTo(((decimal?)0.919m, (decimal?)(-49), (decimal?)10083, (decimal?)120, (decimal?)26, 1)));
            Assert.That(player.ExtendedMetrics!.Value.GetProperty("battingAverage").GetDecimal(), Is.EqualTo(45.628m));
            Assert.That(player.ExtendedMetrics!.Value.GetProperty("oneId").GetString(), Is.EqualTo("4"), "An ID stays text");
        });
        var hoya = Seasons(Build())[1].Players.Single();
        Assert.That(hoya.ExtendedMetrics!.Value.TryGetProperty("oneId", out _), Is.False, "NULL is no value");
    }

    [Test]
    public void PerGameRows_AreAddedUp_TheirGamesCounted_RatesWorkedOutAgain_AndATradeIsASecondStint()
    {
        var halo4 = Seasons(Build()).Single(s => s.GameVersion == "halo4");
        var crusader = halo4.Players.Where(p => p.Gamertag == "Crusader0102").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(halo4.Players.Select(p => (p.Gamertag, p.TeamName, p.StintIndex)), Is.EqualTo(new[]
            {
                ("Crusader0102", "Four Guys One Ball", 1), ("TrueFlyingCow", "Four Guys One Ball", 1), ("Crusader0102", "Hybrid Theory", 2),
            }), "Gamertags from h_players, teams from h_teams");
            Assert.That((crusader[0].GamesPlayed, crusader[0].Wins, crusader[0].Losses, crusader[0].Goals, crusader[0].Kills, crusader[0].Deaths, crusader[0].TotalSecondsPlayed),
                Is.EqualTo(((decimal?)2, (decimal?)1, (decimal?)1, (decimal?)2, (decimal?)55, (decimal?)50, (decimal?)1100)));
            Assert.That((crusader[0].KdRatio, crusader[0].KdSpread, crusader[0].MultiKills), Is.EqualTo(((decimal?)1.1m, (decimal?)5, (decimal?)90)),
                "kd is worked out from the totals, not added up; spread was NULL");
            Assert.That(crusader[0].ExtendedMetrics, Is.Null, "A rate isn't kept from added-up rows");
            Assert.That((crusader[1].GamesPlayed, crusader[1].Kills), Is.EqualTo(((decimal?)1, (decimal?)28)));
        });
    }

    [Test]
    public void RowsOfAnUnknownSeason_WithoutADivision_OrWithoutAGamertag_AreSkipped_AndReported()
    {
        var build = Build();

        Assert.Multiple(() =>
        {
            Assert.That(build.Warnings, Is.EquivalentTo(new[]
            {
                $"{SourceId}: hr_pstats: 2 row(s) of season -1, which its config doesn't name, skipped",
                $"{SourceId}: hr_pstats: 1 row(s) of season 12 without one of its divisions, skipped",
                $"{SourceId}: hr_pstats: 1 row(s) without a gamertag, skipped",
            }));
            Assert.That(Seasons(build).SelectMany(s => s.Players).Select(p => p.Gamertag), Has.None.EqualTo("Twizzted Status").And.None.EqualTo("Limbo Larry"));
        });
    }

    [Test]
    public void ATablesOwnColumns_LookupsByAnotherColumn_TheSourcesDefaults_AndRowsMissingTheirParts()
    {
        var workbook = Workbook(
            ("stats", [
                Row("season", "pid", "tid", "Player", "Team", "Games", "Wins", "Losses", "Kills", "Deaths"),
                Row(null, 1, 1, "No Season", null, 1, 1, 0, 5, 5),
                Row(1, 2, "NULL", "NULL", "NULL", 3, 2, 1, 10, 5),
                Row(1, 3, 1, "Direct", "Own Team", 4, 3, 1, 20, 10),
                Row(1, 4, 1, "NULL", null, 2, 1, 1, 8, 8),
                Row(2, 5, 99, "No Team Row", null, 2, 1, 1, 8, 8),
                Row(1, 6, 1, "Short")]),
            ("people", [Row("id", "name"), Row(2, "Looked Up")]),
            ("squads", [Row("tid", "tname", "tier"), Row(1, "Squad One", "A")]));
        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "unused", League = "Late League", Division = "Open", Format = "LateLeague" },
            Tables =
            [
                new()
                {
                    Sheet = "stats", Game = "halo3",
                    Players = new() { Sheet = "people", Key = "id", Column = "name", RowKey = "pid" },
                    Teams = new() { Sheet = "squads", Key = "tid", Column = "tname" }, DivisionColumn = "tier",
                    Seasons = new() { ["1"] = new() { Name = "Late League Season 1" }, ["2"] = new() { Name = "Late League Season 2", Divisions = new() { ["A"] = "Pro" } } },
                },
            ],
        };

        var build = Build(config, workbook);
        var season = Seasons(build).Single();

        Assert.Multiple(() =>
        {
            Assert.That((season.SeasonName, season.League, season.Division, season.Format), Is.EqualTo(("Late League Season 1", "Late League", "Open", "LateLeague")),
                "The source's season defaults");
            Assert.That(season.Players.Select(p => (p.Gamertag, p.TeamName, p.GamesPlayed)), Is.EqualTo(new[]
            {
                ("Looked Up", (string?)null, (decimal?)3), ("Direct", "Own Team", (decimal?)4), ("Short", "Squad One", (decimal?)0),
            }), "The gamertag looked up by pid; the table's own team and games columns first; a short row's missing cells empty");
            Assert.That(build.Warnings, Is.EquivalentTo(new[]
            {
                $"{SourceId}: stats: 1 row(s) of season (none), which its config doesn't name, skipped",
                $"{SourceId}: stats: 1 row(s) of season 2 without one of its divisions, skipped",
                $"{SourceId}: stats: 1 row(s) without a gamertag, skipped",
            }));
        });
    }

    [Test]
    public void AMissingSheet_OrColumn_OrLookup_IsReported_AndTheRestIsRead()
    {
        var config = new HistoricalSourceConfig
        {
            Tables =
            [
                new() { Sheet = "missing", Game = "reach", Seasons = new() { ["1"] = new() { Name = "S" } } },
                new() { Sheet = "hr_pstats", Game = "reach", SeasonColumn = "season_id", Seasons = new() { ["12"] = new() { Name = "S" } } },
                new() { Sheet = "hr_pstats", Game = "reach", Teams = new() { Sheet = "nope", Key = "teamid", Column = "teamname" }, Seasons = new() { ["13"] = new() { Name = "Spring League 2011" } } },
                new() { Sheet = "hr_pstats", Game = "reach", Teams = new() { Sheet = "gb_teamindex", Key = "nokey", Column = "teamname" }, Seasons = new() { ["14"] = new() { Name = "S" } } },
                new() { Sheet = "hr_pstats", Game = "reach", Teams = new() { Sheet = "gb_teamindex", Key = "teamid", Column = "nocolumn" }, Seasons = new() { ["14"] = new() { Name = "S" } } },
                new() { Sheet = "hr_pstats", Game = "reach", Players = new() { Sheet = "gb_teamindex", Key = "teamid", Column = "teamname", RowKey = "norowkey" }, Seasons = new() { ["14"] = new() { Name = "S" } } },
            ],
        };

        var build = Build(config);

        Assert.Multiple(() =>
        {
            Assert.That(build.Warnings, Does.Contain($"{SourceId}: no sheet 'missing', or it is empty"));
            Assert.That(build.Warnings, Does.Contain($"{SourceId}: hr_pstats has no 'season_id' column"));
            Assert.That(build.Warnings, Does.Contain($"{SourceId}: no 'teamname' by 'teamid' in sheet 'nope'"));
            Assert.That(build.Warnings, Does.Contain($"{SourceId}: no 'teamname' by 'nokey' in sheet 'gb_teamindex'"));
            Assert.That(build.Warnings, Does.Contain($"{SourceId}: no 'nocolumn' by 'teamid' in sheet 'gb_teamindex'"));
            Assert.That(build.Warnings, Does.Contain($"{SourceId}: no 'teamname' by 'teamid' in sheet 'gb_teamindex'"), "The stats tab has no norowkey column");
            Assert.That(Seasons(build).Single(s => s.SeasonName == "Spring League 2011").Players.Select(p => (p.Gamertag, p.TeamName)), Is.EqualTo(new[] { ("PriestXYZ", (string?)null), ("runNOKYARDrun", null) }),
                "Without the teams tab, no team; one season of the table (no playoffs column: every row the regular season's), the gamertag-less row skipped");
        });
    }

    [Test]
    public void BuildingAgain_GivesTheSameArchive()
    {
        var first = JsonSerializer.Serialize(Build().Input.Seasons);
        var again = JsonSerializer.Serialize(Build().Input.Seasons);

        Assert.That(again, Is.EqualTo(first));
    }

    [Test]
    public void TheCatalogEntrysConfig_RoundTripsThroughItsJson()
    {
        var config = HistoricalSourceConfig.FromJson(CatalogConfig().ToJson())!;

        Assert.Multiple(() =>
        {
            Assert.That(config.Tables.Select(t => (t.Sheet, t.Game, t.PerGame)), Is.EqualTo(new[] { ("hr_pstats", "reach", false), ("h4_pstats", "halo4", true) }));
            Assert.That(config.Tables[0].Seasons.Keys, Is.EqualTo(new[] { "12", "13", "14", "15", "16", "17", "18" }), "In order");
            Assert.That(config.Tables[0].Seasons["12"].Divisions, Is.EqualTo(new Dictionary<string, string> { ["1"] = "Pro", ["0"] = "Amateur" }));
        });
    }
}
