using GrifballWebApp.Historical.Import;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Test;

/// <summary>ArchiveOrigin's ways of naming a tab's cells: a row, a cell, a block of rows, a row by itself, several rows' lines.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ArchiveOriginTests
{
    // The sheet's rows 3, 5 and 6 (rows 1, 2 and 4 are empty)
    private static SheetTable Sheet() => new() { Name = "Games", Rows = [["Ace", "5"], ["Bee", "3", "x"], ["Cee"]], RowNumbers = [3, 5, 6] };

    [Test]
    public void RowCellAndRows_NameTheSheetsOwnCells()
    {
        var sheet = Sheet();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ArchiveOrigin.Row("g", sheet, 1), Is.EqualTo(new ArchiveOrigin("g", "Games", "A5:C5", "Bee | 3 | x")));
            Assert.That(ArchiveOrigin.Cell("g", sheet, 0, 1), Is.EqualTo(new ArchiveOrigin("g", "Games", "B3", "5")));
            Assert.That(ArchiveOrigin.Rows("g", sheet, 0, 2), Is.EqualTo(new ArchiveOrigin("g", "Games", "A3:C6", "Ace | 5\nBee | 3 | x\nCee")), "as wide as its widest row");
            Assert.That(ArchiveOrigin.Rows("g", sheet, 2, 2), Is.EqualTo(ArchiveOrigin.Row("g", sheet, 2)), "one row is that row");
        }
    }

    [Test]
    public void RowOfAndRowsOf_FindRowsByThemselves()
    {
        var sheet = Sheet();
        var rowOf = ArchiveOrigin.RowOf("g", sheet);
        var rowsOf = ArchiveOrigin.RowsOf("g", sheet);
        string[] stranger = ["Dee", "1"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rowOf(sheet.Rows[2]).Cells, Is.EqualTo("A6"));
            Assert.That(rowOf(stranger), Is.EqualTo(new ArchiveOrigin("g", "Games", null, "Dee | 1")), "a row of no tab: its text only");
            Assert.That(rowsOf([sheet.Rows[2], sheet.Rows[0]])!.Cells, Is.EqualTo("A3:B6"), "from the first to the last, whatever their order");
            Assert.That(rowsOf([stranger]), Is.Null);
        }
    }

    [Test]
    public void Of_ALineOfSeveralRows_HasTheFirstsCellsAndEveryText()
    {
        var a = new ArchiveOrigin("g", "Games", "A3", "one");
        var b = new ArchiveOrigin("g", "Games", "A5", "two");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ArchiveOrigin.Of([]), Is.Null);
            Assert.That(ArchiveOrigin.Of([null, a]), Is.EqualTo(a));
            Assert.That(ArchiveOrigin.Of([a, b]), Is.EqualTo(a with { Value = "one\ntwo" }));
        }
    }
}
