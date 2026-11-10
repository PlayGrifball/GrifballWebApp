using ExcelDataReader;
using System.Globalization;
using System.Text;

namespace GrifballWebApp.Infrastructure.Excel;

/// <summary>A sheet as text: its non-empty rows, each without trailing empty cells (row indexes skip empty rows, as the
/// archive's tab configs count them).</summary>
public sealed class SheetTable
{
    public required string Name { get; init; }
    public bool Hidden { get; init; }
    public List<string[]> Rows { get; init; } = [];

    public string Cell(int row, int column) => row < Rows.Count && column < Rows[row].Length ? Rows[row][column] : "";
}

/// <summary>
/// A downloaded workbook read into text cells. Numbers are written the way the sheet shows them (dates, durations, the
/// ":25" of an average life), so the parsers read every source alike. An xlsx is read with ExcelDataReader (MIT): it
/// streams and returns each formula's cached value, never recalculating; a CSV (a gviz export of one tab) is one sheet.
/// </summary>
public sealed class SheetWorkbook
{
    // ExcelDataReader's configuration defaults to code page 1252, which .NET only has once the code pages provider is registered
    static SheetWorkbook() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public List<SheetTable> Sheets { get; init; } = [];

    public SheetTable? Sheet(string name) => Sheets.FirstOrDefault(s => s.Name == name)
        ?? Sheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>An xlsx (a zip) or a CSV (anything else that isn't HTML); null for content that is neither (a web page).
    /// <paramref name="sheets"/>: the xlsx's tabs to read (by name); the others are skipped, their rows never read. Null: every tab.</summary>
    public static SheetWorkbook? Read(byte[] content, string? csvSheetName = null, Func<string, bool>? sheets = null)
    {
        if (IsZip(content))
            return ReadXlsx(content, sheets);
        var text = Encoding.UTF8.GetString(content).TrimStart('﻿');
        if (text.TrimStart().StartsWith('<'))
            return null;
        return new SheetWorkbook { Sheets = [new SheetTable { Name = csvSheetName ?? "Sheet1", Rows = Csv(text) }] };
    }

    public static bool IsZip(byte[] content) => content.Length > 4 && content[0] == 0x50 && content[1] == 0x4b && content[2] == 0x03 && content[3] == 0x04;

    private static SheetWorkbook ReadXlsx(byte[] content, Func<string, bool>? wanted)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = ExcelReaderFactory.CreateOpenXmlReader(stream);
        var sheets = new List<SheetTable>();
        do
        {
            if (wanted is not null && !wanted(reader.Name))
                continue;
            var rows = new List<string[]>();
            while (reader.Read())
            {
                var cells = new string[reader.FieldCount];
                for (var i = 0; i < cells.Length; i++)
                    cells[i] = Text(reader, i);
                AddRow(rows, cells);
            }
            sheets.Add(new SheetTable
            {
                Name = reader.Name,
                Hidden = !string.Equals(reader.VisibleState, "visible", StringComparison.OrdinalIgnoreCase),
                Rows = TimestampColumn(rows),
            });
        } while (reader.NextResult());
        return new SheetWorkbook { Sheets = sheets };
    }

    /// <summary>Keeps a row with any text, without its trailing empty cells.</summary>
    private static void AddRow(List<string[]> rows, IReadOnlyList<string> cells)
    {
        var length = cells.Count;
        while (length > 0 && cells[length - 1].Length == 0)
            length--;
        if (length > 0)
            rows.Add(cells.Take(length).ToArray());
    }

