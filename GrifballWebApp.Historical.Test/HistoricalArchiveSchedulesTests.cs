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

    private static readonly string[] WL21Legacy = ["wl21-legacy/Stats"];
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
            Assert.That(build.Input.ScheduledSeasons, Is.EqualTo(WL21Legacy), "The season the schedule covers");
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
                    Carnage("Cee", 0, true, 800), Carnage("Ace", 1, false, 800),
                    Carnage("Zed", 0, true, 900), Carnage("Yan", 1, false, 900)]),
                ("Schedule", [Row("Date", "6/1/2020", "6/8/2020"), Row(null, "Ace @ Bee", "Cee @ Ace")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Input.RecordedGames!.Select(g => g.PlayedAt), Is.EqualTo(new DateTime?[] { June1, June1, June8, null }),
            "A game of players on no team is no meeting's");
    }

    [Test]
    public void AScheduleOfNoSeason_IsNotRead_AndSaysSo()
    {
        var source = Source("lost", new HistoricalSourceConfig { Schedules = [new() { Sheet = "Schedule", Season = "nothing" }] },
            Workbook(("Schedule", [Row("Date", "6/1/2020"), Row(null, "A vs B")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Warnings, Does.Contain("lost: schedule 'Schedule' not read (no season from nothing)"));
    }

    [Test]
    public void AScheduledTeamWithItsCaptainInBrackets_IsTheTeam_OrElseTheCaptains()
    {
        // FL20's Full Schedule: "ASP (Dhmon) vs. FB (Saintz)"
        var source = Source("fl20", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Fall League 2020 (PC)", Game = "reach" },
                Tabs = [new() { Name = "Stats" }],
                Results = [new() { Sheet = "Game Results" }],
                Schedules = [new() { Sheet = "Full Schedule" }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1)]),
                ("Game Results", [Row("Winner", null, "Loser"), Row("Alpha", "5-3", "Bravo")]),
                ("Full Schedule", [Row("Date", "6/1/2020", "6/8/2020"), Row(null, "Alpha (Ace) vs. Zed (Bee)", "Zed (Nobody) vs. Alpha (Ace)")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Input.RecordedGames!.Single().PlayedAt, Is.EqualTo(June1), "Zed is no team: Bee's, Bravo");
            Assert.That(build.Warnings, Does.Contain("fl20/Full Schedule: 1 of 2 scheduled meetings date no game"));
        }
    }

    [Test]
    public void AScheduleOfAStatsTab_IsThatSeasons_AndItsChangeAffectsIt()
    {
        List<HistoricalSourceContent> sources =
        [
            Source("s", new HistoricalSourceConfig
                {
                    Season = new SeasonDefaults { Name = "Winter League 2021", Game = "halo3" },
                    Tabs = [new() { Name = "Playoff Stats", IsPlayoffs = true }, new() { Name = "Stats" }],
                    Results = [new() { Sheet = "Game Results", Tab = "Stats" }],
                },
                Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1)]),
                    ("Playoff Stats", [StatsHeader, Row("Ace", "Alpha", 1, 1)]),
                    ("Game Results", [Row("Winner", null, "Loser"), Row("Alpha", "5-3", "Bravo")]))),
            Source("sch", new HistoricalSourceConfig { Schedules = [new() { Sheet = "Schedule", Season = "s", Tab = "Stats" }] },
                Workbook(("Schedule", [Row("Date", "6/1/2020"), Row(null, "Bravo vs Alpha")]))),
        ];

        var changed = HistoricalArchiveBuilder.Build(sources, new HashSet<string> { "sch" });
        var unchanged = HistoricalArchiveBuilder.Build(sources, new HashSet<string> { "s" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed.Input.RecordedGames!.Single().PlayedAt, Is.EqualTo(June1));
            Assert.That(changed.Seasons, Does.Contain("s/Stats"), "The schedule changed: its season is the build's");
            Assert.That(unchanged.Input.RecordedGames!.Single().PlayedAt, Is.EqualTo(June1));
        }
    }

    [Test]
    public void AnEveningsTwoEntries_AreOneMeeting_ThoughTheLaterIsTheNextDayInUtc()
    {
        // 7:30 pm and 8:30 pm Eastern on June 1st: the second is June 2nd in UTC
        var source = Source("sl17", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Spring League 2017 (Pro)", Game = "halo5" },
                Tabs = [new() { Name = "Stats" }],
                Results = [new() { Sheet = "Game Results" }],
                Schedules = [new() { Sheet = "Schedule" }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1)]),
                ("Game Results", [Row("Winner", null, "Loser"), Row("Alpha", "5-3", "Bravo"), Row("Alpha", "5-1", "Bravo")]),
                ("Schedule", [Row("Date", "6/1/2020"), Row(null, "Alpha 5-3 Bravo 7:30pm"), Row(null, "Alpha 5-1 Bravo 8:30pm")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Input.RecordedGames!.Select(g => g.PlayedAt), Is.All.EqualTo(new DateTime(2020, 6, 1, 23, 30, 0, DateTimeKind.Utc)),
            "One meeting: the series' two games at its first entry's time");
    }

    [Test]
    public void APerGameBlocksSideLabel_IsItsTeam_AsTheSeasonNamesIt_ForTheSchedule()
    {
        // SL19's Game Results: a block's side with players keeps its row's label ("Our Lady Tunaa")
        object?[] header = Row("Team", "Player", "Games Played", "Wins", "Minutes", "Goals", "Kills", "Assists", "Deaths", "Carrier Kills");
        var source = Source("sl19", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Spring League 2019", Game = "halo3" },
                Tabs = [new() { Name = "Stats" }],
                Games = [new() { Sheet = "Game Results", Season = "sl19/Stats", Game = "halo3", Layout = GamesLayout.Blocks }],
                Schedules = [new() { Sheet = "Schedule" }],
            },
            Workbook(("Stats", [StatsHeader, Row("imShad", "Our Lady Tuna", 4, 1), Row("godwit1ivy", "Collateral Damage", 4, 1)]),
                ("Game Results", [Row("Template"),
                    Row("Week 1 - Our Lady Tuna over Collateral Damage (5-2)"),
                    header,
                    Row("Our Lady Tunaa", "imShad (c)", 1, 1, 17.6, 0, 73, 1, 44, 2)]),
                ("Schedule", [Row("Date", "5/19/2019"), Row(null, "Our Lady Tuna over Collateral Damage 5-2")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        Assert.That(build.Input.RecordedGames!.Single().PlayedAt, Is.EqualTo(new DateTime(2019, 5, 19, 0, 0, 0, DateTimeKind.Utc)));
    }
}
