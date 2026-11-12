using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using static GrifballWebApp.Infrastructure.Test.DatabaseConversionPartsTests;
using static GrifballWebApp.Infrastructure.Test.DatabaseConversionPartsTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The conversion's parts that need an EF model or a SQLite connection (in memory): reading values, the order of
/// tables, the plan.</summary>
[TestFixture]
public class DatabaseConversionModelTests
{
    // Each provider type read from SQLite's storage classes, as the other servers read theirs
    [Test]
    public void Sqlite_ReadsEachValue_AsTheOtherServersDo()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT '2026-10-10 12:00:00.12345', '2026-11-01 08:30:00-05:00', '0F8FAD5B-D9CB-469F-A165-70867728950E', 1, 7, 300, 70000,
                   9000000000, 1.5, 2.5, '0.125', '01:02:03', '19:30:00', '2026-11-01', 'text', x'0102', -1, 900000000, 66.667, 42
            """;
        using var reader = command.ExecuteReader();
        reader.Read();
        var columns = SqliteColumns.Instance;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SqliteColumns.ReadAs(reader, 0, typeof(DateTime)), Is.EqualTo(new DateTime(2026, 10, 10, 12, 0, 0).AddTicks(1_234_500)));
            Assert.That(SqliteColumns.ReadAs(reader, 1, typeof(DateTimeOffset)), Is.EqualTo(new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.FromHours(-5))));
            Assert.That(SqliteColumns.ReadAs(reader, 2, typeof(Guid)), Is.EqualTo(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")));
            Assert.That(SqliteColumns.ReadAs(reader, 3, typeof(bool)), Is.True);
            Assert.That(SqliteColumns.ReadAs(reader, 4, typeof(byte)), Is.EqualTo((byte)7));
            Assert.That(SqliteColumns.ReadAs(reader, 5, typeof(short)), Is.EqualTo((short)300));
            Assert.That(SqliteColumns.ReadAs(reader, 6, typeof(int)), Is.EqualTo(70000));
            Assert.That(SqliteColumns.ReadAs(reader, 7, typeof(long)), Is.EqualTo(9000000000L));
            Assert.That(SqliteColumns.ReadAs(reader, 8, typeof(double)), Is.EqualTo(1.5d));
            Assert.That(SqliteColumns.ReadAs(reader, 9, typeof(float)), Is.EqualTo(2.5f));
            Assert.That(SqliteColumns.ReadAs(reader, 10, typeof(decimal)), Is.EqualTo(0.125m));
            Assert.That(SqliteColumns.ReadAs(reader, 11, typeof(TimeSpan)), Is.EqualTo(new TimeSpan(1, 2, 3)));
            Assert.That(SqliteColumns.ReadAs(reader, 12, typeof(TimeOnly)), Is.EqualTo(new TimeOnly(19, 30)));
            Assert.That(SqliteColumns.ReadAs(reader, 13, typeof(DateOnly)), Is.EqualTo(new DateOnly(2026, 11, 1)));
            Assert.That(SqliteColumns.ReadAs(reader, 14, typeof(string)), Is.EqualTo("text"));
            Assert.That(SqliteColumns.ReadAs(reader, 15, typeof(byte[])), Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(SqliteColumns.ReadAs(reader, 16, typeof(ulong)), Is.EqualTo((decimal)ulong.MaxValue), "a ulong as its bits, read as the decimal the others give");
            Assert.That(SqliteColumns.ReadAs(reader, 14, typeof(Uri)), Is.EqualTo("text"), "a type it doesn't know: as the driver reads it");
            Assert.That(columns.Read(reader, 17, Matches, "Duration"), Is.EqualTo(TimeSpan.FromSeconds(90)), "SQLite's ticks, a TimeSpan again");
            Assert.That(columns.Read(reader, 18, Stats, "WinPercent"), Is.EqualTo(66.667m), "SQLite's double, a decimal again");
            Assert.That(columns.Read(reader, 19, new TableName("", "Nowhere"), "Number"), Is.EqualTo(42L), "a column not in the model: as the driver reads it");
        }
    }

    // SQLite has no schemas: its tables are named alone, and found in the model by their name.
    [Test]
    public void SqliteDialect_NamesTablesWithoutTheirSchema_AndFindsItInTheModel()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SqliteDialect.Instance.Table("", "Seasons"), Is.EqualTo(Seasons));
            Assert.That(SqliteDialect.Instance.Table("", "SeasonsHistory"), Is.EqualTo(new TableName("Event", "SeasonsHistory")));
            Assert.That(SqliteDialect.Instance.Table("", "NotInTheModel"), Is.EqualTo(new TableName("", "NotInTheModel")));
            Assert.That(SqliteDialect.Instance.Quote(Seasons), Is.EqualTo("\"Seasons\""));
            Assert.That(SqlDialect.For(DatabaseProvider.Sqlite), Is.SameAs(SqliteDialect.Instance));
        }
    }

    private static GrifballContext Offline(DatabaseProvider provider) => DatabaseConverter.NewContext(new DatabaseEndpoint(provider,
        provider == DatabaseProvider.Postgres ? "Host=offline;Database=x" : "Server=offline;Database=x"));

    [TestCase(DatabaseProvider.SqlServer)]
    [TestCase(DatabaseProvider.Postgres)]
    public void Order_PrincipalsFirst_DeferringTheCyclesNullableKeys(DatabaseProvider provider)
    {
        using var context = Offline(provider);

        var tables = ConversionTables.Order(context);
        var names = tables.Names.Select(n => n.Name).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names.IndexOf("Seasons"), Is.LessThan(names.IndexOf("Teams")));
            Assert.That(names.IndexOf("Teams"), Is.LessThan(names.IndexOf("TeamPlayers")));
            Assert.That(names.IndexOf("SeasonMatches"), Is.LessThan(names.IndexOf("MatchReschedules")));
            Assert.That(names.IndexOf("Users"), Is.LessThan(names.IndexOf("TeamPlayers")));
            // Teams and their captains are the one cycle: a match no longer points at its active reschedule request (#297)
            Assert.That(tables.DeferredForeignKeys, Has.Some.Contains("Teams.").And.None.Contains("SeasonMatches."));
            Assert.That(tables.DeferredForeignKeys, Has.None.Contains("MatchBracketInfo."), "a table's keys to itself go by row order");
        }
    }

    private sealed class Chicken { public int Id { get; set; } public int EggId { get; set; } public Egg Egg { get; set; } = null!; }
    private sealed class Egg { public int Id { get; set; } public int ChickenId { get; set; } public Chicken Chicken { get; set; } = null!; }

    private sealed class CycleContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseNpgsql("Host=offline;Database=x");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Chicken>().HasOne(c => c.Egg).WithMany().HasForeignKey(c => c.EggId);
            modelBuilder.Entity<Egg>().HasOne(e => e.Chicken).WithMany().HasForeignKey(e => e.ChickenId);
        }
    }

    [Test]
    public void Order_RefusesACycleWithNoNullableKey()
    {
        using var context = new CycleContext();
        var tables = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables;

        Assert.That(() => ConversionTables.Order(tables), Throws.TypeOf<ConversionException>().With.Message.Contains("no nullable key"));
    }

    [Test]
    public void Plan_NamesWhatDiffers_BetweenTheSourceAndTheTarget()
    {
        using var context = Offline(DatabaseProvider.Postgres);
        var tables = ConversionTables.Order(context);
        var target = new Dictionary<TableName, List<ColumnInfo>>
        {
            [new("Event", "Seasons")] = [Column("SeasonID", "int4"), Column("SeasonName", "text")],
            [new("Event", "OnlyTarget")] = [Column("ID", "int4")],
        };
        var source = new Dictionary<TableName, List<ColumnInfo>>
        {
            [new("Event", "Seasons")] = [Column("SeasonID", "int"), Column("Name", "nvarchar")],
            [new("Event", "OnlySource")] = [Column("ID", "int")],
        };

        var e = Assert.Throws<ConversionException>(() => tables.Plan(source, target));

        Assert.That(e!.Message, Does.Contain("Event.OnlySource is only in the source")
            .And.Contain("Event.OnlyTarget is only in the target")
            .And.Contain("Event.Seasons.Name is only in the source")
            .And.Contain("Event.Seasons.SeasonName is only in the target")
            .And.Contain("is in the model but not in the target"));
    }

    [Test]
    public void Plan_LeavesOutQuartzsJobStore_WhoseNamesDifferInCaseByServer()
    {
        using var context = Offline(DatabaseProvider.Postgres);
        var tables = ConversionTables.Order(context);
        var source = new Dictionary<TableName, List<ColumnInfo>> { [new("", "QRTZ_LOCKS")] = [Column("SCHED_NAME", "nvarchar")] };
        var target = new Dictionary<TableName, List<ColumnInfo>> { [new("", "qrtz_locks")] = [Column("sched_name", "text")] };

        // Only the model's tables are missing: nothing about Quartz's
        var e = Assert.Throws<ConversionException>(() => tables.Plan(source, target));
        Assert.That(e!.Message, Does.Not.Contain("qrtz").IgnoreCase);
    }
}