    private static string Text(IExcelDataReader reader, int i)
    {
        var value = reader.GetValue(i);
        switch (value)
        {
            case null:
                return reader.GetCellError(i) switch
                {
                    CellError.NULL => "#NULL!",
                    CellError.DIV0 => "#DIV/0!",
                    CellError.VALUE => "#VALUE!",
                    CellError.REF => "#REF!",
                    CellError.NAME => "#NAME?",
                    CellError.NUM => "#NUM!",
                    CellError.NA => "#N/A",
                    _ => "",
                };
            case string text:
                return text.Replace("_x000D_", "").Replace("\r", "");
            case bool flag:
                return flag ? "TRUE" : "FALSE";
            case DateTime date:
                // ExcelDataReader adds a day below serial 61 (Excel counts a 29 February 1900 that OLE dates don't): a
                // duration such as ":25" comes back as 31 December 1899, so the day is taken off again
                var serial = date.ToOADate();
                return Number(serial < 61 ? serial - 1 : serial, reader.GetNumberFormatString(i), reader.GetNumberFormatIndex(i));
            case TimeSpan span:
                return Number(span.TotalDays, reader.GetNumberFormatString(i), reader.GetNumberFormatIndex(i));
            case IConvertible number:
                return Number(number.ToDouble(CultureInfo.InvariantCulture), reader.GetNumberFormatString(i), reader.GetNumberFormatIndex(i));
            default:
                return value.ToString() ?? "";
        }
    }

    private static readonly string[] MonthsShort = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private static readonly string[] MonthsFull = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    private static readonly string[] DaysFull = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private readonly record struct SerialParts(int Year, int Month, int Day, int DayOfWeek, int Hours24, int Hours12, int Minutes, int Seconds, string AmPm);

    private static SerialParts Parts(double serial)
    {
        var totalSeconds = (long)Math.Round(serial * 86400, MidpointRounding.AwayFromZero);
        var wholeDays = (long)Math.Floor(totalSeconds / 86400.0);
        var daySeconds = (int)(((totalSeconds % 86400) + 86400) % 86400);
        // Excel's 1900 epoch: serial 25569 is 1970-01-01
        var date = DateTime.UnixEpoch.AddDays(wholeDays - 25569);
        var hours = daySeconds / 3600;
        return new SerialParts(date.Year, date.Month, date.Day, (int)date.DayOfWeek, hours, hours % 12 == 0 ? 12 : hours % 12,
            daySeconds % 3600 / 60, daySeconds % 60, hours >= 12 ? "PM" : "AM");
    }

    private static string Two(int value) => value.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>A number as the sheet shows it, by its number format (only the formats the archive uses).</summary>
    public static string Number(double number, string? format, int formatId)
    {
        var fmt = string.IsNullOrEmpty(format) ? "General" : format;
        var raw = Raw(number);
        if (double.IsNaN(number) || double.IsInfinity(number))
            return raw;

        return Duration(number, fmt, formatId)
            ?? TimeOfDay(number, fmt, formatId)
            // Dates
            ?? (number > 1000 && number < 80000 ? Date(Parts(number), fmt, formatId) : null)
            ?? (fmt == "000000000" && number >= 0 && double.IsInteger(number) ? ((long)number).ToString("000000000", CultureInfo.InvariantCulture) : null)
            ?? raw;
    }

    /// <summary>A duration (hours, minutes and seconds), by its format; null for another format.</summary>
    private static string? Duration(double number, string fmt, int formatId)
    {
        var total = (long)Math.Round(Math.Abs(number) * 86400, MidpointRounding.AwayFromZero);
        var sign = number < 0 ? "-" : "";
        if (formatId == 46 || fmt is "[h]:mm:ss" or "h\":\"mm\":\"ss" or "hh\":\"mm\":\"ss" || (fmt == "hh:mm:ss" && number < 100))
        {
            var h = total / 3600;
            var hours = fmt.StartsWith("hh") ? h.ToString("00", CultureInfo.InvariantCulture) : h.ToString(CultureInfo.InvariantCulture);
            return $"{sign}{hours}:{Two((int)(total % 3600 / 60))}:{Two((int)(total % 60))}";
        }
        if (formatId == 45 || fmt is "mm:ss" or "mm\":\"ss")
            return $"{sign}{(total / 60).ToString("00", CultureInfo.InvariantCulture)}:{Two((int)(total % 60))}";
        // An average life in seconds, ":25"
        if (fmt == "\":\"ss")
            return total >= 60 ? $"{total / 60}:{Two((int)(total % 60))}" : $":{Two((int)total)}";
        return null;
    }

