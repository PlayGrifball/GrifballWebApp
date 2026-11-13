using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// SqliteHistory's trigger SQL and name helpers, on the real tables of a SQLite model (no connection ever opened):
/// no database, host or network.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SqliteHistoryTests
{
    private static GrifballContext Context() => new(new DbContextOptionsBuilder<GrifballContext>()
        .UseGrifballDatabase(DatabaseProvider.Sqlite, "Data Source=sqlite-history-test;Mode=Memory").Options);

    private static (ITable Table, ITable History) SeasonsTables(GrifballContext context)
    {
        var tables = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables;
        return (tables.Single(t => t.Name == "Seasons"), tables.Single(t => t.Name == "SeasonsHistory"));
    }

    [Test]
    public void UpdateTrigger_DeleteTrigger_NameTheTriggersAfterTheTable()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SqliteHistory.UpdateTrigger("Seasons"), Is.EqualTo("Seasons_History_Update"));
            Assert.That(SqliteHistory.DeleteTrigger("Seasons"), Is.EqualTo("Seasons_History_Delete"));
        }
    }

    [Test]
    public void HistoryOf_AHistoryTable_NamesTheTableItKeepsHistoryFor()
    {
        using var context = Context();
        var (_, history) = SeasonsTables(context);

        Assert.That(SqliteHistory.HistoryOf(history), Is.EqualTo("Seasons"));
    }

    [Test]
    public void HistoryOf_NotAHistoryTable_IsNull()
    {
        using var context = Context();
        var (table, _) = SeasonsTables(context);

        Assert.That(SqliteHistory.HistoryOf(table), Is.Null);
    }

    [Test]
    public void CreateTriggers_BuildsAnUpdateAndADeleteTrigger_CopyingEveryColumnButPeriodEnd()
    {
        using var context = Context();
        var (table, history) = SeasonsTables(context);

        var sql = SqliteHistory.CreateTriggers(table, history);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sql, Does.Contain("CREATE TRIGGER \"Seasons_History_Update\" AFTER UPDATE ON \"Seasons\" FOR EACH ROW BEGIN"));
            Assert.That(sql, Does.Contain("CREATE TRIGGER \"Seasons_History_Delete\" AFTER DELETE ON \"Seasons\" FOR EACH ROW BEGIN"));
            Assert.That(sql, Does.Contain("INSERT INTO \"SeasonsHistory\""));
            Assert.That(sql, Does.Contain("\"PeriodStart\" = " + SqliteHistory.Now));
            Assert.That(sql, Does.Contain("OLD.\"SeasonName\""), "A column both tables have is copied from OLD");
            Assert.That(sql, Does.Not.Contain("OLD.\"PeriodEnd\""), "PeriodEnd isn't copied: the history row gets the time it ended instead");
        }
    }

    [Test]
    public void CreateTriggers_EndsWith_END()
    {
        using var context = Context();
        var (table, history) = SeasonsTables(context);

        var sql = SqliteHistory.CreateTriggers(table, history);

        Assert.That(sql.TrimEnd(), Does.EndWith("END;"));
    }

    [TestCase("Seasons", "DROP TRIGGER IF EXISTS \"Seasons_History_Update\";\nDROP TRIGGER IF EXISTS \"Seasons_History_Delete\";")]
    public void DropTriggers_DropsBothTriggersIfTheyExist(string table, string expected)
    {
        Assert.That(SqliteHistory.DropTriggers(table), Is.EqualTo(expected));
    }
}
