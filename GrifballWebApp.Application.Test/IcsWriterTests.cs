using System.Text;
using GrifballWebApp.Application.Calendar;

namespace GrifballWebApp.Application.Test;

/// <summary>IcsWriter: the RFC 5545 rules a calendar app holds a feed to (TEXT escaping, lines folded at 75 octets,
/// CRLF line ends, UTC times) and the calendar and events it writes.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class IcsWriterTests
{
    private static readonly DateTime Start = new(2026, 11, 20, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Modified = new(2026, 11, 10, 12, 30, 15, DateTimeKind.Utc);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static CalendarEvent Event(string summary = "Red vs Blue (Season 5)", string? description = null, string? url = "https://grif.test/seasonmatch/7")
        => new("seasonmatch-7@grifballwebapp", Start, Start.AddMinutes(30), summary, description, url, Modified);

    /// <summary>The feed's lines as a calendar app reads them: folded lines joined again (a CRLF and one space removed).</summary>
    private static string[] Unfolded(string ics) => ics.Replace("\r\n ", "").Split("\r\n");

    [TestCase("plain", "plain")]
    [TestCase(@"back\slash", @"back\\slash")]
    [TestCase("semi;colon", @"semi\;colon")]
    [TestCase("com,ma", @"com\,ma")]
    [TestCase("two\nlines", @"two\nlines")]
    [TestCase("two\r\nlines", @"two\nlines")]
    [TestCase("lone\rreturn", @"lone\nreturn")]
    [TestCase("ends\r", @"ends\n")]
    [TestCase("colon: kept", "colon: kept")]
    [TestCase("tab\tkept", "tab\tkept")]
    [TestCase("bell\u0007gone", "bellgone")]
    [TestCase("delete\u007Fgone", "deletegone")]
    [TestCase(@"all\;,", @"all\\\;\,")]
    public void EscapeText_EscapesWhatATextValueMayNotHold(string text, string expected)
    {
        Assert.That(IcsWriter.EscapeText(text), Is.EqualTo(expected));
    }

    [Test]
    public void FoldLine_LeavesALineOf75OctetsAsItIs()
    {
        var line = new string('a', 75);

        Assert.That(IcsWriter.FoldLine(line), Is.EqualTo(line));
    }

    [Test]
    public void FoldLine_FoldsALineOf76Octets_AfterThe75th()
    {
        var line = new string('a', 76);

        Assert.That(IcsWriter.FoldLine(line), Is.EqualTo(new string('a', 75) + "\r\n a"));
    }

    [Test]
    public void FoldLine_KeepsEveryLineTo75Octets_TheContinuationsSpaceIncluded()
    {
        var line = string.Concat(Enumerable.Repeat("0123456789", 20));

        var lines = IcsWriter.FoldLine(line).Split("\r\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines, Has.Length.EqualTo(3));
            Assert.That(lines.Select(l => Encoding.UTF8.GetByteCount(l)), Is.All.LessThanOrEqualTo(IcsWriter.MaxLineOctets));
            Assert.That(lines.Skip(1), Is.All.StartsWith(" "));
            Assert.That(string.Join("", lines.Take(1).Concat(lines.Skip(1).Select(l => l[1..]))), Is.EqualTo(line));
        }
    }

    [TestCase("é", TestName = "FoldLine_NeverSplitsATwoOctetCharacter")]
    [TestCase("€", TestName = "FoldLine_NeverSplitsAThreeOctetCharacter")]
    [TestCase("\U0001F3C8", TestName = "FoldLine_NeverSplitsASurrogatePair")]
    public void FoldLine_NeverSplitsACharacter(string character)
    {
        var line = "SUMMARY:" + string.Concat(Enumerable.Repeat(character, 60));

        var lines = IcsWriter.FoldLine(line).Split("\r\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines, Has.Length.GreaterThan(1));
            // A lone surrogate or a character cut in two can't be encoded: the strict encoder throws on it
            Assert.That(lines.Select(l => StrictUtf8.GetByteCount(l)), Is.All.LessThanOrEqualTo(IcsWriter.MaxLineOctets));
            Assert.That(string.Join("", lines.Take(1).Concat(lines.Skip(1).Select(l => l[1..]))), Is.EqualTo(line));
        }
    }

    [Test]
    public void FormatUtc_WritesAUtcTimeInTheBasicFormat()
    {
        Assert.That(IcsWriter.FormatUtc(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)), Is.EqualTo("20260102T030405Z"));
    }

    [Test]
    public void FormatUtc_TakesATimeOfNoKindAsUtc_AsTheDatabaseReturnsIt()
    {
        Assert.That(IcsWriter.FormatUtc(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)), Is.EqualTo("20260102T030405Z"));
    }

    [Test]
    public void FormatUtc_ConvertsALocalTimeToUtc()
    {
        var local = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);

        Assert.That(IcsWriter.FormatUtc(local), Is.EqualTo(IcsWriter.FormatUtc(local.ToUniversalTime())));
    }

    [Test]
    public void Write_EndsEveryLineWithCrlf_AndHasNoOtherLineBreak()
    {
        var ics = IcsWriter.Write(new CalendarFeed("Red", [Event(summary: new string('x', 200), description: "one\ntwo")]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ics, Does.EndWith("END:VCALENDAR\r\n"));
            Assert.That(ics.Replace("\r\n", ""), Does.Not.Contain("\n").And.Not.Contain("\r"));
            Assert.That(ics.Split("\r\n").Select(l => Encoding.UTF8.GetByteCount(l)), Is.All.LessThanOrEqualTo(IcsWriter.MaxLineOctets));
        }
    }

    private static readonly string[] EmptyCalendar =
    [
        "BEGIN:VCALENDAR",
        "VERSION:2.0",
        "PRODID:" + IcsWriter.ProductID,
        "CALSCALE:GREGORIAN",
        @"X-WR-CALNAME:Red\, Blue\; and \\ Green",
        "REFRESH-INTERVAL;VALUE=DURATION:PT1H",
        "X-PUBLISHED-TTL:PT1H",
        "END:VCALENDAR",
        "",
    ];

    [Test]
    public void Write_ACalendarWithoutEvents_IsTheCalendarAlone()
    {
        var lines = Unfolded(IcsWriter.Write(new CalendarFeed("Red, Blue; and \\ Green", [])));

        Assert.That(lines, Is.EqualTo(EmptyCalendar));
    }

    private static readonly string[] OneEvent =
    [
        "UID:seasonmatch-7@grifballwebapp",
        "DTSTAMP:20261110T123015Z",
        "LAST-MODIFIED:20261110T123015Z",
        "DTSTART:20261120T010000Z",
        "DTEND:20261120T013000Z",
        @"SUMMARY:Red vs Blue\, finals",
        @"DESCRIPTION:Best of 3\nhttps://grif.test/seasonmatch/7",
        "URL:https://grif.test/seasonmatch/7",
    ];

    [Test]
    public void Write_AnEvent_HasItsUidTimesSummaryDescriptionAndUrl()
    {
        var lines = Unfolded(IcsWriter.Write(new CalendarFeed("Red", [Event(summary: "Red vs Blue, finals", description: "Best of 3\nhttps://grif.test/seasonmatch/7")])));

        var vevent = lines.SkipWhile(l => l != "BEGIN:VEVENT").TakeWhile(l => l != "END:VEVENT").Skip(1).ToArray();
        Assert.That(vevent, Is.EqualTo(OneEvent));
    }

    [Test]
    public void Write_AnEventWithoutDescriptionOrUrl_LeavesThemOut()
    {
        var lines = Unfolded(IcsWriter.Write(new CalendarFeed("Red", [Event(url: null)])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines, Has.None.StartsWith("DESCRIPTION"));
            Assert.That(lines, Has.None.StartsWith("URL"));
            Assert.That(lines, Has.One.EqualTo("BEGIN:VEVENT"));
        }
    }

    private static readonly string[] TwoUids = ["UID:a@grifballwebapp", "UID:b@grifballwebapp"];

    [Test]
    public void Write_EachEventInTheOrderGiven()
    {
        var first = Event() with { Uid = "a@grifballwebapp" };
        var second = Event() with { Uid = "b@grifballwebapp" };

        var lines = Unfolded(IcsWriter.Write(new CalendarFeed("Red", [first, second])));

        Assert.That(lines.Where(l => l.StartsWith("UID:", StringComparison.Ordinal)), Is.EqualTo(TwoUids));
    }
}
