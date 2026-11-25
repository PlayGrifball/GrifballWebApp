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
}
