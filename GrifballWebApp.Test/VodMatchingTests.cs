using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Historical.Vods;

namespace GrifballWebApp.Test;

/// <summary>VOD titles read (seasons, weeks, stages, teams) and matched to seasons, teams and matches, with titles like
/// the channel's.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VodMatchingTests
{
    private static DateTime D(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Parse_ReadsTheSeasonWeekAndTeams()
    {
        var info = VodTitleParser.Parse("SL24 Week 3 - Team A vs Team B");

        Assert.Multiple(() =>
        {
            Assert.That(info.Season.Term, Is.EqualTo(SeasonTerm.Spring));
            Assert.That(info.Season.Year, Is.EqualTo(2024));
            Assert.That(info.Week, Is.EqualTo(3));
            Assert.That((info.TeamA, info.TeamB), Is.EqualTo(("Team A", "Team B")));
            Assert.That(info.IsPlayoff, Is.False);
        });
    }

    [TestCase("SML25 Pro Playoffs | Alpha vs. Bravo", SeasonTerm.Summer, 2025, "pro")]
    [TestCase("WL'23 Semi-Final: Alpha v Bravo", SeasonTerm.Winter, 2023, null)]
    [TestCase("FL24 Grand Final - Alpha vs Bravo", SeasonTerm.Fall, 2024, null)]
    [TestCase("AGLA Infinite SL25' Week 1 - Alpha vs Bravo", SeasonTerm.Spring, 2025, null)]
    [TestCase("SL26 Major Week 2 — Alpha vs Bravo", SeasonTerm.Spring, 2026, "major")]
    [TestCase("Spring League 2026 (Minor) | Alpha vs Bravo", SeasonTerm.Spring, 2026, "minor")]
    [TestCase("Autumn League 2024 | \u201cAlpha\u201d vs Bravo", SeasonTerm.Fall, 2024, null)]
    public void Parse_ReadsSeasonAbbreviations(string title, SeasonTerm term, int year, string? qualifier)
    {
        var info = VodTitleParser.Parse(title);

        Assert.Multiple(() =>
        {
            Assert.That(info.Season.Term, Is.EqualTo(term));
            Assert.That(info.Season.Year, Is.EqualTo(year));
            Assert.That(info.Season.Qualifiers, qualifier is null ? (NUnit.Framework.Constraints.IResolveConstraint)Is.Empty : Is.EquivalentTo(new[] { qualifier }));
            Assert.That((info.TeamA, info.TeamB), Is.EqualTo(("Alpha", "Bravo")));
        });
    }

    [Test]
    public void Parse_ReadsTheChannelsTitles()
    {
        var championship = VodTitleParser.Parse("AGLA Summer Minor Championship | BO7 | Low IQ vs Reach for the Balls | Halo Infinite Grifball");
        var thirdPlace = VodTitleParser.Parse("AGLA Summer Minor PLAYOFFS | 3rd Place Match | Uncle Scorefester (2) vs Duck Dynasty (3)");
        var semi = VodTitleParser.Parse("AGLA Semifinal Playoffs: Like CLockwork (2) vs Spank’s Life Coaches (6)");
        var score = VodTitleParser.Parse("AGLA: Zero Logic (Rebs) 0:0 Like Clockwork (YLuxe)");
        var late = VodTitleParser.Parse("Late League S7 Grand Final - Charlie vs Delta");
        var none = VodTitleParser.Parse("Blitz League I");

        Assert.Multiple(() =>
        {
            Assert.That(championship.Season.Term, Is.EqualTo(SeasonTerm.Summer));
            Assert.That(championship.Season.Year, Is.Null, "No year: the publish date decides");
            Assert.That(championship.Season.Qualifiers, Is.EquivalentTo(new[] { "minor" }));
            Assert.That(championship.IsFinal, Is.True);
            Assert.That((championship.TeamA, championship.TeamB), Is.EqualTo(("Low IQ", "Reach for the Balls")));
            Assert.That((thirdPlace.IsPlayoff, thirdPlace.IsFinal), Is.EqualTo((true, false)));
            Assert.That((thirdPlace.TeamA, thirdPlace.TeamB), Is.EqualTo(("Uncle Scorefester", "Duck Dynasty")), "Seeds dropped");
            Assert.That((semi.IsPlayoff, semi.IsFinal), Is.EqualTo((true, false)));
            Assert.That((semi.TeamA, semi.TeamB), Is.EqualTo(("Like CLockwork", "Spank's Life Coaches")));
            Assert.That((score.TeamA, score.TeamB), Is.EqualTo(("Zero Logic", "Like Clockwork")), "A score separates the teams; the captains are dropped");
            Assert.That(late.Season.LateLeagueSeason, Is.EqualTo(7));
            Assert.That(late.Season.Term, Is.Null);
            Assert.That(late.IsFinal, Is.True);
            Assert.That(none.Season.NamesASeason, Is.False);
            Assert.That(none.NamesTeams, Is.False);
        });
    }

    [Test]
    public void ParseSeasonName_ReadsTheArchivesNames()
    {
        var spring = VodTitleParser.ParseSeasonName("Spring League 2026 (Minor)", D(2026, 1, 1));
        var classic = VodTitleParser.ParseSeasonName("Summer League 2025 (Classic Pro)", D(2025, 1, 1));
        var late = VodTitleParser.ParseSeasonName("Late League Season 7 (Halo 3)", D(2019, 1, 1));
        var noYear = VodTitleParser.ParseSeasonName("Summer Minor", D(2026, 6, 1));
        var other = VodTitleParser.ParseSeasonName("GWL Tournament Series #1", D(2023, 1, 1));

        Assert.Multiple(() =>
        {
            Assert.That((spring.Term, spring.Year), Is.EqualTo(((SeasonTerm?)SeasonTerm.Spring, (int?)2026)));
            Assert.That(spring.Qualifiers, Is.EquivalentTo(new[] { "minor" }));
            Assert.That(classic.Qualifiers, Is.EquivalentTo(new[] { "classic", "pro" }));
            Assert.That(late.LateLeagueSeason, Is.EqualTo(7));
            Assert.That(noYear.Year, Is.EqualTo(2026), "A name without a year takes its start's");
            Assert.That(other.NamesASeason, Is.False);
        });
    }

    [TestCase("The Bandits", "bandits")]
    [TestCase("Spank’s Life Coaches", "spankslifecoaches")]
    [TestCase("Équipe", "equipe")]
    public void TeamKey_ComparesNamesLooselyButWhole(string name, string key)
        => Assert.That(VodTitleParser.TeamKey(name), Is.EqualTo(key));

    // The catalog: two springs with the same team names, an archive summer, two Late League season 7s, a major and a minor

    private const int Spring24 = 1, Spring25 = 2, Summer26 = 3, LateH3 = 4, LateReach = 5, Major = 6, Minor = 7;
    private const int GameWeek1 = 100, GameWeek3 = 101, GameFinal = 102;

    private static VodCatalogTeam Team(int id, string name) => new(id, name, VodTitleParser.TeamKey(name));

    private static VodCatalogSeason Season(int id, string name, DateTime start, DateTime end, VodCatalogTeam[] teams, VodCatalogMatch[]? matches = null)
        => new(id, name, start, end, VodTitleParser.ParseSeasonName(name, start), teams, matches ?? []);

    private static readonly VodMatcher Matcher = new(
    [
        Season(Spring24, "Spring League 2024", D(2024, 3, 1), D(2024, 5, 31), [Team(11, "Alpha"), Team(12, "Bravo"), Team(13, "Spank's Life Coaches")],
        [
            new(GameWeek1, 11, 12, D(2024, 3, 1), false, false, null),
            new(GameWeek3, 12, 11, D(2024, 3, 15), false, false, null),
            new(GameFinal, 11, 12, D(2024, 5, 20), true, true, 3),
        ]),
        Season(Spring25, "Spring League 2025", D(2025, 3, 1), D(2025, 5, 31), [Team(21, "Alpha"), Team(22, "Bravo")]),
        Season(Summer26, "Summer League 2026 (Minor)", D(2026, 7, 1), D(2026, 10, 10), [Team(31, "Low IQ"), Team(32, "Reach for the Balls")]),
        Season(LateH3, "Late League Season 7 (Halo 3)", D(2019, 1, 1), D(2019, 12, 31), [Team(41, "Charlie"), Team(42, "Delta")]),
        Season(LateReach, "Late League Season 7 (Reach)", D(2019, 1, 1), D(2019, 12, 31), [Team(51, "Charlie"), Team(52, "Delta"), Team(53, "Echo"), Team(54, "Foxtrot")]),
        Season(Major, "Spring League 2026 (Major)", D(2026, 3, 1), D(2026, 5, 31), [Team(61, "Golf"), Team(62, "Hotel")],
            [new(600, 61, 62, null, false, false, null), new(601, 62, 61, null, false, false, null)]),
        Season(Minor, "Spring League 2026 (Minor)", D(2026, 3, 1), D(2026, 5, 31), [Team(71, "Golf"), Team(72, "Hotel")]),
    ]);

    [Test]
    public void Match_LinksTheWeeksMatch_WhenTheSeasonIsNamed()
    {
        var match = Matcher.Match("SL24 Week 3 - Alpha vs Bravo", D(2024, 3, 16));

        Assert.That((match.Status, match.SeasonID, match.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)Spring24, (int?)GameWeek3)));
        Assert.That(match.TeamIDs, Is.EquivalentTo(new[] { 11, 12 }));
    }

    [Test]
    public void Match_LinksTheGrandFinal_ByItsStage()
    {
        var final = Matcher.Match("Spring League 2024 Grand Final | Bravo vs Alpha", D(2024, 5, 21));
        var playoffs = Matcher.Match("SL24 Playoffs: Alpha vs Bravo", D(2024, 5, 21));

        Assert.Multiple(() =>
        {
            Assert.That((final.Status, final.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)GameFinal)));
            Assert.That((playoffs.Status, playoffs.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)GameFinal)), "The only playoff match between them");
        });
    }

    [Test]
    public void Match_IsTimeAware_WithoutASeasonInTheTitle()
    {
        // The same names play in both springs: the one running decides
        var spring25 = Matcher.Match("Alpha vs Bravo", D(2025, 4, 1));
        // A month after Spring League 2024's last match: its matches are guessed, so it's for review
        var spring24 = Matcher.Match("Alpha vs Bravo", D(2024, 7, 10));

        Assert.Multiple(() =>
        {
            Assert.That((spring25.Status, spring25.SeasonID, spring25.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)Spring25, (int?)null)), "No matches recorded: season and teams only");
            Assert.That((spring24.Status, spring24.SeasonID, spring24.SeasonMatchID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)Spring24, (int?)GameWeek3)));
            Assert.That(spring24.Note, Does.Contain("the one before it was published"));
        });
    }

    [Test]
    public void Match_TakesTheYearFromThePublishDate_AndTheDivisionWord()
    {
        var summer = Matcher.Match("AGLA Summer Minor Championship | BO7 | Low IQ vs Reach for the Balls | Halo Infinite Grifball", D(2026, 10, 16));
        var major = Matcher.Match("SL26 Major Week 1 - Golf vs Hotel", D(2026, 3, 2));

        Assert.Multiple(() =>
        {
            Assert.That((summer.Status, summer.SeasonID), Is.EqualTo((VodMatchStatus.Auto, (int?)Summer26)));
            Assert.That(summer.TeamIDs, Is.EquivalentTo(new[] { 31, 32 }));
            Assert.That(major.SeasonID, Is.EqualTo(Major), "Major, not the minor with the same team names");
            Assert.That((major.Status, major.SeasonMatchID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null)), "Two unscheduled matches between them: none picked, so for review");
        });
    }

    [Test]
    public void Match_LateLeague_ByTheTeams_OrForReviewWhenBothSeasonsHaveThem()
    {
        var reach = Matcher.Match("Late League S7 - Echo vs Foxtrot", D(2019, 6, 1));
        var both = Matcher.Match("Late League Season 7 - Charlie vs Delta", D(2019, 6, 1));

        Assert.Multiple(() =>
        {
            Assert.That((reach.Status, reach.SeasonID), Is.EqualTo((VodMatchStatus.Auto, (int?)LateReach)));
            Assert.That(both.Status, Is.EqualTo(VodMatchStatus.NeedsReview));
            Assert.That(both.Note, Does.Contain("2 seasons"));
        });
    }

    [Test]
    public void Match_SuggestsWhatItFound_WhenUnsure()
    {
        var oneTeam = Matcher.Match("Alpha vs Zulu", D(2024, 3, 20));
        var close = Matcher.Match("Spanks Life Coach vs Alpha", D(2024, 3, 20));
        var typo = Matcher.Match("Spank's Life Coachez vs Bravo", D(2024, 3, 20));
        var late = Matcher.Match("SL24 Week 1 - Alpha vs Bravo", D(2024, 8, 1));
        var namedSeasonOnly = Matcher.Match("SL24 - Yankee vs Zulu", D(2024, 3, 20));
        var nobody = Matcher.Match("DVEO vs. Scullen cx Bo3 Series (Observer POV)", D(2024, 3, 20));
        var beforeSeason = Matcher.Match("SL25 - Alpha vs Bravo", D(2024, 3, 20));

        Assert.Multiple(() =>
        {
            Assert.That((oneTeam.Status, oneTeam.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)Spring24)));
            Assert.That(oneTeam.TeamIDs, Is.EqualTo(new[] { 11 }));
            Assert.That((close.Status, close.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)Spring24)), "A close name is suggested, not linked");
            Assert.That(close.TeamIDs, Is.EquivalentTo(new[] { 11, 13 }));
            Assert.That(typo.TeamIDs, Is.EquivalentTo(new[] { 12, 13 }), "A letter off");
            Assert.That((late.Status, late.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)GameWeek1)), "Named season and week: sure even when uploaded late");
            Assert.That(late.Note, Does.Contain("published outside the season"));
            Assert.That((namedSeasonOnly.Status, namedSeasonOnly.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)Spring24)));
            Assert.That(namedSeasonOnly.TeamIDs, Is.Empty);
            Assert.That((nobody.Status, nobody.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null)));
            Assert.That((beforeSeason.Status, beforeSeason.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null)), "Never a season that started after it");
        });
    }

    [Test]
    public void Match_WithoutTeams_LinksOnlyANamedSeason()
    {
        var named = Matcher.Match("SL24 Draft Show", D(2024, 2, 20));
        var running = Matcher.Match("Fireside Chat with the Commish and Season Awards", D(2024, 3, 20));
        var nothing = Matcher.Match("PlayGrifball: Intramural Customs (Part 2)", D(2030, 1, 1));
        var several = Matcher.Match("Blitz League I", D(2019, 6, 1));

        Assert.Multiple(() =>
        {
            Assert.That((named.Status, named.SeasonID), Is.EqualTo((VodMatchStatus.Auto, (int?)Spring24)));
            Assert.That((running.Status, running.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)Spring24)));
            Assert.That((nothing.Status, nothing.SeasonID, nothing.Note), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null, "No season or teams recognised")));
            Assert.That((several.Status, several.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null)));
            Assert.That(several.Note, Does.Contain("2 seasons fit"));
        });
    }
}