    /// <summary>A time of day, by its format; null for another format.</summary>
    private static string? TimeOfDay(double number, string fmt, int formatId)
    {
        if (fmt == "h:mm am/pm" || formatId == 18)
        {
            var p = Parts(number);
            return $"{p.Hours12}:{Two(p.Minutes)} {p.AmPm}";
        }
        if (fmt == "h:mm:ss am/pm" || formatId == 19)
        {
            var p = Parts(number);
            return $"{p.Hours12}:{Two(p.Minutes)}:{Two(p.Seconds)} {p.AmPm}";
        }
        return null;
    }

    /// <summary>A date (and time), by its format; null for another format.</summary>
    private static string? Date(SerialParts p, string fmt, int formatId)
    {
        if (fmt is "m/d/yyyy" or "M/d/yyyy" || formatId == 14)
            return $"{p.Month}/{p.Day}/{p.Year}";
        return fmt switch
        {
            "m-d-yyyy" => $"{p.Month}-{p.Day}-{p.Year}",
            "m/d" => $"{p.Month}/{p.Day}",
            "m-d" => $"{p.Month}-{p.Day}",
            "d-m" => $"{p.Day}-{p.Month}",
            "mmmm d" => $"{MonthsFull[p.Month - 1]} {p.Day}",
            "dddd mmm d" => $"{DaysFull[p.DayOfWeek]} {MonthsShort[p.Month - 1]} {p.Day}",
            "m/d h:mm am/pm" => $"{p.Month}/{p.Day} {p.Hours12}:{Two(p.Minutes)} {p.AmPm}",
            "m/d/yy h:mm am/pm" => $"{p.Month}/{p.Day}/{p.Year % 100:00} {p.Hours12}:{Two(p.Minutes)} {p.AmPm}",
            "m/d/yyyy h:mm:ss" or "m/d/yyyy h:mm" => $"{p.Month}/{p.Day}/{p.Year} {p.Hours24}:{Two(p.Minutes)}:{Two(p.Seconds)}",
            _ when formatId == 22 => $"{p.Month}/{p.Day}/{p.Year} {p.Hours24}:{Two(p.Minutes)}:{Two(p.Seconds)}",
            _ => null,
        };
    }

    /// <summary>The number as the sheet stores it: integers without ".0" (big ones, Discord IDs, without an exponent) and
    /// floating-point noise (12.300000000000001) rounded to 12 significant digits.</summary>
    private static string Raw(double number)
    {
        if (number == 0)
            return "0";
        if (double.IsInteger(number) && Math.Abs(number) < 1e21)
            return number.ToString("F0", CultureInfo.InvariantCulture);
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        if (dot >= 0 && !text.Contains('E') && text.Length - dot - 1 >= 9)
            return double.Parse(number.ToString("G12", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
        return text;
    }

    /// <summary>A sheet headed "Timestamp" (the Late League match forms) whose timestamps are unformatted serials gets
    /// them written as dates.</summary>
    private static List<string[]> TimestampColumn(List<string[]> rows)
    {
        if (rows.Count < 2 || rows[0].Length == 0 || rows[0][0] != "Timestamp")
            return rows;
        for (var r = 1; r < rows.Count; r++)
        {
            var cell = rows[r].Length > 0 ? rows[r][0] : "";
            if (cell.Length > 6 && cell[5] == '.' && cell[..5].All(char.IsAsciiDigit)
                && double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 40000 and < 50000)
            {
                var p = Parts(serial);
                rows[r][0] = $"{p.Month}/{p.Day}/{p.Year} {p.Hours24}:{Two(p.Minutes)}:{Two(p.Seconds)}";
            }
        }
        return rows;
    }

    /// <summary>RFC 4180 CSV: quoted fields with doubled quotes and line breaks.</summary>
    public static List<string[]> Csv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
                quoted = InQuotes(text, ref i, cell);
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(cell.ToString());
                cell.Clear();
                AddRow(rows, row);
                row = [];
            }
            else
                cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            AddRow(rows, row);
        }
        return rows;
    }

    /// <summary>A character of a quoted field (a doubled quote taken as one); false at the quote that ends it.</summary>
    private static bool InQuotes(string text, ref int i, StringBuilder cell)
    {
        if (text[i] != '"')
        {
            cell.Append(text[i]);
            return true;
        }
        if (i + 1 < text.Length && text[i + 1] == '"')
        {
            cell.Append('"');
            i++;
            return true;
        }
        return false;
    }
}
