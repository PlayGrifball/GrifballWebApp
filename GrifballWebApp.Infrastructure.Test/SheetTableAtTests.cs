using GrifballWebApp.Infrastructure.Excel;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>SheetTable.At: a cell by its reference, the sheet's empty rows counted though they aren't kept.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SheetTableAtTests
{
    [Test]
    public void At_AnXlsxWithEmptyRows_ReadsTheSheetsOwnRowAndColumn()
    {
        // Rows 1, 2 and 4 are empty: C3 and B5 are the 1st and 2nd rows kept
        var xlsx = Build(workbook =>
        {
            var sheet = workbook.Worksheets.Add("Bracket");
            sheet.Cell("C3").Value = "Alpha";
            sheet.Cell("B5").Value = "vs (2-1)";
            sheet.Cell("AA5").Value = "far";
        });

        var table = SheetWorkbook.Read(xlsx)!.Sheet("Bracket")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(table.Rows, Has.Count.EqualTo(2), "Empty rows aren't kept");
            Assert.That(table.At("C3"), Is.EqualTo("Alpha"));
            Assert.That(table.At("b5"), Is.EqualTo("vs (2-1)"), "Any case");
            Assert.That(table.At("AA5"), Is.EqualTo("far"), "Two-letter columns");
            Assert.That(table.At("C4"), Is.Empty, "An empty row");
            Assert.That(table.At("D3"), Is.Empty, "An empty cell of a kept row");
        }
    }

    [TestCase("")]
    [TestCase("3")]
    [TestCase("C")]
    [TestCase("C0")]
    [TestCase("C-1")]
    public void At_AReferenceThatNamesNoCell_IsEmpty(string reference)
    {
        var table = new SheetTable { Name = "S", Rows = [["a", "b", "c"]] };

        Assert.That(table.At(reference), Is.Empty);
    }

    [Test]
    public void At_RowsWithoutNumbers_AreTheSheetsFromItsFirstRow()
    {
        var table = new SheetTable { Name = "S", Rows = [["a"], ["", "b"]] };

        Assert.That((table.At("A1"), table.At("B2"), table.At("A3")), Is.EqualTo(("a", "b", "")));
    }

    [Test]
    public void Reference_AnXlsxWithEmptyRows_NamesTheSheetsOwnRowAndColumn()
    {
        // Rows 1, 2 and 4 are empty: the 1st and 2nd rows kept are the sheet's 3 and 5
        var xlsx = Build(workbook =>
        {
            var sheet = workbook.Worksheets.Add("Schedule");
            sheet.Cell("C3").Value = "Alpha vs. Bravo";
            sheet.Cell("A5").Value = "Week 2";
            sheet.Cell("AA5").Value = "far";
        });

        var table = SheetWorkbook.Read(xlsx)!.Sheet("Schedule")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(table.Reference(0, 2), Is.EqualTo("C3"));
            Assert.That(table.Reference(1, 26), Is.EqualTo("AA5"));
            Assert.That(table.RowRange(1), Is.EqualTo("A5:AA5"), "From its first cell to its last with text");
            Assert.That(table.Range(0, 2, 0, 2), Is.EqualTo("C3"), "One cell is its reference");
            Assert.That(table.Range(0, 0, 1, 3), Is.EqualTo("A3:D5"));
            Assert.That(table.RowText(0), Is.EqualTo(" |  | Alpha vs. Bravo"));
        }
    }

    [Test]
    public void Reference_RowsWithoutNumbers_CountsThemFromTheFirstRow()
    {
        var table = new SheetTable { Name = "S", Rows = [["a"], ["", "b"]] };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(table.Reference(1, 1), Is.EqualTo("B2"));
            Assert.That(table.RowNumber(5), Is.EqualTo(6), "A row past the last is numbered on");
            Assert.That(table.RowText(2), Is.Empty, "A row past the last has no text");
        }
    }

    [TestCase(0, "A")]
    [TestCase(25, "Z")]
    [TestCase(26, "AA")]
    [TestCase(51, "AZ")]
    [TestCase(52, "BA")]
    [TestCase(701, "ZZ")]
    [TestCase(702, "AAA")]
    public void ColumnName_IsTheColumnsLetters(int column, string name) => Assert.That(SheetTable.ColumnName(column), Is.EqualTo(name));

    [Test]
    public void ColumnName_ANegativeColumn_Throws() => Assert.Throws<ArgumentOutOfRangeException>(() => SheetTable.ColumnName(-1));
}
