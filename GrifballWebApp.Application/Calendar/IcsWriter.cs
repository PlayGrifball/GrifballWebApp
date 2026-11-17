using System.Globalization;
using System.Text;

namespace GrifballWebApp.Application.Calendar;

/// <summary>An event of a calendar feed: one VEVENT (<see cref="IcsWriter"/>).</summary>
/// <param name="Uid">What identifies the event for good, so a calendar app updates it in place when it changes.</param>
/// <param name="Start">When it starts (UTC).</param>
/// <param name="End">When it ends (UTC).</param>
/// <param name="Summary">Its title.</param>
/// <param name="Description">Its notes, if any.</param>
/// <param name="Url">Its page, if any.</param>
/// <param name="LastModified">When it last changed (UTC): its DTSTAMP and LAST-MODIFIED.</param>
public sealed record CalendarEvent(string Uid, DateTime Start, DateTime End, string Summary, string? Description, string? Url, DateTime LastModified);

/// <summary>A calendar feed: its name, as a calendar app shows it, and its events.</summary>
/// <param name="Name">The calendar's name (X-WR-CALNAME).</param>
/// <param name="Events">Its events, in the order written.</param>
public sealed record CalendarFeed(string Name, IReadOnlyList<CalendarEvent> Events);

/// <summary>
/// Writes a <see cref="CalendarFeed"/> as an iCalendar object (RFC 5545), which Google Calendar, Apple Calendar and
/// Outlook subscribe to by URL. Hand-written, as it needs so little of the standard: TEXT values escaped
/// (<see cref="EscapeText"/>), every line ended with CRLF and folded at 75 octets (<see cref="FoldLine"/>), and times in
/// UTC (<see cref="FormatUtc"/>), so no VTIMEZONE is needed. No METHOD: a published feed, whose DTSTAMP is then the time
/// the event last changed (RFC 5545 3.8.7.2), so the same data always gives the same feed.
/// </summary>
public static class IcsWriter
{
    /// <summary>The feed's media type (RFC 5545 8.1), in UTF-8, its default charset.</summary>
    public const string ContentType = "text/calendar; charset=utf-8";

    /// <summary>The PRODID: the product that made the feed.</summary>
    public const string ProductID = "-//PlayGrifball//GrifballWebApp//EN";

    /// <summary>The longest a line may be, in octets, its line break left out (RFC 5545 3.1).</summary>
    public const int MaxLineOctets = 75;

    /// <summary>How often a calendar app should fetch the feed again, if it reads the hint (an ISO 8601 duration).</summary>
    private const string RefreshInterval = "PT1H";

    private const string LineBreak = "\r\n";

    /// <summary>The feed as an iCalendar object: a VCALENDAR with a VEVENT for each event, every line folded and ended with CRLF.</summary>
    public static string Write(CalendarFeed feed)
    {
        var ics = new StringBuilder();
        Line(ics, "BEGIN:VCALENDAR");
        Line(ics, "VERSION:2.0");
        Line(ics, $"PRODID:{ProductID}");
        Line(ics, "CALSCALE:GREGORIAN");
        Line(ics, $"X-WR-CALNAME:{EscapeText(feed.Name)}");
        // RFC 7986's refresh hint, and the one Outlook reads
        Line(ics, $"REFRESH-INTERVAL;VALUE=DURATION:{RefreshInterval}");
        Line(ics, $"X-PUBLISHED-TTL:{RefreshInterval}");
        foreach (var e in feed.Events)
            Event(ics, e);
        Line(ics, "END:VCALENDAR");
        return ics.ToString();
    }

    private static void Event(StringBuilder ics, CalendarEvent e)
    {
        Line(ics, "BEGIN:VEVENT");
        Line(ics, $"UID:{EscapeText(e.Uid)}");
        Line(ics, $"DTSTAMP:{FormatUtc(e.LastModified)}");
        Line(ics, $"LAST-MODIFIED:{FormatUtc(e.LastModified)}");
        Line(ics, $"DTSTART:{FormatUtc(e.Start)}");
        Line(ics, $"DTEND:{FormatUtc(e.End)}");
        Line(ics, $"SUMMARY:{EscapeText(e.Summary)}");
        if (e.Description is not null)
            Line(ics, $"DESCRIPTION:{EscapeText(e.Description)}");
        // A URI, not TEXT: written as it is
        if (e.Url is not null)
            Line(ics, $"URL:{e.Url}");
        Line(ics, "END:VEVENT");
    }

    private static void Line(StringBuilder ics, string line) => ics.Append(FoldLine(line)).Append(LineBreak);

    /// <summary>
    /// A TEXT value as RFC 5545 3.3.11 writes it: a backslash, semicolon and comma escaped with a backslash, a line break
    /// (CRLF, LF or CR) as \n, and the other control characters, which TEXT may not hold, left out (a tab stays).
    /// </summary>
    public static string EscapeText(string value)
    {
        var text = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '\\' or ';' or ',':
                    text.Append('\\').Append(c);
                    break;
                case '\r' when i + 1 < value.Length && value[i + 1] == '\n':
                    // The CR of a CRLF: its LF writes the line break
                    break;
                case '\r' or '\n':
                    text.Append("\\n");
                    break;
                case '\t':
                    text.Append(c);
                    break;
                default:
                    if (!char.IsControl(c))
                        text.Append(c);
                    break;
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// A content line folded as RFC 5545 3.1 says: no line longer than <see cref="MaxLineOctets"/> octets of UTF-8, each
    /// continuation a CRLF and a space (which counts toward its line's octets), and never inside a character: a
    /// character of several octets, or a surrogate pair, stays whole on one line. The last line's CRLF is the caller's.
    /// </summary>
    public static string FoldLine(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= MaxLineOctets)
            return line;

        var folded = new StringBuilder(line.Length + line.Length / 32 * 3);
        var octets = 0;
        var i = 0;
        while (i < line.Length)
        {
            var length = char.IsSurrogatePair(line, i) ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(line.AsSpan(i, length));
            if (octets + size > MaxLineOctets)
            {
                folded.Append(LineBreak).Append(' ');
                octets = 1;
            }
            folded.Append(line, i, length);
            octets += size;
            i += length;
        }
        return folded.ToString();
    }

    /// <summary>
    /// A time as a UTC DATE-TIME (RFC 5545 3.3.5, form 2): 20261120T010000Z. A time of no kind, as the database gives it,
    /// is UTC already (the app stores UTC); a local one is converted.
    /// </summary>
    public static string FormatUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }
}
