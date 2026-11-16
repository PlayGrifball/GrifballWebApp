using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Test;

/// <summary>AwardsParser on the archive's award layouts (the sheets' own, trimmed to a few awards).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AwardsParserTests
{
    private static SheetTable Sheet(params string[][] rows) => new() { Name = "Awards", Rows = [.. rows] };

    private static List<(string Name, string Winner)> Winners(List<AwardsParser.ParsedAward> awards) => [.. awards.Select(a => (a.Award.Name, a.Award.Winner))];

    [Test]
    public void Columns_ReadsEachHeadersWinnersDownToTheFirstGap()
    {
        // H4 Bid's Achievements: an award per column, its winners below
        var sheet = Sheet(
            ["S Marks the Spot (S Grade)", "Divine Blade", "", "Unstoppable"],
            ["Solace VII", "PoisonxPoptart", "", "kev1897"],
            ["Scomancer", "", "", "Solace VIII"],
            ["", "", "", "Myst the Cold"],
            ["Not an award winner"]);

        var awards = AwardsParser.Parse(sheet, new AwardsTab { Sheet = "Achievements", Names = new() { ["Solace VII"] = "Solace VIII" } });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Winners(awards), Is.EqualTo(new List<(string, string)>
            {
                ("S Marks the Spot (S Grade)", "Solace VIII"), ("S Marks the Spot (S Grade)", "Scomancer"),
                ("Divine Blade", "PoisonxPoptart"),
                ("Unstoppable", "kev1897"), ("Unstoppable", "Solace VIII"), ("Unstoppable", "Myst the Cold"),
            }));
            Assert.That(awards.All(a => a.Season is null && a.Award.Value is null && !a.Award.IsTeam), Is.True);
        }
    }

    [Test]
    public void Columns_Ranked_FromItsAnchor_TakesTheRankOffAndGivesThePlace()
    {
        // GWL SL23's PrePlayoff Standings: two ranked lists under a header row past the sheet's first, the first unnumbered
        var sheet = Sheet(
            ["Season notes"],
            ["", "GWL'S TOP 5 RUNNERS", "", "GWL'S TOP 10 TANKS"],
            ["", "yLuxe", "", "oDhmon"],
            ["", "2. EllisGotMyPills", "", "2. DIGITAL PAIN"],
            ["", "", "", "3. Itzreelz"]);
        var tab = new AwardsTab
        {
            Sheet = "PrePlayoff Standings", Anchor = "gwl's top 5 runners", Ranked = true,
            Awards = new() { ["GWL'S TOP 5 RUNNERS"] = "Top 5 Runners" }, Names = new() { ["DIGITAL PAIN"] = "DlGITAL PAIN" },
        };

        var awards = AwardsParser.Parse(sheet, tab);

        Assert.That(awards.Select(a => (a.Award.Name, a.Award.Winner, a.Award.Value)), Is.EqualTo(new List<(string, string, string?)>
        {
            ("Top 5 Runners", "yLuxe", "#1"), ("Top 5 Runners", "EllisGotMyPills", "#2"),
            ("GWL'S TOP 10 TANKS", "oDhmon", "#1"), ("GWL'S TOP 10 TANKS", "DlGITAL PAIN", "#2"), ("GWL'S TOP 10 TANKS", "Itzreelz", "#3"),
        }));
    }

    [Test]
    public void Columns_AnAnchorNotInTheSheet_ReadsNothing()
    {
        var sheet = Sheet(["MVP"], ["yLuxe"]);

        Assert.That(AwardsParser.Parse(sheet, new AwardsTab { Sheet = "S", Anchor = "Top Runners" }), Is.Empty);
    }

    [Test]
    public void Labels_TakesTheFirstTextBelowEachLabel_InItsColumns()
    {
        // WL23's Season Awards: labels in merged blocks, the winner's text a column in; a legend repeats the labels below
        var sheet = Sheet(
            ["", "MVP", "", "", "", "", "", "Best Tank", "", "", "", "", "", "", "Best Hybrid"],
            ["", "", "yLuxe", "", "", "", "", "", "Jussshhhyyy"],
            ["", "Samurai", "", "", "", "Best Duo", "", "", "", "", "", "", "", "", "Best Runner"],
            ["", "", "", "", "", "", "", "", "", "", "", "Compass"],
            ["", "Memorized And Ken Khaneki", "", "", "", "Mavbrid And MysticalNovice, Jussshhhyyy and Scullen cx", "", "", "", "", "", "", "", "", "Mavbrid"],
            ["", "", "", "", "", "", "", "", "", "", "MVP", "The Most Valuable Player Of The Season"],
            ["", "ROTY", "", "", "", "Mr. Intangibles"],
            ["", "Captain Sev", "", "", "", "Lmao"]);
        var tab = new AwardsTab
        {
            Sheet = "Season Awards", Layout = AwardsLayout.Labels,
            Awards = new()
            {
                ["MVP"] = "", ["Best Tank"] = "", ["Best Hybrid"] = "", ["Samurai"] = "", ["Best Duo"] = "", ["Best Runner"] = "",
                ["ROTY"] = "Rookie of the Year", ["Mr. Intangibles"] = "", ["Tool Man"] = "",
            },
            Names = new() { ["Captain Sev"] = "CaptainSev004", ["lmao"] = "" },
        };

        var awards = AwardsParser.Parse(sheet, tab);

        Assert.That(Winners(awards), Is.EqualTo(new List<(string, string)>
        {
            ("MVP", "yLuxe"), ("Best Tank", "Jussshhhyyy"),
            ("Samurai", "Memorized"), ("Samurai", "Ken Khaneki"),
            ("Best Duo", "Mavbrid"), ("Best Duo", "MysticalNovice"), ("Best Duo", "Jussshhhyyy"), ("Best Duo", "Scullen cx"),
            ("Best Runner", "Mavbrid"),
            ("Rookie of the Year", "CaptainSev004"),
        }), "Best Hybrid's first text below is another label (no winner); Tool Man isn't in the sheet; a joke entry is no player");
    }

    [Test]
    public void Seasons_ReadsEachBlocksAwards_UnderTheSeasonItsTitleNames()
    {
        // Luxe's Record Book: a season block's title in column A, its MVP beside it and its Finals MVP on the next row
        var sheet = Sheet(
            ["SEASON CHAMPS"],
            ["", "", "", "", "MVP:"],
            ["WL23 Season 1 Champions - Ram Ranch", "", "", "", "MVP: Luxe"],
            ["Fanciest Pants", "KDSkill", "TommyWstSide", "MuNgO PuNgO", "FMVP: Fancy"],
            ["SML25 Season 6 Pro Champions - Three Days Ace", "", "", "", "MVP: Ace"],
            ["Hells Angel cx", "Ace B S1lentND1", "", "", "FMVP: Ace"],
            ["SML25 Season 6 Legacy Champions - Reverse Sweepers", "", "", "", "MVP: Sev"],
            ["Rechargen", "", "", "", "FMVP: Rechargen", "Note: kept"],
            ["Season 9 Champions - Unknown", "", "", "", "MVP: Nobody"]);
        var tab = new AwardsTab
        {
            Sheet = "Record Book", Layout = AwardsLayout.Seasons,
            Awards = new() { ["MVP"] = "", ["FMVP"] = "Finals MVP" },
            Seasons = new() { ["Season 1"] = "wl23", ["Season 6"] = "wrong", ["Season 6 Pro"] = "sml25-pro", ["Season 6 Legacy"] = "sml25-legacy" },
            Names = new() { ["Luxe"] = "yLuxe", ["Fancy"] = "Fanciest Pants", ["Ace"] = "ACE B S1lentND1", ["Sev"] = "CaptainSev004" },
        };

        var awards = AwardsParser.Parse(sheet, tab);

        Assert.That(awards.Select(a => (a.Season, a.Award.Name, a.Award.Winner)), Is.EqualTo(new List<(string?, string, string)>
        {
            ("wl23", "MVP", "yLuxe"), ("wl23", "Finals MVP", "Fanciest Pants"),
            ("sml25-pro", "MVP", "ACE B S1lentND1"), ("sml25-pro", "Finals MVP", "ACE B S1lentND1"),
            ("sml25-legacy", "MVP", "CaptainSev004"), ("sml25-legacy", "Finals MVP", "Rechargen"),
        }), "The longest season key a title holds; a title of no season given is skipped");
    }

    [Test]
    public void Records_ReadsAPlayersRecordWithItsTeam_AndATeamsAsTheTeams()
    {
        // grifball.com's Records table
        var sheet = Sheet(
            ["Records of the league"],
            ["category", "description", "record_value", "holder", "team_or_captain", "date", "opponent", "bungie_game_id"],
            ["Individual Player Records (Single Game)", "Most Kills (one game)", "134", "Sisum", "WYLASS", "1/9/08", "Tactical Warfare", "245795122"],
            ["Individual Player Records (Single Game)", "Best Batting Avg", "77%", "magus0mega", "", "1/11/08"],
            ["Team Records (Single Game)", "Most Kills", "385", "Chupathingy", "Gingerlink", "1/8/08"],
            ["Team Records (Single Game)", "Best Team Spread", "", "WYLASS", "Syphith"],
            ["Team Records (Single Game)", "", "1", "Nobody"]);

        var awards = AwardsParser.Parse(sheet, new AwardsTab { Sheet = "Records", Layout = AwardsLayout.Records });

        Assert.That(awards.Select(a => (a.Award.Name, a.Award.Winner, a.Award.IsTeam, a.Award.Team, a.Award.Value)), Is.EqualTo(new List<(string, string, bool, string?, string?)>
        {
            ("Most Kills (one game)", "Sisum", false, "WYLASS", "134"),
            ("Best Batting Avg", "magus0mega", false, null, "77%"),
            ("Team Most Kills", "Chupathingy", true, null, "385"),
            ("Best Team Spread", "WYLASS", true, null, null),
        }), "A team's record is named as one; a row without a description is skipped");
    }

    [Test]
    public void Records_WithoutItsHeaders_ReadsNothing()
    {
        Assert.That(AwardsParser.Parse(Sheet(["category", "record"], ["A", "B"]), new AwardsTab { Sheet = "Records", Layout = AwardsLayout.Records }), Is.Empty);
    }
}
