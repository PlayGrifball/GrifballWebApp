using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Historical.Parsing;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// How the historical archive reads single cells, case by case: which schedule cells name a team and which are a round
/// marker, a score, a time or a link (WaypointMatchParser.TeamName), the match IDs a cell links to, the number a cell
/// starts with, a gamertag's trade and role notes (ArchiveCells), a carnage report's timestamp, and Halo's HTML error
/// pages cut to their title (HaloErrors). No workbook: one cell at a time.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalCellCasesTests
{
    // Round markers: a whole cell of a week or a playoff round
    [TestCase("Week3")]
    [TestCase("week 12")]
    [TestCase("Playoff")]
    [TestCase("Playoff Round 2")]
    [TestCase("Playoffs rd 3")]
    [TestCase("Playoffs Week2")]
    [TestCase("Winners Playoffs")]
    [TestCase("losers bracket")]
    [TestCase("Upper Finals")]
    [TestCase("Lower Semis")]
    [TestCase("Grand Final")]
    [TestCase("grand  finals")]
    [TestCase("Final")]
    [TestCase("Finals Game 2")]
    [TestCase("Semi-Finals")]
    [TestCase("Semifinal")]
    [TestCase("Quarterfinals")]
    [TestCase("Quarter-Final")]
    [TestCase("Quarters")]
    [TestCase("Bracket Week 1")]
    // Scores
    [TestCase("10-2")]
    [TestCase("5 – 3")]
    [TestCase("3-1 OT")]
    [TestCase("2 - 0 (ff)")]
    [TestCase(" 4-4 ")]
    // Times
    [TestCase("8:30")]
    [TestCase("9:00pm")]
    [TestCase("12:15 AM")]
    [TestCase("Mon 9")]
    [TestCase("Saturday 7:30 pm")]
    [TestCase("Tue 10pm")]
    [TestCase("Fri 8:00")]
    [TestCase("thu 11 AM")]
    [TestCase("wed 12:30pm")]
    [TestCase("13:45")]
    [TestCase("7:05 Pm")]
    [TestCase("SAT 10:15 am")]
    [TestCase("12-12 (2OT)")]
    [TestCase("3 -1")]
    [TestCase("0–0 tie")]
    [TestCase("WEEK 1")]
    [TestCase("Semis")]
    [TestCase("Winners Semi-Final")]
    [TestCase("lower bracket")]
    [TestCase("Upper Quarterfinals Round 2")]
    [TestCase("Grand Final Game 3")]
    // Links, IDs, numbers and placeholders
    [TestCase("halodatahive.com/match/1")]
    [TestCase("HALOQUERY.COM")]
    [TestCase("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    [TestCase("3.5")]
    [TestCase("Away Team")]
    [TestCase("vs.")]
    public void TeamName_ACellThatNamesNoTeam_IsNull(string cell)
        => Assert.That(WaypointMatchParser.TeamName(cell), Is.Null);

    [TestCase("Semis Squad", "Semis Squad")]
    [TestCase("Week Warriors", "Week Warriors")]
    [TestCase("Final Boss", "Final Boss")]
    [TestCase("Winners", "Winners")]
    [TestCase("Upper Deck", "Upper Deck")]
    [TestCase("Grand Theft", "Grand Theft")]
    [TestCase("Quarterback Club", "Quarterback Club")]
    [TestCase("Weeknd 5ever", "Weeknd 5ever")]
    [TestCase("Monday Night", "Monday Night")]
    [TestCase("Sat Squad", "Sat Squad")]
    [TestCase("8 Ball", "8 Ball")]
    [TestCase("10-Man Army", "10-Man Army")]
    [TestCase("Ten  Spaced   Out", "Ten Spaced Out")]
    [TestCase("Tuna (c), ", "Tuna")]
    [TestCase("Wed Nite Crew", "Wed Nite Crew")]
    [TestCase("8:30 Club", "8:30 Club")]
    [TestCase("Sun Devils 2", "Sun Devils 2")]
    [TestCase("Week 3 Heroes", "Week 3 Heroes")]
    [TestCase("Finals Bound", "Finals Bound")]
    [TestCase("Bracketeers", "Bracketeers")]
    [TestCase("Lower Decks", "Lower Decks")]
    [TestCase("Semifinalists", "Semifinalists")]
    [TestCase("3-Peat", "3-Peat")]
    [TestCase("12 Monkeys", "12 Monkeys")]
    [TestCase("Halo Waypoint Fans", "Halo Waypoint Fans")]
    [TestCase("Sunday 8", "Sunday 8")]
    [TestCase("Thursday 9:00PM", "Thursday 9:00PM")]
    [TestCase("Quarter Final", "Quarter Final")]
    public void TeamName_ATeamsName_IsKept_Cleaned(string cell, string expected)
        => Assert.That(WaypointMatchParser.TeamName(cell), Is.EqualTo(expected));

    [Test]
    public void MatchIds_TheLastIdOfEachLink_LowerCase()
    {
        const string cell = "https://www.halowaypoint.com/halo-infinite/players/11111111-2222-3333-4444-555555555555/matches/AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE; "
            + "https://halodatahive.com/Infinite/Match/ffffffff-0000-1111-2222-333333333333";

        Assert.That(WaypointMatchParser.MatchIds(cell), Is.EqualTo(ExpectedIds));
    }

    private static readonly string[] ExpectedIds = ["aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "ffffffff-0000-1111-2222-333333333333"];

    [Test]
    public void MatchIds_NoLink_IsNone_AndALinkWithoutAnId_IsNull()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(WaypointMatchParser.MatchIds("Red Team won"), Is.Empty);
            Assert.That(WaypointMatchParser.MatchIds(null), Is.Empty);
            Assert.That(WaypointMatchParser.MatchIds("https://haloquery.com/players/someone"), Is.Null);
        }
    }

    [TestCase("42.21 min", 42.21)]
    [TestCase("-3.5x", -3.5)]
    [TestCase("+7", 7)]
    [TestCase(".5 s", 0.5)]
    [TestCase("1e3 points", 1000)]
    [TestCase("2.5E-1", 0.25)]
    [TestCase("12.", 12)]
    [TestCase("  9 kills", 9)]
    [TestCase("-.5", -0.5)]
    [TestCase("+.25e2 pts", 25)]
    [TestCase("5e kills", 5)]
    [TestCase("1.2.3", 1.2)]
    [TestCase("007", 7)]
    [TestCase("3E+2", 300)]
    [TestCase("-0", 0)]
    public void LeadingNumber_IsTheNumberTheCellStartsWith(string cell, double expected)
        => Assert.That(ArchiveCells.LeadingNumber(cell), Is.EqualTo((decimal)expected));

    [TestCase("kills: 9")]
    [TestCase("")]
    [TestCase("e5")]
    [TestCase("+")]
    [TestCase(".")]
    [TestCase("-e3")]
    [TestCase(" .x")]
    [TestCase("-")]
    public void LeadingNumber_NoneAtTheStart_IsNull(string? cell)
        => Assert.That(ArchiveCells.LeadingNumber(cell), Is.Null);

    [TestCase("Tuna (from Rayzyr)", "Tuna", "from Rayzyr", null)]
    [TestCase("Rayzyr (S)", "Rayzyr", null, "s")]
    [TestCase("Rayzyr (cab)", "Rayzyr", null, "cab")]
    [TestCase("Rayzyr (sub) (TO Tuna)", "Rayzyr", "TO Tuna", "sub")]
    [TestCase("Rayzyr (x)", "Rayzyr (x)", null, null)]
    [TestCase("Tuna (from  Two Words)", "Tuna", "from  Two Words", null)]
    [TestCase("Tuna (to)", "Tuna (to)", null, null)]
    [TestCase("Tuna (TO Rayzyr", "Tuna (TO Rayzyr", null, null)]
    [TestCase("Tuna (CS)", "Tuna", null, "cs")]
    [TestCase("Tuna (c)(s)", "Tuna (s)", null, "c")]
    [TestCase("Tuna (A) ,, ", "Tuna", null, "a")]
    [TestCase("(sub)", "", null, "sub")]
    [TestCase("Rayzyr,  ", "Rayzyr", null, null)]
    [TestCase("  ", "", null, null)]
    public void Gamertag_SplitsOffItsNotes(string cell, string gamertag, string? trade, string? role)
        => Assert.That(ArchiveCells.Gamertag(cell), Is.EqualTo((gamertag, trade, role)));

    [Test]
    public void Timestamp_ReadsEachFormatTheReportsUse_AsUtc()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RecordedGameParser.Timestamp("Mon  Jul 11 18:02:15   2022"), Is.EqualTo(new DateTime(2022, 7, 11, 18, 2, 15, DateTimeKind.Utc)));
            Assert.That(RecordedGameParser.Timestamp("7/11/2022 18:02:15"), Is.EqualTo(new DateTime(2022, 7, 11, 18, 2, 15, DateTimeKind.Utc)));
            Assert.That(RecordedGameParser.Timestamp("7/11/2022 6:02"), Is.EqualTo(new DateTime(2022, 7, 11, 6, 2, 0, DateTimeKind.Utc)));
            Assert.That(RecordedGameParser.Timestamp("7/11/2022 6:02")?.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(RecordedGameParser.Timestamp("0"), Is.Null);
            Assert.That(RecordedGameParser.Timestamp(" "), Is.Null);
        }
    }

    [TestCase("<html><head><title class=\"err\">504 Gateway Time-out</title></head></html>", "an HTML error page (504 Gateway Time-out)")]
    [TestCase("<HTML><TITLE> Bad Gateway </TITLE></HTML>", "an HTML error page (Bad Gateway)")]
    [TestCase("<html><title></title><body>x</body></html>", "an HTML error page (no title)")]
    [TestCase("<html><body><h1>title</h1></body></html>", "an HTML error page (no title)")]
    [TestCase("<html><titles>not it</titles><title>It</title></html>", "an HTML error page (It)")]
    public void Describe_AnHtmlErrorPage_IsItsTitle(string message, string expected)
        => Assert.That(HaloErrors.Describe(message), Is.EqualTo(expected));
}
