using ClosedXML.Excel;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Historical.Parsing;
using GrifballWebApp.Server.Historical.Sources;
using System.Text;
using static GrifballWebApp.Test.HistoricalXlsx;

namespace GrifballWebApp.Test;

/// <summary>The .NET parsers of the archive's sources, against small workbooks built here (never real data).</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalArchiveParsingTests
{
    private static SheetWorkbook Read(byte[] xlsx) => SheetWorkbook.Read(xlsx)!;

    [Test]
    public void AWorkbook_IsReadAsText_LikeTheSheetShowsIt()
    {
        var xlsx = Build(workbook =>
        {
            var sheet = workbook.Worksheets.Add("Stats");
            sheet.Cell(1, 1).Value = "Player";
            sheet.Cell(1, 3).Value = "Avg Life";
            // An empty row is skipped: row indexes count the non-empty ones
            sheet.Cell(3, 1).Value = "Rayzyr";
            sheet.Cell(3, 2).Value = 21d;
            sheet.Cell(3, 3).Value = 25d / 86400;
            sheet.Cell(3, 3).Style.NumberFormat.Format = "\":\"ss";
            sheet.Cell(3, 4).Value = 3725d / 86400;
            sheet.Cell(3, 4).Style.NumberFormat.Format = "[h]:mm:ss";
            sheet.Cell(3, 5).Value = 43493d;
            sheet.Cell(3, 5).Style.NumberFormat.Format = "m/d/yyyy";
            sheet.Cell(3, 6).Value = 12.300000000000001;
            sheet.Cell(3, 7).Value = true;
            sheet.Cell(3, 8).Value = XLError.DivisionByZero;
            sheet.Cell(3, 9).Value = 167049601508048896d;
            var hidden = workbook.Worksheets.Add("Old");
            hidden.Cell(1, 1).Value = "x";
            hidden.Hide();
        });

        var workbook = Read(xlsx);

        var stats = workbook.Sheet("stats ")!;
        Assert.Multiple(() =>
        {
            Assert.That(workbook.Sheets.Select(s => (s.Name, s.Hidden)), Is.EqualTo(new[] { ("Stats", false), ("Old", true) }));
            Assert.That(stats.Rows, Has.Count.EqualTo(2));
            Assert.That(stats.Rows[0], Is.EqualTo(new[] { "Player", "", "Avg Life" }), "No trailing empty cells");
            Assert.That(stats.Rows[1][..8], Is.EqualTo(new[] { "Rayzyr", "21", ":25", "1:02:05", "1/28/2019", "12.3", "TRUE", "#DIV/0!" }));
            Assert.That(stats.Rows[1][8], Does.StartWith("1670496015080"), "A big integer has no exponent");
            Assert.That(stats.Cell(5, 5), Is.Empty);
            Assert.That(workbook.Sheet("Missing"), Is.Null);
        });
    }

    [Test]
    public void ErrorCells_AreWrittenAsTheSheetShowsThem()
    {
        var errors = new[] { XLError.NullValue, XLError.IncompatibleValue, XLError.CellReference, XLError.NameNotRecognized, XLError.NumberInvalid, XLError.NoValueAvailable };
        var xlsx = Build(workbook =>
        {
            var sheet = workbook.Worksheets.Add("Errors");
            for (var i = 0; i < errors.Length; i++)
                sheet.Cell(1, i + 1).Value = errors[i];
        });

        Assert.That(Read(xlsx).Sheet("Errors")!.Rows[0], Is.EqualTo(new[] { "#NULL!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A" }));
    }

    [Test]
    public void ATimestampColumn_OfUnformattedSerials_IsWrittenAsDates()
    {
        var xlsx = Build(workbook =>
        {
            var form = workbook.Worksheets.Add("Form Responses");
            form.Cell(1, 1).Value = "Timestamp";
            form.Cell(1, 2).Value = "Team";
            form.Cell(2, 1).Value = 45000.5;
            form.Cell(2, 2).Value = "Alpha";
            form.Cell(3, 1).Value = "1/2/2023 3:04:05";
            form.Cell(3, 2).Value = "Bravo";
            form.Cell(4, 2).Value = "No timestamp";
            form.Cell(5, 1).Value = 123.25;
            var other = workbook.Worksheets.Add("Other");
            other.Cell(1, 1).Value = "When";
            other.Cell(2, 1).Value = 45000.5;
        });

        var workbook = Read(xlsx);

        Assert.Multiple(() =>
        {
            Assert.That(workbook.Sheet("Form Responses")!.Rows.Select(r => r[0]), Is.EqualTo(new[] { "Timestamp", "3/15/2023 12:00:00", "1/2/2023 3:04:05", "", "123.25" }));
            Assert.That(workbook.Sheet("Other")!.Rows[1][0], Is.EqualTo("45000.5"), "Only a sheet headed Timestamp");
        });
    }

    [TestCase(double.NaN, "NaN")]
    [TestCase(double.PositiveInfinity, "Infinity")]
    public void NonNumbers_AreWrittenRaw(double number, string text)
        => Assert.That(SheetWorkbook.Number(number, "m/d/yyyy", 14), Is.EqualTo(text));

    [TestCase(30d / 86400, "mm:ss", 0, "00:30")]
    [TestCase(0.75, "h:mm am/pm", 0, "6:00 PM")]
    [TestCase(0.75, "h:mm:ss am/pm", 0, "6:00:00 PM")]
    [TestCase(43493.5, "m/d/yyyy h:mm", 0, "1/28/2019 12:00:00")]
    [TestCase(43493d, "m/d", 0, "1/28")]
    [TestCase(43493d, "m-d", 0, "1-28")]
    [TestCase(43493d, "d-m", 0, "28-1")]
    [TestCase(43493d, "m-d-yyyy", 0, "1-28-2019")]
    [TestCase(43493d, "mmmm d", 0, "January 28")]
    [TestCase(43493d, "dddd mmm d", 0, "Monday Jan 28")]
    [TestCase(43493.25, "m/d h:mm am/pm", 0, "1/28 6:00 AM")]
    [TestCase(43493.25, "m/d/yy h:mm am/pm", 0, "1/28/19 6:00 AM")]
    [TestCase(43493d, "General", 14, "1/28/2019")]
    [TestCase(90d / 86400, "\":\"ss", 0, "1:30")]
    [TestCase(-1d / 24, "hh\":\"mm\":\"ss", 0, "-01:00:00")]
    [TestCase(42d, "000000000", 0, "000000042")]
    [TestCase(1.5, null, 0, "1.5")]
    [TestCase(1.659090909, "General", 0, "1.659090909")]
    public void Numbers_AreWrittenByTheirFormat(double number, string? format, int formatId, string text)
        => Assert.That(SheetWorkbook.Number(number, format, formatId), Is.EqualTo(text));

    [Test]
    public void ACsv_IsOneSheet_AndAPage_IsNotAWorkbook()
    {
        var csv = SheetWorkbook.Read(Encoding.UTF8.GetBytes("﻿A,\"b, \"\"c\"\"\"\r\n\r\n1,\"two\nlines\"\n3"), "Game History")!;
        Assert.Multiple(() =>
        {
            Assert.That(csv.Sheets.Single().Name, Is.EqualTo("Game History"));
            Assert.That(csv.Sheets[0].Rows, Is.EqualTo(new[] { new[] { "A", "b, \"c\"" }, new[] { "1", "two\nlines" }, new[] { "3" } }));
            Assert.That(SheetWorkbook.Read(Encoding.UTF8.GetBytes("<!doctype html><html></html>")), Is.Null);
            Assert.That(SheetWorkbook.Read(Encoding.UTF8.GetBytes("x"))!.Sheets[0].Name, Is.EqualTo("Sheet1"));
        });
    }

    [TestCase("1,234", 1234)]
    [TestCase(" 7 ", 7)]
    [TestCase("-", null)]
    [TestCase("#N/A", null)]
    [TestCase("", null)]
    [TestCase("abc", null)]
    public void Number(string text, int? number) => Assert.That(ArchiveCells.Number(text), Is.EqualTo((decimal?)number));

    [TestCase("1:02:05", "Time", 3725)]
    [TestCase("2:05", "Time", 125)]
    [TestCase("42.21", "Minutes", 2533)]
    [TestCase("90", "Seconds Played", 90)]
    [TestCase("", "Time", null)]
    [TestCase("x", "Time", null)]
    public void DurationSeconds(string text, string header, int? seconds)
        => Assert.That(ArchiveCells.DurationSeconds(text, header), Is.EqualTo((decimal?)seconds));

    [Test]
    public void AverageLife_AndGamertagNotes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ArchiveCells.AverageLifeSeconds(":25"), Is.EqualTo(25m));
            Assert.That(ArchiveCells.AverageLifeSeconds("18"), Is.EqualTo(18m));
            Assert.That(ArchiveCells.AverageLifeSeconds(" "), Is.Null);
            Assert.That(ArchiveCells.Gamertag("Rayzyr (c) (to Tuna),"), Is.EqualTo(("Rayzyr", (string?)"to Tuna", (string?)"c")));
            Assert.That(ArchiveCells.Gamertag(null), Is.EqualTo(("", (string?)null, (string?)null)));
        });
    }

    [Test]
    public void SeasonStats_AreOneEntryPerTab_WithStintsNotesAndDefaults()
    {
        var workbook = Read(Workbook(
            ("Stats", [Row("Week totals"), Row("Player", "Team", "Games", "Kills", "Deaths", "K/D", "Avg Life", "Minutes", "Hammer Kills", "Time"),
                Row("Rayzyr (c)", "Tuna", 4, 40, 20, null, ":25", 42, 7, "1:00"),
                Row("Shadow (to Tuna)", "Bandits", 2, 10, 0),
                Row("Shadow", "Tuna", 2, 5, 5, 1.5),
                Row("Team Total", "Tuna", 8, 55, 25),
                Row(null, "Tuna", 1, 1, 1)]),
            ("Playoffs", [Row("Team", "", "Kills"), Row("Tuna", "Rayzyr", 3)])));
        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "Winter League 2019", Game = "halo3", Format = "LeagueSeason" },
            Tabs = [new() { Name = "Stats", HeaderRow = 1 }, new() { Name = "Playoffs", IsPlayoffs = true, PlayerColumn = 1, TeamColumn = 0, CustomSeason = "WL19 Playoffs", Game = "reach" }, new() { Name = "Gone" }],
        };
        var warnings = new List<string>();

        var seasons = SeasonStatsParser.Parse("2019-wl19", config, workbook, warnings);

        var regular = seasons[0];
        Assert.Multiple(() =>
        {
            Assert.That(warnings, Is.EqualTo(new[] { "2019-wl19: no sheet 'Gone', or it is empty" }));
            Assert.That(seasons.Select(s => (s.SeasonName, s.GameVersion, s.League, s.Division, s.SourceSheet, s.IsPlayoffs)), Is.EqualTo(new[]
            {
                ("Winter League 2019", "halo3", "AGLA", (string?)"Open", (string?)"Stats", false),
                ("WL19 Playoffs", "reach", "AGLA", "Open", "Playoffs", true),
            }));
            Assert.That(regular.Format, Is.EqualTo("LeagueSeason"));
            Assert.That(regular.Players.Select(p => (p.Gamertag, p.TeamName, p.StintIndex, p.TradeNote, p.RoleNote)), Is.EqualTo(new[]
            {
                ("Rayzyr", (string?)"Tuna", 1, (string?)null, (string?)"c"),
                ("Shadow", "Bandits", 1, "to Tuna", null),
                ("Shadow", "Tuna", 2, null, null),
            }), "Totals and rows without a player are left out; a gamertag seen again is a later stint");
            var rayzyr = regular.Players[0];
            Assert.That((rayzyr.Kills, rayzyr.Deaths, rayzyr.KdRatio, rayzyr.KdSpread, rayzyr.AverageLifeSeconds, rayzyr.TotalSecondsPlayed, rayzyr.HammerKills),
                Is.EqualTo(((decimal?)40, (decimal?)20, (decimal?)2m, (decimal?)20, (decimal?)25, (decimal?)60, (decimal?)7)), "A later Time column wins over Minutes");
            Assert.That((regular.Players[1].KdRatio, regular.Players[1].Goals, regular.Players[1].Score), Is.EqualTo(((decimal?)null, (decimal?)0, (decimal?)null)),
                "No K/D without deaths; counts default to 0, the rest to unknown");
            Assert.That(regular.Players[2].KdRatio, Is.EqualTo(1.5m), "The sheet's own K/D");
            Assert.That(seasons[1].Players.Single().Gamertag, Is.EqualTo("Rayzyr"), "Read from the configured columns");
        });
    }

    [Test]
    public void Champions_AreOneTitlePerRow_FromTheThirdRow()
    {
        var sheet = Read(Workbook(("AGLA Champions", [Row("AGLA Champions"), Row("Season", "Game", "League", "Team"),
            Row("Winter League 2019:", "MCC Halo 3", "-", "Tuna", "Rayzyr", "Shadow"),
            Row("Summer League 2021", "MCC Halo 3", "Pro", "Bandits", "A"),
            Row("No team", "MCC Halo 3", "Pro")]))).Sheet("AGLA Champions")!;

        var champions = ChampionsParser.Parse("history-agla-champions", "https://example.org/c", sheet);

        Assert.Multiple(() =>
        {
            Assert.That((champions.SourceId, champions.SourceUrl), Is.EqualTo(("history-agla-champions", "https://example.org/c")));
            Assert.That(champions.Champions.Select(c => (c.Season, c.Game, c.Tier, c.Team, c.Captain)), Is.EqualTo(new[]
            {
                ("Winter League 2019", (string?)"MCC Halo 3", "Open", "Tuna", (string?)"Rayzyr"),
                ("Summer League 2021", "MCC Halo 3", "Pro", "Bandits", "A"),
            }));
            Assert.That(champions.Champions[0].Roster, Is.EqualTo(new[] { "Rayzyr", "Shadow" }));
        });
    }

    [Test]
    public void WaypointGames_AreSplitIntoRegularAndPlayoffs_ByTheFirstColumn()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var sheet = Read(Workbook(("Game History", [
            Row("Week 1", $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{a}"),
            Row(null, $"https://halodatahive.com/Infinite/Match/{b} https://haloquery.com/matches/{c}", "notes"),
            Row("Playoffs", $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{c.ToString().ToUpperInvariant()}"),
            Row(null, "https://www.halowaypoint.com/halo-infinite/players/x")]))).Sheet("Game History")!;
        var unparsed = new List<string>();

        var (regular, playoffs) = WaypointMatchParser.Parse(sheet, unparsed);

        Assert.Multiple(() =>
        {
            Assert.That(regular, Is.EqualTo(new[] { a.ToString(), b.ToString(), c.ToString() }));
            Assert.That(playoffs, Is.EqualTo(new[] { c.ToString() }));
            Assert.That(unparsed, Is.EqualTo(new[] { "Game History: https://www.halowaypoint.com/halo-infinite/players/x" }));
            Assert.That(WaypointMatchParser.MatchIds("plain text"), Is.Empty);
        });
    }

    private static readonly object?[] CarnageHeader = Row("Player", "Team", "GamesPlayed", "Wins", "Loses", "Goals", "Kills", "Assists", "Deaths", "Betrayals", "HammerKills", "BombStops", "GameTime", "TimeStamp", "Game");

    private static object?[] Carnage(string player, int team, bool won, int kills, string? stamp, int gameTime = 600, string? game = null)
        => Row(player, team, 1, won ? 1 : 0, won ? 0 : 1, won ? 1 : 0, kills, 2, 9, 0, 5, 1, gameTime, stamp, game);

    [Test]
    public void CarnageGames_AreTheRowsSharingATimestamp_InTheirPhase()
    {
        const string first = "Mon Jul 11 18:02:15 2022";
        const string second = "Sun Nov  6 23:17:48 2022";
        var workbook = Read(Workbook(("Match_History", [
            CarnageHeader,
            Row(null, "Week 1"),
            Carnage("A (c)", 1, true, 40, first), Carnage("B", 1, true, 30, first), Carnage("C", 0, false, 20, first), Carnage("D", 0, false, 10, first, game: "Halo 4"),
            Row("Playoffs Round 1", 0, 0, 0, 0, 0, 0),
            Carnage("A", 0, true, 41, second), Carnage("C", 1, false, 21, second),
            // One side only: a partial copy
            Carnage("Z", 1, true, 5, "Tue Oct 18 07:51:40 2022"),
            Row("Broken", 0, 0, 0, 0, 0, 0)])));
        var warnings = new List<string>();
        var tab = new GamesTab { Sheet = "Match_History", Season = "h3/Stats", Game = "halo3" };

        var games = RecordedGameParser.Parse("h3", tab, workbook, warnings);

        Assert.Multiple(() =>
        {
            Assert.That(warnings, Is.EqualTo(new[] { "h3/Match_History: 1 games with only one side left out" }));
            Assert.That(games.Select(g => (g.SourceKey, g.SeasonSourceId, g.GameVersion, g.PlayedAt, g.IsPlayoffs, g.IsScrim)), Is.EqualTo(new[]
            {
                ("h3/Match_History@20220711180215", "h3/Stats", "halo4", (DateTime?)new DateTime(2022, 7, 11, 18, 2, 15, DateTimeKind.Utc), false, false),
                ("h3/Match_History@20221106231748", "h3/Stats", "halo3", new DateTime(2022, 11, 6, 23, 17, 48, DateTimeKind.Utc), true, false),
            }), "A row's Game column names the game");
            var game = games[0];
            Assert.That(game.Players.Select(p => (p.Gamertag, p.Side, p.Kills, p.Deaths, p.Assists, p.Goals, p.HammerKills, p.CarrierKills, p.Betrayals)), Is.EqualTo(new[]
            {
                ("A", 2, 40, 9, 2, (int?)1, (int?)5, (int?)1, (int?)0), ("B", 2, 30, 9, 2, 1, 5, 1, 0), ("C", 1, 20, 9, 2, 0, 5, 1, 0), ("D", 1, 10, 9, 2, 0, 5, 1, 0),
            }));
            Assert.That(game.Teams.Select(t => (t.Side, t.Outcome, t.Score)), Is.EqualTo(new[] { (1, Outcomes.Lost, (int?)0), (2, Outcomes.Won, (int?)2) }));
        });
    }

    [Test]
    public void CarnageGames_WithoutATimestamp_EndWhenAPlayerOrASideComesBack_OrTheGameTimeJumps()
    {
        var workbook = Read(Workbook(("HR_MatchH", [
            Row("", "Team ID", "Games Played", "Wins", "Losses", "Kills", "Deaths", "Game Time", "TimeStamp"),
            Row("A", 1, 1, 1, 0, 5, 1, 600, 0), Row("B", 0, 1, 0, 1, 4, 2, 600, 0),
            // A comes back: a new game
            Row("A", 1, 1, 1, 0, 6, 1, 610, 0), Row("C", 0, 1, 0, 1, 1, 2, 610, 0),
            // Side 1 comes back after side 0: a new game
            Row("D", 1, 1, 0, 1, 2, 2, 610, 0), Row("E", 0, 1, 1, 0, 3, 2, 610, 0),
            // The game time jumps
            Row("F", 0, 1, 1, 0, 3, 2, 900, 0), Row("G", 1, 1, 0, 1, 3, 2, 900, 0)])));
        var tab = new GamesTab { Sheet = "HR_MatchH", Season = "late-league/S6", Game = "reach", Scrims = true };

        var games = RecordedGameParser.Parse("ll", tab, workbook, []);

        Assert.Multiple(() =>
        {
            Assert.That(games.Select(g => string.Join(",", g.Players.Select(p => p.Gamertag))), Is.EqualTo(new[] { "A,B", "A,C", "D,E", "F,G" }));
            Assert.That(games.Select(g => g.SourceKey), Is.EqualTo(new[] { "ll/HR_MatchH#1", "ll/HR_MatchH#3", "ll/HR_MatchH#5", "ll/HR_MatchH#7" }));
            Assert.That(games.Select(g => (g.PlayedAt, g.IsScrim)), Is.All.EqualTo(((DateTime?)null, true)));
        });
    }

    [Test]
    public void CarnageGames_SharingATimestamp_GetTheirOwnKeys_AndASheetWithoutKillsHasNone()
    {
        const string stamp = "Mon Jul 11 18:02:15 2022";
        var workbook = Read(Workbook(
            ("Twice", [CarnageHeader, Carnage("A", 1, true, 1, stamp), Carnage("B", 0, false, 1, stamp), Carnage("C", 1, true, 1, "Mon Jul 11 18:30:00 2022"), Carnage("D", 0, false, 1, "Mon Jul 11 18:30:00 2022"),
                Carnage("E", 1, true, 1, stamp), Carnage("F", 0, false, 1, stamp)]),
            ("NoKills", [Row("Player", "Team"), Row("A", 1)])));

        var games = RecordedGameParser.Parse("w", new GamesTab { Sheet = "Twice", Season = "w/S", Game = "halo3" }, workbook, []);
        var warnings = new List<string>();

        Assert.Multiple(() =>
        {
            Assert.That(games.Select(g => g.SourceKey), Is.EqualTo(new[] { "w/Twice@20220711180215", "w/Twice@20220711183000", "w/Twice@20220711180215-2" }));
            Assert.That(RecordedGameParser.Parse("w", new GamesTab { Sheet = "NoKills", Season = "w/S", Game = "halo3" }, workbook, warnings), Is.Empty);
            Assert.That(RecordedGameParser.Parse("w", new GamesTab { Sheet = "Missing", Season = "w/S", Game = "halo3" }, workbook, warnings), Is.Empty);
            Assert.That(warnings, Is.EqualTo(new[] { "w: no games sheet 'Missing', or it is empty" }));
        });
    }

    [TestCase("Mon Jul 11 18:02:15 2022", 2022, 7, 11)]
    [TestCase("12/8/2020 17:32:20", 2020, 12, 8)]
    [TestCase("0", null, null, null)]
    [TestCase("", null, null, null)]
    public void Timestamps(string text, int? year, int? month, int? day)
        => Assert.That(RecordedGameParser.Timestamp(text)?.Date, Is.EqualTo(year is null ? (DateTime?)null : new DateTime(year.Value, month!.Value, day!.Value)));

    [Test]
    public void BlockGames_AreATitleThenEachTeamsPlayers()
    {
        object?[] header = Row("Team", "Player", "Games Played", "Wins", "Minutes", "Goals", "Kills", "Assists", "Deaths", "Carrier Kills");
        var workbook = Read(Workbook(("Game Results", [
            Row("Template"), header, Row(null, null, 0, 0), Row("Totals:", null, 0),
            Row("Week 1 - 1/28/19 - Our Lady Tuna over Collateral Damage (5-2) RECORDED https://xboxdvr.com/x"),
            Row(null, null, null, null, null, "Postgame Page 1"),
            header,
            Row("Our Lady Tuna", "imShad (c)", 1, 1, 17.6, 0, 73, 1, 44, 2),
            Row("Our Lady Tuna", "Benched", 0, 0, 0, 0, 0, 0, 0, 0),
            Row("Totals:", null, 1, 1),
            Row("Collateral Damage", "godwit1ivy (c)", 1, 0, 16.5, 1, 56, 0, 62, 1),
            Row("Sub", "Sherbetshake", 1, 0, 7.9, 0, 33, 0, 34, 0),
            Row("Playoffs Round 1 - Tuna tied Damage 5-5"),
            header,
            Row("Tuna", "imShad", 1, 0, 10, 2, 10, 0, 10, 0),
            Row(null, null, 1, 0, 10),
            Row("Damage", "godwit1ivy", 1, 0, 10, 3, 10, 0, 10, 0),
            Row("Week 2 - Forfeit over Nobody - NO STATS"), header])));
        var warnings = new List<string>();

        var games = RecordedGameParser.Parse("wl19", new GamesTab { Sheet = "Game Results", Season = "wl19/Stats", Game = "halo3", Layout = GamesLayout.Blocks }, workbook, warnings);

        Assert.Multiple(() =>
        {
            Assert.That(warnings, Is.EqualTo(new[] { "wl19/Game Results: 1 games with only one side left out" }));
            Assert.That(games.Select(g => (g.SourceKey, g.PlayedAt, g.IsPlayoffs)), Is.EqualTo(new[]
            {
                ("wl19/Game Results#4", (DateTime?)new DateTime(2019, 1, 28, 0, 0, 0, DateTimeKind.Utc), false),
                ("wl19/Game Results#12", null, true),
            }));
            var first = games[0];
            Assert.That(first.Players.Select(p => (p.Gamertag, p.Side, p.Kills, p.Goals, p.CarrierKills)), Is.EqualTo(new[]
            {
                ("imShad", 1, 73, (int?)0, (int?)2), ("godwit1ivy", 2, 56, 1, 1), ("Sherbetshake", 2, 33, 0, 0),
            }), "A player who didn't play is left out; a sub plays for the side above them");
            Assert.That(first.Teams.Select(t => (t.Side, t.Name, t.Outcome, t.Score)), Is.EqualTo(new[]
            {
                (1, (string?)"Our Lady Tuna", Outcomes.Won, (int?)5), (2, "Collateral Damage", Outcomes.Lost, 2),
            }));
            Assert.That(games[1].Teams.Select(t => (t.Outcome, t.Score)), Is.All.EqualTo((Outcomes.Tie, (int?)5)));
        });
    }

    [Test]
    public void APlayersList_IsReadByItsHeaders()
    {
        var sheet = Read(Workbook(("Player Masterlist", [Row("Gamertag", "Discord", "Discord ID", "Location", "System"),
            Row("ACE B", "S1lent", "167049601508048896", "Central", "PC"), Row(null, "nobody"), Row("Solo")]))).Sheet("Player Masterlist")!;

        var players = PlayersListParser.Parse("late-league", sheet);

        Assert.Multiple(() =>
        {
            Assert.That(players.Select(p => (p.Gamertag, p.DiscordUsername, p.DiscordId, p.Region, p.System, p.SourceKey)), Is.EqualTo(new[]
            {
                ("ACE B", (string?)"S1lent", (string?)"167049601508048896", (string?)"Central", (string?)"PC", (string?)"late-league/Player Masterlist"),
                ("Solo", null, null, null, null, "late-league/Player Masterlist"),
            }));
            Assert.That(players[0].DiscordUserId, Is.EqualTo(167049601508048896L));
            Assert.That(PlayersListParser.Parse("x", Read(Workbook(("P", [Row("Name"), Row("a")]))).Sheet("P")!), Is.Empty, "No Gamertag column");
            Assert.That(PlayersListParser.Parse("x", Read(Workbook(("P", [Row("Gamertag")]))).Sheet("P")!), Is.Empty);
        });
    }

    [Test]
    public void ARosterTable_GivesEachPlayerTheirTeam()
    {
        var sheet = Read(Workbook(("TeamInfo", [Row("Seed #", "Team Name", "Player Name", "Location"), Row("Seed 1", "Silly Kids", "Galvann (c)", "West"), Row("Seed 1", "Silly Kids", "e Jolts", "East"), Row("Seed 2", "Never Last", "Invayda Zim", "South")]))).Sheet("TeamInfo")!;

        var teams = RosterParser.Parse(sheet);

        Assert.That(teams, Is.EquivalentTo(new Dictionary<string, string>
        {
            [HistoricalKeys.Normalize("Galvann")] = "Silly Kids", [HistoricalKeys.Normalize("e Jolts")] = "Silly Kids", [HistoricalKeys.Normalize("Invayda Zim")] = "Never Last",
        }));
    }

    [Test]
    public void ADraftSheet_GivesEachPlayerTheirColumnsTeam_SkippingOtherRows_AndAPlayerOnTwoKeepsTheFirst()
    {
        var sheet = Read(Workbook(("Teams", [
            Row("Draft Sheet - presented by someone"),
            Row(null, "SEASON COMPLETE", "Seed 1"),
            Row("Team Name:", "Bears", "Beer"),
            Row("Record", "3-15", "16-2"),
            Row("Captain:", "a Bears Nutz", "Solace VIII"),
            Row("Current Grade", "E", "S"),
            Row("Player 2", "TommyWstSide", "Myst the Cold"),
            Row("1st Round:", "Galvann", "Solace VIII"),
            Row("Draft Pool"),
            Row("Gamertag", null, "Discord"),
            Row("Somebody Else", null, "someone#1")]))).Sheet("Teams")!;

        var teams = RosterParser.Parse(sheet);

        Assert.That(teams.OrderBy(t => t.Value).ThenBy(t => t.Key).Select(t => t.Value), Is.EqualTo(new[] { "Bears", "Bears", "Bears", "Beer", "Beer" }));
        Assert.Multiple(() =>
        {
            Assert.That(teams[HistoricalKeys.Normalize("Solace VIII")], Is.EqualTo("Beer"));
            Assert.That(teams[HistoricalKeys.Normalize("Galvann")], Is.EqualTo("Bears"));
            Assert.That(teams.ContainsKey(HistoricalKeys.Normalize("Somebody Else")), Is.False, "The draft pool is no team's");
        });
    }

    [TestCase("Team", "Gamertag")]
    [TestCase("Team", "Player")]
    [TestCase("Team Names", "Player")]
    public void ARosterTableHeadedTeamAndGamertag_SkipsARowMissingEither(string teamHeader, string playerHeader)
    {
        var sheet = Read(Workbook(("TeamInfo", [Row(teamHeader, playerHeader), Row("Silly Kids", "Galvann"), Row(null, "No Team"), Row("Never Last", null)]))).Sheet("TeamInfo")!;

        var teams = RosterParser.Parse(sheet);

        Assert.That(teams, Is.EquivalentTo(new Dictionary<string, string> { [HistoricalKeys.Normalize("Galvann")] = "Silly Kids" }));
    }

    [Test]
    public void ADraftSheetsBlocksSideBySide_EachKeepTheirTeams_AndABlankRowEndsThem()
    {
        var sheet = Read(Workbook(("Draft", [
            Row("Team Name", "Bears", null, "Team Name", "Beer"),
            Row("Captain", "a Bears Nutz", null, "Captain", "Solace VIII"),
            Row(null, null),
            Row("Player 2", "After The Blank")]))).Sheet("Draft")!;

        var teams = RosterParser.Parse(sheet);

        Assert.That(teams, Is.EquivalentTo(new Dictionary<string, string>
        {
            [HistoricalKeys.Normalize("a Bears Nutz")] = "Bears", [HistoricalKeys.Normalize("Solace VIII")] = "Beer",
        }));
    }

    [Test]
    public void ADraftSheetsNamelessTeams_AreNamedAfterTheirCaptains_EvenWhenTheTeamNameRowIsOnlyItsLabel()
    {
        var sheet = Read(Workbook(("Legacy Draft Sheet", [
            Row(null, "Team Name:"),
            Row(null, "Captain", "ActiveHate", "Sauted Smurf (c)"),
            Row(null, "Player 1", "Havoc Puppies", "MuNgO PuNgO")]))).Sheet("Legacy Draft Sheet")!;

        var teams = RosterParser.Parse(sheet);

        Assert.That(teams, Is.EquivalentTo(new Dictionary<string, string>
        {
            [HistoricalKeys.Normalize("ActiveHate")] = "ActiveHate", [HistoricalKeys.Normalize("Havoc Puppies")] = "ActiveHate",
            [HistoricalKeys.Normalize("Sauted Smurf")] = "Sauted Smurf", [HistoricalKeys.Normalize("MuNgO PuNgO")] = "Sauted Smurf",
        }));
    }

    [Test]
    public void ARosterInAnotherSource_FillsTheTeamOfItsSeasonsPlayersWhoseStatsNameNone()
    {
        List<HistoricalSourceContent> sources =
        [
            Source("stats", new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "Summer League 2025", Game = "infinite" }, Tabs = [new() { Name = "Stats" }] },
                Workbook(("Stats", [Row("Player", "Team", "Games", "Kills"), Row("A", null, 4, 10), Row("B", null, 4, 5), Row("C", "Own Team", 4, 1), Row("D", null, 4, 1)]))),
            Source("draft", new HistoricalSourceConfig { Rosters = [new() { Sheet = "Draft", Season = "stats" }, new() { Sheet = "Missing", Season = "stats" }, new() { Sheet = "Draft", Season = "nothing" }] },
                Workbook(("Draft", [Row("Team Name:", null, "The   Beer"), Row("Captain:", "A", "B"), Row("Player 2", "C", null)]))),
        ];

        var build = HistoricalArchiveBuilder.Build(sources, new HashSet<string> { "draft" });

        var teams = build.Input.Seasons.Seasons.Single().Players.ToDictionary(p => p.Gamertag, p => p.TeamName);
        Assert.Multiple(() =>
        {
            Assert.That(teams, Is.EqualTo(new Dictionary<string, string?> { ["A"] = "A", ["B"] = "The Beer", ["C"] = "Own Team", ["D"] = null }));
            Assert.That(build.Seasons, Is.EquivalentTo(new[] { "stats/Stats" }), "The roster's season is the changed source's");
            Assert.That(build.Warnings, Does.Contain("draft: roster 'Missing' not read (no such sheet)"));
            Assert.That(build.Warnings, Does.Contain("draft: roster 'Draft' not read (no season from nothing)"));
        });
    }

    [Test]
    public void APivotTablesSumOfColumn_IsItsStat()
    {
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "S" }, Tabs = [new() { Name = "Halo Infinite Stats" }] };
        var xlsx = Workbook(("Halo Infinite Stats", [Row("Name", "SUM of Games Played", "SUM of kd ratio2", "Kills"), Row("A", 23, 1.5, 1597)]));

        var player = SeasonStatsParser.Parse("s", config, Read(xlsx), []).Single().Players.Single();

        Assert.That((player.Gamertag, player.GamesPlayed), Is.EqualTo(("A", 23m)));
    }

    [Test]
    public void AStatsTabsTeamColumnOfGameSides_IsNoTeam_TheRosterTabsGiveIt_AndItsTimeStampsDateTheEntry()
    {
        var xlsx = Workbook(
            ("Halo 4 Stats", [
                Row("Player", "Team", "GamesPlayed", "Wins", "Loses", "Kills", "TimeStamp"),
                Row("Galvann", 1, 4, 3, 1, 50, "Tue Oct 18 08:01:46 2022"),
                Row("Invayda Zim", 0, 4, 1, 3, 40, "Mon Oct  17 21:15:00 2022"),
                Row("Sub Player", 1, 1, 0, 1, 5, "not a date")]),
            ("TeamInfo", [Row("Team Name", "Player Name"), Row("Silly Kids", "Galvann"), Row("Never Last", "Invayda Zim")]));
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "GWL Tournament Series #1", League = "GWL", Game = "halo4" }, Tabs = [new() { Name = "Halo 4 Stats" }], RosterSheets = ["TeamInfo", "Missing"] };
        var warnings = new List<string>();

        var season = SeasonStatsParser.Parse("gwl", config, Read(xlsx), warnings).Single();

        Assert.Multiple(() =>
        {
            Assert.That(season.Players.Select(p => (p.Gamertag, p.TeamName)), Is.EqualTo(new[] { ("Galvann", "Silly Kids"), ("Invayda Zim", "Never Last"), ("Sub Player", (string?)null) }));
            Assert.That((season.FirstPlayedAt, season.LastPlayedAt), Is.EqualTo(((DateTime?)new DateTime(2022, 10, 17, 21, 15, 0, DateTimeKind.Utc), (DateTime?)new DateTime(2022, 10, 18, 8, 1, 46, DateTimeKind.Utc))));
            Assert.That(warnings, Is.EqualTo(new[] { "gwl: no sheet 'Missing'" }));
        });
    }

    [Test]
    public void ARealTeamColumn_IsKept_AndTheRosterOnlyFillsAGap()
    {
        var xlsx = Workbook(
            ("Stats", [Row("Player", "Team", "Kills"), Row("Galvann", "Silly Kids", 5), Row("Invayda Zim", null, 4)]),
            ("TeamInfo", [Row("Team Name", "Player Name"), Row("Roster Team", "Galvann"), Row("Never Last", "Invayda Zim")]));
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "S" }, Tabs = [new() { Name = "Stats" }], RosterSheets = ["TeamInfo"] };

        var season = SeasonStatsParser.Parse("s", config, Read(xlsx), []).Single();

        Assert.That(season.Players.Select(p => p.TeamName), Is.EqualTo(new[] { "Silly Kids", "Never Last" }));
        Assert.That((season.FirstPlayedAt, season.LastPlayedAt), Is.EqualTo(((DateTime?)null, (DateTime?)null)), "No TimeStamp column");
    }

    [TestCase("Tue Oct 18 08:01:46 2022", "2022-10-18T08:01:46")]
    [TestCase("Mon Sep  5 02:36:41 2022", "2022-09-05T02:36:41")]
    [TestCase("1/28/2019", "2019-01-28T00:00:00")]
    [TestCase("1999-01-01", null)]
    [TestCase("", null)]
    [TestCase("TRUE", null)]
    public void ATimeStampCell_IsReadAsUtc(string cell, string? expected)
        => Assert.That(SeasonStatsParser.PlayedAt(cell), Is.EqualTo(expected is null ? (DateTime?)null : DateTime.SpecifyKind(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc)));

    [Test]
    public void AWorkbook_ReadForSomeTabs_HasOnlyThose()
    {
        var xlsx = Workbook(("Stats", [Row("Player"), Row("Rayzyr")]), ("Notes", [Row("x")]), (" Champions ", [Row("Season")]));

        var workbook = SheetWorkbook.Read(xlsx, sheets: name => name.Trim() is "Stats" or "Champions")!;

        Assert.Multiple(() =>
        {
            Assert.That(workbook.Sheets.Select(s => s.Name), Is.EqualTo(new[] { "Stats", " Champions " }), "The other tabs are never read");
            Assert.That(workbook.Sheet("Stats")!.Rows, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void ASourcesConfig_NamesEveryTabItsImportReads()
    {
        var config = new HistoricalSourceConfig
        {
            Tabs = [new() { Name = " Stats " }],
            Tables = [new() { Sheet = "hr_pstats", Game = "reach", Players = new() { Sheet = "h_players", Key = "playerid", Column = "gamertag" }, Teams = new() { Sheet = "gb_teamindex", Key = "teamid", Column = "teamname" } }],
            Waypoint = [new() { Sheet = "Match History", Season = "s" }],
            Games = [new() { Sheet = "Game Results", Season = "s/Stats", Game = "halo3" }],
            RosterSheets = ["TeamInfo"],
            Rosters = [new() { Sheet = "Draft Sheet", Season = "s" }],
            ChampionsSheet = "AGLA Champions",
            PlayersSheet = "Player Masterlist",
        };

        var names = config.SheetNames();

        Assert.Multiple(() =>
        {
            Assert.That(names, Is.EquivalentTo(new[] { "Stats", "hr_pstats", "h_players", "gb_teamindex", "Match History", "Game Results", "TeamInfo", "Draft Sheet", "AGLA Champions", "Player Masterlist" }));
            Assert.That(names.Contains("STATS"), Is.True, "Any case, as SheetWorkbook.Sheet finds a tab");
        });
    }

    [Test]
    public void TheBuilder_TakingOneSourceAtATime_BuildsWhatBuildingThemAllAtOnceDoes()
    {
        var builder = new HistoricalArchiveBuilder();
        foreach (var source in Sources())
            builder.Add(source);
        var oneAtATime = builder.Finish();
        var allAtOnce = HistoricalArchiveBuilder.Build(Sources());

        Assert.Multiple(() =>
        {
            Assert.That(System.Text.Json.JsonSerializer.Serialize(oneAtATime.Input), Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(allAtOnce.Input)));
            Assert.That(oneAtATime.Warnings, Is.EqualTo(allAtOnce.Warnings));
        });
    }

    private static HistoricalSourceContent Source(string id, HistoricalSourceConfig? config, byte[] content, int snapshotId = 1)
        => new(new HistoricalSource { HistoricalSourceID = id, Url = $"https://example.org/{id}", Config = config?.ToJson() }, snapshotId, content);

    private static List<HistoricalSourceContent> Sources()
    {
        var match = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var playoff = Guid.Parse("66666666-2222-3333-4444-555555555555");
        return
        [
            Source("stats", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Winter League 2023", Game = "infinite" },
                Tabs = [new() { Name = "Stats" }, new() { Name = "Playoff Stats", IsPlayoffs = true }],
                Games = [new() { Sheet = "Match_History", Season = "stats/Stats", Game = "halo3" }],
            }, Workbook(
                ("Playoff Stats", StatsTab(("A", "Tuna", 3))),
                ("Stats", StatsTab(("A", "Tuna", 10), ("B", "Bandits", 5))),
                ("Match_History", [CarnageHeader, Carnage("A", 1, true, 1, "Mon Jul 11 18:02:15 2022"), Carnage("B", 0, false, 1, "Mon Jul 11 18:02:15 2022")]))),
            Source("schedule", new HistoricalSourceConfig { Waypoint = [new() { Sheet = "Game History", Season = "stats" }, new() { Sheet = "Missing", Season = "stats" }, new() { Sheet = "Game History", Season = "nothing" }] },
                Workbook(("Game History", [
                    Row("Week 1", $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{match}", $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{playoff}"),
                    Row("Finals", $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{playoff}"),
                    Row(null, "https://www.halowaypoint.com/halo-infinite/players/x")]))),
            Source("champions", new HistoricalSourceConfig { ChampionsSheet = "AGLA Champions" },
                Workbook(("AGLA Champions", [Row("x"), Row("y"), Row("Winter League 2023", "Infinite", "-", "Tuna", "A")]))),
            Source("late-league", new HistoricalSourceConfig { PlayersSheet = "Player Masterlist" },
                Workbook(("Player Masterlist", [Row("Gamertag", "Discord ID"), Row("A", "123")]))),
            Source("backup", null, Encoding.UTF8.GetBytes("<html></html>")),
            Source("broken", new HistoricalSourceConfig { ChampionsSheet = "Nope", PlayersSheet = "Nope" }, Workbook(("Other", [Row("x")]))),
            Source("page", new HistoricalSourceConfig(), Encoding.UTF8.GetBytes("<html></html>")),
            Source("corrupt", new HistoricalSourceConfig(), [0x50, 0x4b, 0x03, 0x04, 0x00, 0x01]),
        ];
    }

    [Test]
    public void TheArchive_IsBuiltFromEverySource()
    {
        var build = HistoricalArchiveBuilder.Build(Sources());

        var input = build.Input;
        Assert.Multiple(() =>
        {
            Assert.That(build.Seasons, Is.Null, "A full build is every season");
            Assert.That(input.Seasons.Seasons.Select(s => HistoricalKeys.SeasonSourceId(s.SourceWorkbook!, s.SourceSheet!)), Is.EqualTo(new[] { "stats/Stats", "stats/Playoff Stats" }));
            Assert.That(input.WaypointMatches!.Keys, Is.EqualTo(new[] { "stats/Stats" }), "Under the season's regular-season key");
            Assert.That(input.WaypointMatches["stats/Stats"].Regular, Is.EqualTo(new[] { "11111111-2222-3333-4444-555555555555" }));
            Assert.That(input.WaypointMatches["stats/Stats"].Playoffs, Is.EqualTo(new[] { "66666666-2222-3333-4444-555555555555" }), "Listed as both, it is a playoff game");
            Assert.That(input.Championships!.Champions.Single().Team, Is.EqualTo("Tuna"));
            Assert.That(input.Championships.SourceUrl, Is.EqualTo("https://example.org/champions"));
            Assert.That(input.RecordedGames!.Single().SeasonSourceId, Is.EqualTo("stats/Stats"));
            Assert.That(input.Players!.Single().DiscordId, Is.EqualTo("123"));
            Assert.That(input.Catalog!.Sources.Select(s => s.Id), Does.Contain("stats").And.Contain("backup"));
            Assert.That(build.Warnings, Does.Contain("schedule: Waypoint games of 'Missing' not read (no such sheet)"));
            Assert.That(build.Warnings, Does.Contain("schedule: Waypoint games of 'Game History' not read (no season from nothing)"));
            Assert.That(build.Warnings, Does.Contain("schedule: a link without a match ID, Game History: https://www.halowaypoint.com/halo-infinite/players/x"));
            Assert.That(build.Warnings, Does.Contain("broken: no sheet 'Nope'"));
            Assert.That(build.Warnings, Does.Contain("page: its snapshot is not a workbook"));
            Assert.That(build.Warnings.Any(w => w.StartsWith("corrupt: could not be read")), Is.True);
        });
    }

    [Test]
    public void OnlyTheSeasonsOfChangedSources_AreAskedFor()
    {
        var schedule = HistoricalArchiveBuilder.Build(Sources(), new HashSet<string> { "schedule" });
        var stats = HistoricalArchiveBuilder.Build(Sources(), new HashSet<string> { "stats" });
        var champions = HistoricalArchiveBuilder.Build(Sources(), new HashSet<string> { "champions" });
        var players = HistoricalArchiveBuilder.Build(Sources(), new HashSet<string> { "late-league" });

        Assert.Multiple(() =>
        {
            Assert.That(schedule.Seasons, Is.EquivalentTo(new[] { "stats/Stats" }), "Its Waypoint games' season");
            Assert.That(schedule.Input.Players, Is.Null, "The players list didn't change");
            Assert.That(schedule.Input.Seasons.Seasons, Has.Count.EqualTo(2), "Every source is parsed: a season is imported whole");
            Assert.That(stats.Seasons, Is.EquivalentTo(new[] { "stats/Stats", "stats/Playoff Stats" }));
            Assert.That(champions.Seasons, Is.Null, "Titles are of every season");
            Assert.That(players.Seasons, Is.Empty);
            Assert.That(players.Input.Players, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TheCatalog_HasEverySourceOnce_AndItsConfigsPointAtRealSeasons()
    {
        var sources = HistoricalSourceCatalog.Sources;
        var stats = sources.Where(s => s.Config?.Tabs.Count > 0)
            .SelectMany(s => s.Config!.Tabs.Select(t => HistoricalKeys.SeasonSourceId(s.HistoricalSourceID, t.Name))).ToHashSet();
        Assert.Multiple(() =>
        {
            Assert.That(sources.Select(s => s.HistoricalSourceID), Is.Unique);
            Assert.That(sources.Where(s => s.Kind == HistoricalSourceKind.GoogleSheet).Select(s => s.SheetKey), Has.None.Null);
            Assert.That(sources.Where(s => s.IsDead).Select(s => s.Enabled), Is.All.False);
            Assert.That(sources.SelectMany(s => s.Config?.Games ?? Enumerable.Empty<GamesTab>()).Select(g => g.Season), Is.SubsetOf(stats), "Games go to a season a stats tab makes");
            Assert.That(sources.SelectMany(s => s.Config?.Waypoint ?? Enumerable.Empty<WaypointTab>()).Select(w => w.Season).Distinct(),
                Is.SubsetOf(sources.Where(s => s.Config?.Tabs.Count > 0).Select(s => s.HistoricalSourceID)));
            Assert.That(sources.Count(s => s.Config?.ChampionsSheet is not null), Is.EqualTo(1));
            Assert.That(sources.Single(s => s.Kind == HistoricalSourceKind.YouTubeChannel).Enabled, Is.True, "Kept for the VODs to come");
            Assert.That(stats, Has.Count.EqualTo(52), "The seasons the Node compiler made");
        });
    }

    [Test]
    public void AConfig_RoundTripsAsJson()
    {
        var config = HistoricalSourceCatalog.Sources.Single(s => s.HistoricalSourceID == "late-league-aspence-main").Config!;

        var back = HistoricalSourceConfig.FromJson(config.ToJson())!;

        Assert.Multiple(() =>
        {
            Assert.That(back.Tabs.Select(t => (t.Name, t.Game, t.CustomSeason)), Is.EqualTo(config.Tabs.Select(t => (t.Name, t.Game, t.CustomSeason))));
            Assert.That(back.Games.Select(g => (g.Sheet, g.Scrims, g.Layout)), Is.EqualTo(config.Games.Select(g => (g.Sheet, g.Scrims, g.Layout))));
            Assert.That(back.PlayersSheet, Is.EqualTo("Player Masterlist"));
            Assert.That(HistoricalSourceConfig.FromJson(null), Is.Null);
            Assert.That(config.ToJson(), Does.Contain("\"Carnage\""), "Enums as names");
        });
    }
}
