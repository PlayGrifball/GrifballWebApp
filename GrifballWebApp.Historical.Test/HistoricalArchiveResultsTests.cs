using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;

namespace GrifballWebApp.Historical.Test;

/// <summary>The builder's result lists (<see cref="HistoricalSourceConfig.Results"/>) and the per-game tabs' titles: games put
/// under their season, each side named as the season names its team.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalArchiveResultsTests
{
    private static readonly string[] StatsSeason = ["fl20/Stats"];
    private static readonly string[] NotoriousAndGhosts = ["Notorious", "Ghosts Of Grif"];

    private static HistoricalSourceContent Source(string id, HistoricalSourceConfig config, byte[] content)
        => new(HistoricalTestData.Source(id, $"https://example.org/{id}", config.ToJson()), 1, content);

    private static readonly object?[] StatsHeader = Row("Player", "Team", "Games", "Kills");

    [Test]
    public void AResultList_GoesUnderItsSeason_EachSideNamedAsTheSeasonNamesTheTeam()
    {
        List<HistoricalSourceContent> sources =
        [
            Source("fl20", new HistoricalSourceConfig
                {
                    Season = new SeasonDefaults { Name = "Fall League 2020 (PC)", Game = "reach" },
                    Tabs = [new() { Name = "Stats" }],
                    Results = [new() { Sheet = "Game Results", Teams = new() { ["AJunkyardDog"] = "AJYD" } }],
                },
                Workbook(("Stats", [StatsHeader, Row("Ace", "A&tS", 4, 1), Row("Sauted Smurf", "MAB", 4, 1), Row("Cee", "AJYD", 4, 1), Row("Dee", "ApoPo", 4, 1)]),
                    ("Game Results", [Row("Winner", null, "Loser", "Host"),
                        Row("A and the Spences", "5-0", "Mediocre At Best", "Rage"),
                        Row("AJunkyardDog", "5-1", "Apocalypse Ponies"),
                        Row("Smurf", "5-2", "Nobody Known")]))),
            Source("other", new HistoricalSourceConfig
                {
                    Results = [new() { Sheet = "Game Results", Season = "fl20" }, new() { Sheet = "Missing", Season = "fl20" }, new() { Sheet = "Game Results", Season = "nothing" }],
                },
                Workbook(("Game Results", [Row("Winner", null, "Loser"), Row("ApoPo", "5-3", "MAB")]))),
        ];

        var build = HistoricalArchiveBuilder.Build(sources, new HashSet<string> { "other" });

        var games = build.Input.RecordedGames!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.Teams[0].Name, g.Teams[1].Name)), Is.EqualTo(new[]
            {
                ((string?)"A&tS", (string?)"MAB"), ("AJYD", "ApoPo"), ("MAB", "Nobody Known"), ("ApoPo", "MAB"),
            }), "An abbreviation either way, the tab's alias, a player's name; a name nothing finds stays");
            Assert.That(games.Select(g => (g.SeasonSourceId, g.GameVersion)), Is.All.EqualTo(("fl20/Stats", "reach")));
            Assert.That(games[0].Host, Is.EqualTo("Rage"));
            Assert.That(build.Input.Seasons.Seasons.Single().Teams, Is.Empty, "The season names its teams: the list adds none");
            Assert.That(build.Seasons, Is.EquivalentTo(StatsSeason), "The list's season is the changed source's");
            Assert.That(build.Warnings, Does.Contain("other: results 'Missing' not read (no such sheet)"));
            Assert.That(build.Warnings, Does.Contain("other: results 'Game Results' not read (no season from nothing)"));
        }
    }

    [Test]
    public void AResultListOfASeasonOfItsOwn_MakesTheSeason_WithTheListsTeams()
    {
        var source = Source("sml18", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Summer League 2018 (Fantasy Rey)", Game = "halo3", League = "AGLA" },
                Tabs = [new() { Name = "Total Stats" }],
                Results = [new() { Sheet = "Fixtures", Layout = ResultsLayout.Series, CustomSeason = "Summer League 2018", Game = "halo5" },
                    new() { Sheet = "Fixtures", Layout = ResultsLayout.Series, CustomSeason = "Summer League 2018", Game = "halo5" }],
            },
            Workbook(("Total Stats", [StatsHeader, Row("Ace", null, 4, 1)]),
                ("Fixtures", [Row("Fortnight 1", null, "Notorious 2-1 Ghosts Of Grif", "5-4", "2-5", "5-2")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        var season = build.Input.Seasons.Seasons.Single(s => s.SeasonName == "Summer League 2018");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((season.GameVersion, season.League, season.Division, season.SourceWorkbook, season.SourceSheet, season.IsPlayoffs),
                Is.EqualTo(("halo5", "AGLA", "Open", "sml18", "Fixtures", false)));
            Assert.That(season.Teams.Select(t => t.TeamName), Is.EqualTo(NotoriousAndGhosts), "Once, however many tabs list a team");
            Assert.That(build.Input.Seasons.Seasons, Has.Count.EqualTo(2), "One season of its own for both tabs");
            Assert.That(build.Input.RecordedGames!.Select(g => (g.SeasonSourceId, g.GameVersion)), Is.All.EqualTo(("sml18/Fixtures", "halo5")));
        }
    }

    [Test]
    public void AGameResultsTitle_GivesAForfeitItsGame_APartialCopyItsOtherSide_AndEachItsVod()
    {
        object?[] header = Row("Team", "Player", "Games Played", "Wins", "Minutes", "Goals", "Kills", "Assists", "Deaths", "Carrier Kills");
        var source = Source("wl19", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Winter League 2019", Game = "halo3" },
                Tabs = [new() { Name = "Stats" }],
                Games = [new() { Sheet = "Game Results", Season = "wl19/Stats", Game = "halo3", Layout = GamesLayout.Blocks }],
            },
            Workbook(("Stats", [StatsHeader, Row("imShad", "Our Lady Tuna", 4, 1), Row("godwit1ivy", "Collateral Damage", 4, 1), Row("Pad", "Stat Padders United", 4, 1)]),
                ("Game Results", [Row("Template"),
                    Row("Week 2 - 2/4/19 - Collateral Damage over Stat Padder United - Forfeit"),
                    Row("Week 4 - 2/18/19 - Our Lady Tuna FF Win over Stat Padders United"),
                    Row("Week 6 - 3/8/19 - Our Lady Tuna (FF W) over Collateral Damage"),
                    Row("Week 1 - 1/28/19 - Our Lady Tuna over Collateral Damage (5-2) RECORDED https://xboxdvr.com/x"),
                    header,
                    Row("Our Lady Tuna", "imShad (c)", 1, 1, 17.6, 0, 73, 1, 44, 2),
                    Row("Week 1 - 1/29/19 - Collateral Damage over Our Lady Tuna (5-3)"),
                    header,
                    Row("Our Lady Tuna", "imShad", 1, 0, 17.6, 0, 73, 1, 44, 2),
                    Row("Week 1 - 1/30/19 - Nobody over Somebody (5-3) - NO STATS"),
                    Row("Not a header")])));

        var build = HistoricalArchiveBuilder.Build([source]);

        var games = build.Input.RecordedGames!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.SourceKey, g.IsForfeit, g.Players.Count, g.Vod)), Is.EqualTo(new[]
            {
                ("wl19/Game Results#1", true, 0, (string?)null), ("wl19/Game Results#2", true, 0, null), ("wl19/Game Results#3", true, 0, null),
                ("wl19/Game Results#4", false, 1, "https://xboxdvr.com/x"), ("wl19/Game Results#7", false, 1, null),
            }), "A title without a block that is no forfeit is no game");
            Assert.That(games.Select(g => g.Teams.Select(t => (t.Side, t.Name, t.Outcome, t.Score)).ToList()), Is.EqualTo(new List<(int, string?, Outcomes, int?)>[]
            {
                [(1, "Collateral Damage", Outcomes.Won, null), (2, "Stat Padders United", Outcomes.Lost, null)],
                [(1, "Our Lady Tuna", Outcomes.Won, null), (2, "Stat Padders United", Outcomes.Lost, null)],
                [(1, "Our Lady Tuna", Outcomes.Won, null), (2, "Collateral Damage", Outcomes.Lost, null)],
                [(1, "Our Lady Tuna", Outcomes.Won, 5), (2, "Collateral Damage", Outcomes.Lost, 2)],
                [(1, "Our Lady Tuna", Outcomes.Lost, 3), (2, "Collateral Damage", Outcomes.Won, 5)],
            }), "A side its title spells another way takes the season's name; a partial copy's other side is the team the title names that it is not");
            Assert.That(build.Warnings, Is.Empty);
        }
    }

    [Test]
    public void APartialCopyOfATie_WhoseSideTheTitleDoesNotName_StaysOneSided()
    {
        object?[] header = Row("Team", "Player", "Games Played", "Wins", "Minutes", "Goals", "Kills");
        var workbook = SheetWorkbook.Read(Workbook(("Game Results", [Row("Template"),
            Row("Week 1 - 1/28/19 - Tuna tied Damage (5-5)"), header, Row("Someone Else", "imShad", 1, 0, 10, 5, 10)])))!;
        var warnings = new List<string>();

        var games = RecordedGameParser.Parse("wl19", new GamesTab { Sheet = "Game Results", Season = "wl19/Stats", Game = "halo3", Layout = GamesLayout.Blocks }, workbook, warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games, Is.Empty);
            Assert.That(warnings, Has.Count.EqualTo(1));
        }
    }
}
