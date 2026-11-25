using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;

namespace GrifballWebApp.Historical.Test;

/// <summary>The builder's schedules (<see cref="HistoricalSourceConfig.Schedules"/>): their matches date the season's games.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalArchiveSchedulesTests
{
    private static readonly object?[] StatsHeader = Row("Player", "Team", "Games", "Kills");

    private static HistoricalSourceContent Source(string id, HistoricalSourceConfig config, byte[] content)
        => new(HistoricalTestData.Source(id, $"https://example.org/{id}", config.ToJson()), 1, content);

    private static readonly DateTime June1 = new(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime June8 = new(2020, 6, 8, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void AScheduledMeeting_DatesTheFirstUndatedSeriesOfItsTeams_InTheListsOrder()
    {
        var source = Source("sl17", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Spring League 2017 (Pro)", Game = "halo3" },
                Tabs = [new() { Name = "Stats" }],
                Results = [new() { Sheet = "Game Results" }],
                Schedules = [new() { Sheet = "Schedule" }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1), Row("Cee", "Charlie", 4, 1)]),
                ("Game Results", [Row("Winner", null, "Loser"),
                    Row("Alpha", "5-3", "Bravo"), Row("Alpha", "5-1", "Bravo"),
                    Row("Bravo", "5-2", "Charlie"),
                    Row("Bravo", "5-0", "Alpha")]),
                ("Schedule", [Row("Date", "6/1/2020", "6/8/2020"),
                    Row(null, "Alpha 5-3 Bravo", "Bravo vs Alpha"),
                    Row(null, "Alpha 5-1 Bravo", "Delta vs Echo"),
                    Row(null, "Charlie vs. Bravo")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Input.RecordedGames!.Select(g => g.PlayedAt), Is.EqualTo(new DateTime?[] { June1, June1, June1, June8 }),
                "Both games of the first series on its day (the schedule's two entries of it are one meeting), either side first");
            Assert.That(build.Warnings, Does.Contain("sl17/Schedule: 1 of 4 scheduled meetings date no game"));
        }
    }

    [Test]
    public void AScheduleOfAnotherWorkbook_DatesItsSeasonsGames_AndAMissingTabSaysSo()
    {
        List<HistoricalSourceContent> sources =
        [
            Source("wl21-legacy", new HistoricalSourceConfig
                {
                    Season = new SeasonDefaults { Name = "Winter League 2021 (Legacy)", Game = "halo3" },
                    Tabs = [new() { Name = "Stats" }],
                    Results = [new() { Sheet = "Game Results" }],
                },
                Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1)]),
                    ("Game Results", [Row("Winner", null, "Loser"), Row("Alpha", "5-3", "Bravo")]))),
            Source("wl21-schedule", new HistoricalSourceConfig
                {
                    Schedules = [new() { Sheet = "Legacy Schedule", Season = "wl21-legacy" }, new() { Sheet = "Missing", Season = "wl21-legacy" }],
                },
                Workbook(("Legacy Schedule", [Row("Date", "6/8/2020"), Row(null, "Bravo vs. Alpha")]))),
        ];

        var build = HistoricalArchiveBuilder.Build(sources);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Input.RecordedGames!.Single().PlayedAt, Is.EqualTo(June8));
            Assert.That(build.Warnings, Does.Contain("wl21-schedule: schedule 'Missing' not read (no such sheet)"));
        }
    }

    [Test]
    public void AnUnplayedSchedulesMeetingThatDatesNoGame_IsAMatchWithNoResult_WhenTheSeasonHasBothTeams()
    {
        var source = Source("wl20", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Winter League 2020 (Reach PC)", Game = "reach" },
                Tabs = [new() { Name = "Stats" }],
                Results = [new() { Sheet = "Game Results" }],
                Schedules = [new() { Sheet = "Full Schedule", Unplayed = true }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1), Row("Cee", "Charlie", 4, 1)]),
                ("Game Results", [Row("Winner", null, "Loser"), Row("Alpha", "5-3", "Bravo")]),
                ("Full Schedule", [Row("Date", "6/1/2020", "6/8/2020"),
                    Row(null, "Bravo vs Alpha", "Charlie V. Alpha"),
                    Row(null, null, "Delta vs Echo")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Input.RecordedGames!.Single().PlayedAt, Is.EqualTo(June1), "A meeting with a game dates it");
            Assert.That(build.Input.ScheduledMatches!.Select(m => (m.SeasonSourceId, m.Home, m.Away, m.PlayedAt)),
                Is.EqualTo(new[] { ("wl20/Stats", "Charlie", "Alpha", June8) }), "Teams the season hasn't make no match");
            Assert.That(build.Warnings, Does.Contain("wl20/Full Schedule: 1 of 3 scheduled meetings date no game"));
        }
    }

    [Test]
    public void AMeetingOfSeveralEntries_DatesASeriesOfAGameEach_AsManyAsItHasEntries()
    {
        // GGL SML17's Schedule: a day's games each their own cell, and each its own series in the result list
        var source = Source("ggl17", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "GGL Summer League 2017", Game = "halo3" },
                Tabs = [new() { Name = "Stats" }],
                Results = [new() { Sheet = "Schedule", Layout = ResultsLayout.Series }],
                Schedules = [new() { Sheet = "Schedule" }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1)]),
                ("Schedule", [Row("Date", "6/1/2020", "6/8/2020"),
                    Row(null, "Alpha over Bravo 5-4", "Bravo over Alpha 5-0"),
                    Row(null, "Bravo over Alpha 5-1")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Input.RecordedGames!.Select(g => g.PlayedAt), Is.EquivalentTo(new DateTime?[] { June1, June1, June8 }));
    }

    [Test]
    public void AWholeSeriesEntry_DatesItsTeamsUndatedGamesInARow()
    {
        // GWL SL23: a per-game tab's games, a match's in a row; the schedule lists each match once
        var source = Source("gwl23", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "GWL Spring League 2023", Game = "reach" },
                Tabs = [new() { Name = "Stats" }],
                Results = [new() { Sheet = "Game Results", Layout = ResultsLayout.Series }],
                Schedules = [new() { Sheet = "Schedule", WholeSeries = true }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1), Row("Cee", "Charlie", 4, 1)]),
                ("Game Results", [Row("Alpha over Bravo 5-4"), Row("Bravo over Alpha 5-1"), Row("Charlie over Alpha 5-0"), Row("Alpha over Bravo 5-2")]),
                ("Schedule", [Row("Date", "6/1/2020", "6/8/2020"), Row(null, "Ace @ Bee", "Bee @ Ace"), Row(null, "Cee @ Ace")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Input.RecordedGames!.Select(g => g.PlayedAt), Is.EqualTo(new DateTime?[] { June1, June1, June1, June8 }),
            "Captains name their teams; the first match is both games in a row");
    }

    private static readonly object?[] CarnageHeader = Row("Player", "Team", "GamesPlayed", "Wins", "Loses", "Goals", "Kills", "Assists", "Deaths", "Betrayals", "HammerKills", "BombStops", "GameTime", "TimeStamp", "Game");

    /// <summary>A per-game tab's row, no timestamp: a game is a run of rows of one game time.</summary>
    private static object?[] Carnage(string player, int team, bool won, int gameTime)
        => Row(player, team, 1, won ? 1 : 0, won ? 0 : 1, won ? 1 : 0, 10, 2, 9, 0, 5, 1, gameTime, null, null);

    [Test]
    public void APerGameTabsGame_IsItsPlayersTeams_ForTheSchedule()
    {
        // GWL SL23's HR_MatchH: its rows name no team, only their side's number
        var source = Source("gwl23", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "GWL Spring League 2023", Game = "reach" },
                Tabs = [new() { Name = "Stats" }],
                Games = [new() { Sheet = "HR_MatchH", Season = "gwl23/Stats", Game = "reach" }],
                Schedules = [new() { Sheet = "Schedule", WholeSeries = true }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1), Row("Cee", "Charlie", 4, 1)]),
                ("HR_MatchH", [CarnageHeader,
                    Carnage("Ace", 0, true, 600), Carnage("Bee", 1, false, 600),
                    Carnage("Bee", 0, true, 700), Carnage("Ace", 1, false, 700),
                    Carnage("Cee", 0, true, 800), Carnage("Ace", 1, false, 800)]),
                ("Schedule", [Row("Date", "6/1/2020", "6/8/2020"), Row(null, "Ace @ Bee", "Cee @ Ace")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Input.RecordedGames!.Select(g => g.PlayedAt), Is.EqualTo(new DateTime?[] { June1, June1, June8 }));
    }

    [Test]
    public void AScheduleOfNoSeason_IsNotRead_AndSaysSo()
    {
        var source = Source("lost", new HistoricalSourceConfig { Schedules = [new() { Sheet = "Schedule", Season = "nothing" }] },
            Workbook(("Schedule", [Row("Date", "6/1/2020"), Row(null, "A vs B")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Warnings, Does.Contain("lost: schedule 'Schedule' not read (no season from nothing)"));
    }
}
