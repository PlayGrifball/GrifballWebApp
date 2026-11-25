using System.Globalization;
using System.Text.RegularExpressions;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>A match on a season's schedule: when it was played, and its two teams as the calendar names them.</summary>
public sealed record CalendarMatch(DateTime PlayedAt, string First, string Second);

/// <summary>
/// A season's schedule (<see cref="ScheduleTab"/>): a calendar of rows of days, each day a column, its matches in the cells
/// below it up to the next row of days. A row of days is any of:
/// <list type="bullet">
/// <item>dates ("5/19/2019"), as SL19's "Date" row;</item>
/// <item>a weekday's month and day in the cells after it ("Monday | March | 27"), as SL17's weeks, the year the tab's;</item>
/// <item>three or more days of the month in a row, each the day after the last ("2 | 3 | 4"), as SML20's and FL20's weeks:
/// the first row's month is the tab's, and a day earlier than the last is the next month's.</item>
/// </list>
/// A day's column runs to the next day's (the last day's as wide as the one before it): what is left of the first day (a
/// time, a week's label) and right of the last (the standings) is no match. A match is "A vs. B", "A @ B", "A over B 2-0",
/// "A tied B 1-1", "2-0 A over B", "ND 5-3 IMP" or "9:30pm: A (5) vs. B (1)", a cell's lines each a match; its time
/// ("9:00", "8pm", "8 PM Est.") is in its line, the row's first
/// cell or the cell below it, Eastern time, in the evening when it says no am or pm. A day without a time is its date at
/// midnight UTC, as the archive's other undated days.
/// </summary>
public static partial class ScheduleParser
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly string[] Months = ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];
    private static readonly string[] Weekdays = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    public static List<CalendarMatch> Parse(string sourceId, ScheduleTab tab, SheetTable sheet, List<string> warnings)
    {
        var dayOf = Grid(sheet, tab.Year, tab.Month, out var needsMonth);
        var matches = new List<CalendarMatch>();
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                if (dayOf(r, c) is not DateTime day)
                    continue;
                // A line each ("9:30pm: Impulse (5) vs. High Voltage (1)", a blank line, the next)
                foreach (var line in row[c].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var timed = LeadingTime().Match(line);
                    if (Teams(timed.Success ? line[timed.Length..] : line) is not { } teams)
                        continue;
                    var time = Time(line) ?? Time(row[0]) ?? (r + 1 < sheet.Rows.Count && TimeOnly().IsMatch(sheet.Cell(r + 1, c).Trim()) ? Time(sheet.Cell(r + 1, c)) : null);
                    matches.Add(new CalendarMatch(At(day, time), teams.First, teams.Second));
                }
            }
        }
        if (needsMonth)
            warnings.Add($"{sourceId}/{tab.Sheet}: rows of days of the month not read (the tab's Year and Month aren't set)");
        return matches;
    }

    /// <summary>The day of each cell of a calendar (a result list's, <see cref="ResultsLayout.Calendar"/>), as <see cref="Parse"/>
    /// reads its rows of days: null for a cell under no day.</summary>
    public static Func<int, int, DateTime?> DayOfCell(SheetTable sheet, int? year, int? month) => Grid(sheet, year, month, out _);

    /// <summary>Each cell's day: the one above it in the last row of days (none in a row of days itself).</summary>
    private static Func<int, int, DateTime?> Grid(SheetTable sheet, int? year, int? month, out bool needsMonth)
    {
        var calendar = new Calendar(year, month);
        var rows = new (List<(int Column, DateTime Day)>? Days, int Width)[sheet.Rows.Count];
        List<(int Column, DateTime Day)>? days = null;
        var width = 1;
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            if (calendar.Days(sheet.Rows[r]) is { Count: > 0 } found)
            {
                width = found.Count > 1 ? found.Zip(found.Skip(1), (a, b) => b.Column - a.Column).Min() : width;
                days = found;
                continue;
            }
            rows[r] = (days, width);
        }
        needsMonth = calendar.NeedsMonth;
        return (r, c) => r < rows.Length && rows[r].Days is { } d ? DayOf(d, rows[r].Width, c) : null;
    }

    /// <summary>A day at the time a text gives ("9:30pm"), Eastern, in UTC; midnight UTC when it gives none.</summary>
    public static DateTime At(DateTime day, string? timeText) => At(day, timeText is null ? null : Time(timeText));

    /// <summary>The day whose column holds <paramref name="column"/>; null left of the first day and right of the last.</summary>
    private static DateTime? DayOf(List<(int Column, DateTime Day)> days, int width, int column)
    {
        for (var i = days.Count - 1; i >= 0; i--)
        {
            if (column < days[i].Column)
                continue;
            var end = i + 1 < days.Count ? days[i + 1].Column : days[i].Column + width;
            return column < end ? days[i].Day : null;
        }
        return null;
    }

    /// <summary>The day at the time (Eastern), in UTC; midnight UTC without one.</summary>
    private static DateTime At(DateTime day, TimeSpan? time)
        => time is { } at
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Date + at, DateTimeKind.Unspecified), Eastern)
            : DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);

    /// <summary>The calendar's days as its rows give them, the month carried from row to row.</summary>
    private sealed class Calendar(int? year, int? month)
    {
        private DateTime? _last;

        /// <summary>A row of days of the month was found with no month to put it in.</summary>
        public bool NeedsMonth { get; private set; }

        /// <summary>The row's days, each with its column; empty when it is no row of days.</summary>
        public List<(int Column, DateTime Day)> Days(string[] row)
        {
            var dates = Enumerable.Range(0, row.Length).Select(c => (Column: c, Day: Date(row[c]))).Where(d => d.Day is not null)
                .Select(d => (d.Column, Day: d.Day!.Value)).ToList();
            if (dates.Count > 0)
                return Seen(dates);
            var named = MonthDays(row);
            if (named.Count > 0)
                return Seen(named);
            var run = DayRun(row);
            if (run.Count < 3)
                return [];
            if (year is null || month is null)
            {
                NeedsMonth = true;
                return [];
            }
            var days = new List<(int Column, DateTime Day)>();
            var (y, m) = _last is { } last ? (last.Year, last.Month) : (year.Value, month.Value);
            foreach (var (column, day) in run)
            {
                // A day earlier than the last is the next month's (a week across its end)
                while (day > DateTime.DaysInMonth(y, m) || (_last is { } before && new DateTime(y, m, day) < before.AddDays(-3)))
                    (y, m) = m == 12 ? (y + 1, 1) : (y, m + 1);
                var date = new DateTime(y, m, day);
                days.Add((column, date));
                _last = date;
            }
            return days;
        }

        private List<(int Column, DateTime Day)> Seen(List<(int Column, DateTime Day)> days)
        {
            _last = days[^1].Day;
            return days;
        }

        /// <summary>"Monday | March | 27": the weekday's column, the month and day after it; or "July 31st" in a cell. The
        /// year is the tab's (the next one once the months start again).</summary>
        private List<(int Column, DateTime Day)> MonthDays(string[] row)
        {
            var days = new List<(int Column, DateTime Day)>();
            for (var c = 0; c < row.Length; c++)
            {
                var named = MonthAndDay().Match(row[c].Trim());
                var m = named.Success ? MonthOf(named.Groups["month"].Value) : c + 1 < row.Length ? MonthOf(row[c]) : 0;
                if (m == 0 || year is null || (named.Success ? Day(named.Groups["day"].Value) : Day(row[c + 1])) is not int day)
                    continue;
                var y = _last is { } last ? last.Year + (m < last.Month ? 1 : 0) : year.Value;
                if (day > DateTime.DaysInMonth(y, m))
                    continue;
                var column = c > 0 && Weekdays.Contains(row[c - 1].Trim().ToLowerInvariant()) ? c - 1 : c;
                days.Add((column, new DateTime(y, m, day)));
                _last = days[^1].Day;
            }
            return days;
        }

        /// <summary>The row's first run of days of the month, left to right, each the day after the one before (or the 1st
        /// after the end of a month).</summary>
        private static List<(int Column, int Day)> DayRun(string[] row)
        {
            var run = new List<(int Column, int Day)>();
            for (var c = 0; c < row.Length; c++)
            {
                if (row[c].Trim().Length == 0)
                    continue;
                if (Day(row[c]) is not int day)
                {
                    if (run.Count > 0)
                        break;
                    continue;
                }
                if (run.Count > 0 && day != run[^1].Day + 1 && !(day == 1 && run[^1].Day >= 28))
                    break;
                run.Add((c, day));
            }
            return run;
        }
    }

    /// <summary>A month's number by its name ("March", "Mar"); 0 for anything else.</summary>
    private static int MonthOf(string name)
    {
        var text = name.Trim().TrimEnd('.').ToLowerInvariant();
        return text.Length >= 3 ? Array.FindIndex(Months, m => m == text || (text.Length == 3 && m.StartsWith(text, StringComparison.Ordinal))) + 1 : 0;
    }

    /// <summary>A day of the month, "14" (or "14.0"); null otherwise.</summary>
    private static int? Day(string cell)
    {
        var text = cell.Trim();
        if (text.EndsWith(".0", StringComparison.Ordinal))
            text = text[..^2];
        return text.Length is > 0 and <= 2 && text.All(char.IsAsciiDigit) && int.Parse(text, CultureInfo.InvariantCulture) is >= 1 and <= 31 and var day ? day : null;
    }

    /// <summary>A cell that is a date with its year ("5/19/2019", "2019-05-19"); null for anything else.</summary>
    private static DateTime? Date(string cell)
    {
        var text = cell.Trim();
        if (!FullDate().IsMatch(text) || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;
        return date.Year is >= 2007 and <= 2100 ? date.Date : null;
    }

    /// <summary>The two teams a match's cell names, as it names them; null for a cell that is no match.</summary>
    private static (string First, string Second)? Teams(string cell)
    {
        var text = Spaces().Replace(cell, " ").Trim();
        // "0 MM vs. DBAGS 2": each side's wins around the names; "2-0 G over Z": the series' before them
        text = Counted().Match(text) is { Success: true } counted ? counted.Groups["rest"].Value : LeadingScore().Replace(text, "");
        var match = Over().Match(text);
        if (!match.Success)
            match = Versus().Match(text);
        if (!match.Success)
            match = Between().Match(text);
        if (!match.Success)
            return null;
        var (first, second) = (Team(match.Groups["a"].Value), Team(match.Groups["b"].Value));
        return first is null || second is null || string.Equals(first, second, StringComparison.OrdinalIgnoreCase) ? null : (first, second);
    }

    /// <summary>A side's name without the time, score or count after it; null when nothing that could be a team is left.</summary>
    private static string? Team(string side)
    {
        var name = TrailingNoise().Replace(side, "").Trim().Trim('*').Trim();
        return name.Length is 0 or > 50 || name.All(c => char.IsAsciiDigit(c) || c == ' ') || name.Equals("TBD", StringComparison.OrdinalIgnoreCase) ? null : name;
    }

    /// <summary>A time in the cell ("9:00", "8pm", "10PM EST", "09:00:00"), in the evening when it says neither am nor pm; a
    /// number with neither minutes nor am or pm (a score, a team's) is none.</summary>
    private static TimeSpan? Time(string cell)
    {
        foreach (Match match in TimePattern().Matches(cell))
        {
            var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            var minutes = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
            var meridiem = match.Groups["ap"].Value.ToLowerInvariant();
            if ((!match.Groups["m"].Success && meridiem.Length == 0) || minutes > 59)
                continue;
            if (hours is >= 13 and <= 23 && meridiem.Length == 0)
                return new TimeSpan(hours, minutes, 0);
            if (hours is >= 1 and <= 12)
                return new TimeSpan(meridiem.StartsWith('a') ? hours % 12 : hours % 12 + 12, minutes, 0);
        }
        return null;
    }

    /// <summary>"5/19/2019", "2019-05-19", "May 19, 2019", "Sunday, May 19, 2019".</summary>
    [GeneratedRegex(@"^(\d{1,2}/\d{1,2}/\d{2,4}|\d{4}-\d{2}-\d{2}|([A-Za-z]{3,9},?\s+)?[A-Za-z]{3,9}\.?\s+\d{1,2},?\s+\d{4})(\b.*)?$")]
    private static partial Regex FullDate();

    /// <summary>"2-0 " before the first team ("2-0 G over Z").</summary>
    [GeneratedRegex(@"^\d{1,2}\s*-\s*\d{1,2}\s+(?=\D)")]
    private static partial Regex LeadingScore();

    /// <summary>"July 31st", "Aug 1": a month's name and a day.</summary>
    [GeneratedRegex(@"^(?<month>[A-Za-z]{3,9})\.?\s+(?<day>\d{1,2})(st|nd|rd|th)?$")]
    private static partial Regex MonthAndDay();

    /// <summary>"0 MM vs. DBAGS 2": a count before the first team and after the second.</summary>
    [GeneratedRegex(@"^\d{1,2}\s+(?<rest>\D.*\s(?:vs\.?|v\.?|@)\s.*?)\s+\d{1,2}$", RegexOptions.IgnoreCase)]
    private static partial Regex Counted();

    [GeneratedRegex(@"^(?<a>.+?)\s+(?:over|tied|beat|def\.?|defeated)\s+(?<b>.+?)$", RegexOptions.IgnoreCase)]
    private static partial Regex Over();

    [GeneratedRegex(@"^(?<a>.+?)\s+(?:vs\.?|v\.?|@)\s+(?<b>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Versus();

    /// <summary>"ND 5-3 IMP": a score between the names.</summary>
    [GeneratedRegex(@"^(?<a>\D.*?)\s+\d{1,2}\s*-\s*\d{1,2}\s+(?<b>\D.*)$")]
    private static partial Regex Between();

    /// <summary>What may follow a side's name: times, a score, a count, a time zone.</summary>
    [GeneratedRegex(@"(\s+(\d{1,2}(:\d{2})?\s*([ap]\.?\s*m\.?)?|\d{1,2}\s*-\s*\d{1,2}|e[sd]?t\.?|eastern|\(\d+\s*-\s*\d+\)|\(\d{1,2}\)))+\s*\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingNoise();

    [GeneratedRegex(@"(?<![\d:])(?<h>\d{1,2})(?::(?<m>\d{2}))?(?::\d{2})?\s*(?<ap>[ap])?\.?\s*m?\.?(?![\d-])", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    /// <summary>"9:30pm: ", "*8pm: " before a match (SL20 Console's calendar).</summary>
    [GeneratedRegex(@"^\*?\d{1,2}(:\d{2})?\s*[ap]\.?\s*m\.?\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingTime();

    /// <summary>A cell that is only a time ("8 PM Est."), below a match's.</summary>
    [GeneratedRegex(@"^\d{1,2}(:\d{2})?\s*[ap]\.?\s*m\.?(\s*e[sd]?t\.?)?$", RegexOptions.IgnoreCase)]
    private static partial Regex TimeOnly();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
