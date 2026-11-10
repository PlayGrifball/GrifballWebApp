using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>SheetWorkbook.Number: a cell's number as the sheet shows it, by each number format the archive uses.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SheetWorkbookNumberTests
{
    // 45000.5 is 2023-03-15 (a Wednesday) at noon; 45000.25 the same day at 6 am
    [TestCase(45000.5, "General", 14, "3/15/2023")]
    [TestCase(45000.5, "m-d-yyyy", 0, "3-15-2023")]
    [TestCase(45000.5, "m/d", 0, "3/15")]
    [TestCase(45000.5, "m-d", 0, "3-15")]
    [TestCase(45000.5, "d-m", 0, "15-3")]
    [TestCase(45000.5, "mmmm d", 0, "March 15")]
    [TestCase(45000.5, "dddd mmm d", 0, "Wednesday Mar 15")]
    [TestCase(45000.5, "m/d h:mm am/pm", 0, "3/15 12:00 PM")]
    [TestCase(45000.5, "m/d/yy h:mm am/pm", 0, "3/15/23 12:00 PM")]
    [TestCase(45000.5, "m/d/yyyy h:mm", 0, "3/15/2023 12:00:00")]
    [TestCase(45000.5, "General", 22, "3/15/2023 12:00:00")]
    [TestCase(45000.5, "General", 0, "45000.5")]
    [TestCase(45000.25, "h:mm am/pm", 0, "6:00 AM")]
    [TestCase(45000.25, "General", 19, "6:00:00 AM")]
    public void Dates_and_times_of_day(double number, string format, int formatId, string expected)
    {
        Assert.That(SheetWorkbook.Number(number, format, formatId), Is.EqualTo(expected));
    }

    [TestCase(0.5, "[h]:mm:ss", 0, "12:00:00")]
    [TestCase(-0.5, "[h]:mm:ss", 0, "-12:00:00")]
    [TestCase(0.0625, "hh:mm:ss", 0, "01:30:00")]
    [TestCase(0.0625, "General", 45, "90:00")]
    [TestCase(25 / 86400d, "\":\"ss", 0, ":25")]
    [TestCase(90 / 86400d, "\":\"ss", 0, "1:30")]
    public void Durations(double number, string format, int formatId, string expected)
    {
        Assert.That(SheetWorkbook.Number(number, format, formatId), Is.EqualTo(expected));
    }

    [TestCase(123456, "000000000", "000123456")]
    [TestCase(-5, "000000000", "-5")]
    [TestCase(12.5, "000000000", "12.5")]
    [TestCase(12.300000000000001, null, "12.3")]
    public void Plain_numbers(double number, string? format, string expected)
    {
        Assert.That(SheetWorkbook.Number(number, format, 0), Is.EqualTo(expected));
    }
}
