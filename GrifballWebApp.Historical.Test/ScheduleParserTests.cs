using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Test;

/// <summary>ScheduleParser on the archive's calendars (the sheets' own, trimmed to a few days).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ScheduleParserTests
{
    private static readonly string[] NoMonth = ["sml20/Full Schedule: rows of days of the month not read (the tab's Year and Month aren't set)"];

    private static SheetTable Sheet(params string[][] rows) => new() { Name = "Full Schedule", Rows = [.. rows] };

    private static List<CalendarMatch> Parse(ScheduleTab tab, SheetTable sheet, List<string>? warnings = null)
        => ScheduleParser.Parse("sml20", tab, sheet, warnings ?? []);

    /// <summary>9 pm Eastern (summer time), in UTC: 1 am the next day.</summary>
    private static DateTime NinePm(int year, int month, int day) => new DateTime(year, month, day, 1, 0, 0, DateTimeKind.Utc).AddDays(1);

    [Test]
    public void WeeksOfDays_DateTheMatchesBelowEachDay_TheNextMonthOnceTheDaysStartAgain()
    {
        // SML20's Full Schedule: a week's days of the month, its matches below; the standings to the right are no match
        var sheet = Sheet(
            ["", "", "", "Sunday", "", "Monday", "", "Tuesday", "", "", "", "", "", "", "", "", "", "Standings"],
            ["", "Week", "", "30", "", "31", "", "1", "", "", "", "", "", "", "", "", "", "Slytherin (15-1)"],
            ["", "1", "", "", "", "Bank Your Items! vs Slytherin 9:00", "", "ZAMF over Turquoise Jeep 2-0", "", "", "", "", "", "", "", "", "", "E-Dogs vs Zed 9:00"],
            ["", "", "", "", "", "", "", "Kill The Carrier ", "", "", "", "", "", "", "", "", "", "Turquoise Jeep (9-1)"],
            ["", "Week", "", "6", "", "7", "", "8"],
            ["", "2", "", "Circus Galvino vs. Redemption Runners 10:00"]);

        var matches = Parse(new ScheduleTab { Sheet = "Full Schedule", Year = 2020, Month = 5 }, sheet);

        Assert.That(matches, Is.EqualTo(new[]
        {
            new CalendarMatch(NinePm(2020, 5, 31), "Bank Your Items!", "Slytherin"),
            new CalendarMatch(new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc), "ZAMF", "Turquoise Jeep"),
            new CalendarMatch(NinePm(2020, 6, 6).AddHours(1), "Circus Galvino", "Redemption Runners"),
        }), "May 30 and 31, then June; a match with no time is its day; half a match in a cell is none");
    }

    [Test]
    public void ADateRow_DatesItsColumns_TheRowsFirstCellTheTime()
    {
        // SL19's Schedule: a Date row, each later row's first cell its time
        var sheet = Sheet(
            ["", "Sunday", "", "Monday"],
            ["Date", "5/19/2019", "", "5/20/2019", "", "Date"],
            ["9:00pm est", "", "", "Mon over Junk 5-3", "", "9:00pm est", "Team Name"],
            ["9:00pm est", "Doc over TT 5-1", "MP over Hype 5-2"]);

        var matches = Parse(new ScheduleTab { Sheet = "Schedule" }, sheet);

        Assert.That(matches, Is.EqualTo(new[]
        {
            new CalendarMatch(NinePm(2019, 5, 20), "Mon", "Junk"),
            new CalendarMatch(NinePm(2019, 5, 19), "Doc", "TT"),
            new CalendarMatch(NinePm(2019, 5, 19), "MP", "Hype"),
        }), "A day's column runs to the next day's");
    }

    [Test]
    public void AWeekdaysMonthAndDay_DateItsColumns_ScoresBetweenTheTeams()
    {
        // SL17's Schedule: "Monday | March | 27" per day, the year the tab's, into April
        var sheet = Sheet(
            ["Week One", "Friday", "March", "31", "Saturday", "April", "1"],
            ["09:00:00", "ND 5-3 IMP", "", "", "HT 5-1 PR"]);

        var matches = Parse(new ScheduleTab { Sheet = "Schedule", Year = 2017 }, sheet);

        Assert.That(matches.Select(m => (m.PlayedAt.ToUniversalTime().Date, m.First, m.Second)), Is.EqualTo(new[]
        {
            (new DateTime(2017, 4, 1), "ND", "IMP"), (new DateTime(2017, 4, 2), "HT", "PR"),
        }), "09:00:00 is 9 pm Eastern: the next day in UTC");
    }

    [TestCase("MM vs. LMT", "MM", "LMT")]
    [TestCase("2-0 G over Z", "G", "Z")]
    [TestCase("2 - 0 LMT over PIE", "LMT", "PIE")]
    [TestCase("0 MM vs. DBAGS 2", "MM", "DBAGS")]
    [TestCase("E-Dogs tied Redemption Runners 1-1", "E-Dogs", "Redemption Runners")]
    [TestCase("Grifballs Deep V. Free Agents 8pm", "Grifballs Deep", "Free Agents")]
    [TestCase("ASP (Dhmon) vs. FB (Saintz)", "ASP (Dhmon)", "FB (Saintz)")]
    [TestCase("2 Hype Crew vs Zero Gravity", "2 Hype Crew", "Zero Gravity")]
    public void AMatchsCell_NamesItsTwoTeams(string cell, string first, string second)
    {
        var matches = Parse(new ScheduleTab { Sheet = "Schedule" }, Sheet(["Date", "6/1/2020"], ["", cell]));

        Assert.That(matches.Select(m => (m.First, m.Second)), Is.EqualTo(new[] { (first, second) }));
    }

    [TestCase("Split Series 1 - 1")]
    [TestCase("No Games")]
    [TestCase("ALL GAME TIMES ARE EASTERN!!!!!")]
    [TestCase("TBD vs Alpha")]
    public void ACellThatIsNoMatch_IsLeftOut(string cell)
    {
        Assert.That(Parse(new ScheduleTab { Sheet = "Schedule" }, Sheet(["Date", "6/1/2020"], ["", cell])), Is.Empty);
    }

    [Test]
    public void ATimeInTheCellBelow_IsTheMatchs()
    {
        // WL21's Legacy Schedule: "8 PM Est." below the match
        var matches = Parse(new ScheduleTab { Sheet = "Legacy Schedule" }, Sheet(["Date", "1/18/2021"], ["", "BFH vs. OD"], ["", "8 PM Est."]));

        Assert.That(matches.Select(m => m.PlayedAt), Is.EqualTo(new[] { new DateTime(2021, 1, 19, 1, 0, 0, DateTimeKind.Utc) }), "8 pm Eastern (winter time)");
    }

    [Test]
    public void DaysOfTheMonth_WithNoMonthSet_AreNotRead_AndSaySo()
    {
        var warnings = new List<string>();

        var matches = Parse(new ScheduleTab { Sheet = "Full Schedule" }, Sheet(["Week", "2", "3", "4"], ["1", "A vs B"]), warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches, Is.Empty);
            Assert.That(warnings, Is.EqualTo(NoMonth));
        }
    }

    [Test]
    public void AMonthAndDayCell_DatesItsColumn_TheYearTheTabs()
    {
        // GGL SML17's Schedule: "July 31st" per day, into August
        var sheet = Sheet(
            ["Date", "July 31st", "", "August 1st", "", "Date"],
            ["9:00pm est", "OMG over NH 5-0", "DROP over DJ 5-3", "ARK over 4G1B 5-0", "", "9:00pm est"]);

        var matches = Parse(new ScheduleTab { Sheet = "Schedule", Year = 2017 }, sheet);

        Assert.That(matches.Select(m => (m.PlayedAt, m.First, m.Second)), Is.EqualTo(new[]
        {
            (NinePm(2017, 7, 31), "OMG", "NH"), (NinePm(2017, 7, 31), "DROP", "DJ"), (NinePm(2017, 8, 1), "ARK", "4G1B"),
        }));
    }

    [Test]
    public void AWeeksRow_DatesItsOwnCells_ByTheWeekdayHeaders_EachMatchTheTimeAfterIt()
    {
        // GWL SL23's Schedule: weekday headers, then a row per week, its label first, its matches beside it
        var sheet = Sheet(
            ["AWAY @ HOME", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday ", "Saturday "],
            ["March 26th - April 1st", "", "Dhlyn @ ActiveHate           7PM EST              ActiveHate @ Dhlyn         Double Header", "", "", "", "", "Mavbrid @ oDhmon 10PM EST", "Blue = Rescheduled Games"]);

        var matches = Parse(new ScheduleTab { Sheet = "Schedule", Year = 2023 }, sheet);

        Assert.That(matches, Is.EqualTo(new[]
        {
            new CalendarMatch(new DateTime(2023, 3, 27, 23, 0, 0, DateTimeKind.Utc), "Dhlyn", "ActiveHate"),
            new CalendarMatch(new DateTime(2023, 3, 27, 0, 0, 0, DateTimeKind.Utc), "ActiveHate", "Dhlyn"),
            new CalendarMatch(new DateTime(2023, 4, 2, 2, 0, 0, DateTimeKind.Utc), "Mavbrid", "oDhmon"),
        }), "Monday the 27th, Saturday April 1st; the legend right of Saturday is no day's");
    }
}
