using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Test;

/// <summary>The conversion's parts that need no database: values, the order of tables and rows, the plan.</summary>
[TestFixture]
public class DatabaseConversionUnitTests
{
    private static ColumnInfo Column(string name, string type) => new(name, type, Generated: false, Identity: false);

    private static readonly TableName Seasons = new("Event", "Seasons");
    private static readonly TableName Users = new("Auth", "Users");
    private static readonly TableName Matches = new("Infinite", "Matches");
    private static readonly TableName Stats = new("Event", "PlayerSeasonStats");

    // Each value as EF stores it on SQLite, from whichever server read it (SqliteColumns).
    [Test]
    public void Sqlite_WritesEachValue_AsEFStoresIt()
    {
        var instant = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_500);
        var guid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        var columns = SqliteColumns.Instance;

        Assert.Multiple(() =>
        {
            Assert.That(columns.Write(null, Seasons, "SignupsOpen"), Is.Null);
            Assert.That(columns.Write(instant, Seasons, "SignupsOpen"), Is.EqualTo("2026-10-10 12:00:00.12345"));
            Assert.That(columns.Write(new DateTimeOffset(instant), Seasons, "SignupsOpen"), Is.EqualTo("2026-10-10 12:00:00.12345"));
            Assert.That(columns.Write(instant, Users, "LockoutEnd"), Is.EqualTo("2026-10-10 12:00:00.12345+00:00"), "Postgres's timestamptz, as a UTC DateTime");
            Assert.That(columns.Write(new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.FromHours(-5)), Users, "LockoutEnd"), Is.EqualTo("2026-11-01 08:30:00-05:00"));
            Assert.That(columns.Write(guid, Matches, "MatchID"), Is.EqualTo("0F8FAD5B-D9CB-469F-A165-70867728950E"));
            Assert.That(columns.Write(true, Users, "EmailConfirmed"), Is.EqualTo(1L));
            Assert.That(columns.Write(TimeSpan.FromSeconds(90), Matches, "Duration"), Is.EqualTo(TimeSpan.FromSeconds(90).Ticks));
            Assert.That(columns.Write(66.667m, Stats, "WinPercent"), Is.EqualTo(66.667d));
            Assert.That(columns.Write(new TimeOnly(19, 30), new TableName("Event", "AvailabilityOptions"), "Time"), Is.EqualTo("19:30:00"));
            Assert.That(columns.Write(new TimeSpan(19, 30, 15), new TableName("Event", "AvailabilityOptions"), "Time"), Is.EqualTo("19:30:15"), "SQL Server's time, as a TimeSpan");
            Assert.That(columns.Write(new DateOnly(2026, 11, 1), new TableName("", "Nowhere"), "Date"), Is.EqualTo("2026-11-01"));
            Assert.That(columns.Write("text", new TableName("", "Nowhere"), "Text"), Is.EqualTo("text"));
        });
    }

    // SQLite has no schemas: its tables are named alone, and found in the model by their name.
    [Test]
    public void SqliteDialect_NamesTablesWithoutTheirSchema_AndFindsItInTheModel()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SqliteDialect.Instance.Table("", "Seasons"), Is.EqualTo(Seasons));
            Assert.That(SqliteDialect.Instance.Table("", "SeasonsHistory"), Is.EqualTo(new TableName("Event", "SeasonsHistory")));
            Assert.That(SqliteDialect.Instance.Table("", "NotInTheModel"), Is.EqualTo(new TableName("", "NotInTheModel")));
            Assert.That(SqliteDialect.Instance.Quote(Seasons), Is.EqualTo("\"Seasons\""));
            Assert.That(SqlDialect.For(DatabaseProvider.Sqlite), Is.SameAs(SqliteDialect.Instance));
        });
    }

    [Test]
    public void Fingerprint_IsTheSame_ForTheSameValuesAsEitherServerGivesThem()
    {
        var instant = new DateTime(2026, 10, 10, 12, 0, 0).AddTicks(1_234_567);
        object?[] fromSqlServer = [instant, new DateTimeOffset(instant.AddHours(-5), TimeSpan.FromHours(-5)), TimeSpan.FromTicks(10_000_007), 5m, DateTime.MaxValue, DateTimeOffset.MaxValue];
        object?[] fromPostgres = [DateTime.SpecifyKind(instant.AddTicks(-7), DateTimeKind.Utc), DateTime.SpecifyKind(instant.AddTicks(-7), DateTimeKind.Utc), TimeSpan.FromTicks(10_000_000), 5.0m, DateTime.MaxValue, DateTime.MaxValue];

        Assert.That(TableFingerprint.Of([fromSqlServer]), Is.EqualTo(TableFingerprint.Of([fromPostgres])));
    }

    [Test]
    public void Fingerprint_TellsRowsApart_InAnyOrder()
    {
        object?[] a = ["a", 1, null];
        object?[] b = ["b", 2, true];

        Assert.Multiple(() =>
        {
            Assert.That(TableFingerprint.Of([a, b]), Is.EqualTo(TableFingerprint.Of([b, a])));
            Assert.That(TableFingerprint.Of([a, b]), Is.Not.EqualTo(TableFingerprint.Of([a, a])));
            Assert.That(TableFingerprint.Of([["A"]]), Is.Not.EqualTo(TableFingerprint.Of([["a"]])), "case counts");
            Assert.That(TableFingerprint.Of([["a", "b"]]), Is.Not.EqualTo(TableFingerprint.Of([["a\u001eb"]])));
        });
    }

    [TestCase(null, "null")]
    [TestCase(true, "true")]
    [TestCase(false, "false")]
    [TestCase((short)3, "n3")]
    [TestCase(1.5f, "r1.5")]
    [TestCase(2.5d, "r2.5")]
    [TestCase(ulong.MaxValue, "n18446744073709551615")]
    public void Canonical_Values(object? value, string expected)
    {
        Assert.That(TableFingerprint.Canonical(value), Is.EqualTo(expected));
    }

    [Test]
    public void Canonical_OtherTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TableFingerprint.Canonical(new byte[] { 0, 255 }), Is.EqualTo("x00FF"));
            Assert.That(TableFingerprint.Canonical(Guid.Empty), Is.EqualTo("g00000000-0000-0000-0000-000000000000"));
            Assert.That(TableFingerprint.Canonical(DateTime.MinValue), Is.EqualTo("-infinity"));
            Assert.That(TableFingerprint.Canonical(DateTimeOffset.MinValue), Is.EqualTo("-infinity"));
            Assert.That(TableFingerprint.Canonical(new TimeOnly(1, 2, 3)), Is.EqualTo(TableFingerprint.Canonical(new TimeSpan(1, 2, 3))));
            Assert.That(() => TableFingerprint.Canonical(new Uri("https://example.com")), Throws.TypeOf<ConversionException>());
        });
    }

    [Test]
    public void ToPostgres_MakesInstantsUtc_ToTheMicrosecond_AndKeepsTheEndsOfTime()
    {
        var instant = new DateTime(2026, 10, 10, 12, 0, 0).AddTicks(1_234_567);
        var column = Column("At", "timestamptz");

        var plain = (DateTime)PostgresTargetWriter.ToPostgres(instant, column)!;
        var offset = (DateTime)PostgresTargetWriter.ToPostgres(new DateTimeOffset(instant.AddHours(2), TimeSpan.FromHours(2)), column)!;
        Assert.Multiple(() =>
        {
            Assert.That(plain, Is.EqualTo(instant.AddTicks(-7)));
            Assert.That(plain.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(offset, Is.EqualTo(instant.AddTicks(-7)));
            Assert.That(PostgresTargetWriter.ToPostgres(DateTime.MaxValue, column), Is.EqualTo(DateTime.MaxValue));
            Assert.That(PostgresTargetWriter.ToPostgres(DateTime.MinValue, column), Is.EqualTo(DateTime.MinValue));
            Assert.That(PostgresTargetWriter.ToPostgres(DateTimeOffset.MaxValue, column), Is.EqualTo(DateTime.MaxValue));
            Assert.That(PostgresTargetWriter.ToPostgres(DateTimeOffset.MinValue, column), Is.EqualTo(DateTime.MinValue));
            Assert.That(PostgresTargetWriter.ToPostgres(TimeSpan.FromTicks(15), Column("D", "interval")), Is.EqualTo(TimeSpan.FromTicks(10)));
            Assert.That(PostgresTargetWriter.ToPostgres("text", Column("T", "text")), Is.EqualTo("text"));
        });
    }

    [Test]
    public void ToSqlServer_TakesPostgresInstantsAndTheEndsOfTime()
    {
        var instant = DateTime.SpecifyKind(new DateTime(2026, 10, 10, 12, 0, 0), DateTimeKind.Utc);
        var offset = Column("LockoutEnd", "datetimeoffset");
        Assert.Multiple(() =>
        {
            Assert.That(SqlServerTargetWriter.ToSqlServer(instant, offset), Is.EqualTo(new DateTimeOffset(instant)));
            Assert.That(SqlServerTargetWriter.ToSqlServer(DateTime.MaxValue, offset), Is.EqualTo(DateTimeOffset.MaxValue));
            Assert.That(SqlServerTargetWriter.ToSqlServer(DateTime.MinValue, offset), Is.EqualTo(DateTimeOffset.MinValue));
            Assert.That(SqlServerTargetWriter.ToSqlServer(new DateTimeOffset(instant), Column("At", "datetime2")), Is.EqualTo(instant));
            Assert.That(((DateTime)SqlServerTargetWriter.ToSqlServer(instant, Column("At", "datetime2"))!).Kind, Is.EqualTo(DateTimeKind.Unspecified));
            Assert.That(SqlServerTargetWriter.ToSqlServer(new TimeOnly(19, 30), Column("Time", "time")), Is.EqualTo(new TimeSpan(19, 30, 0)));
            Assert.That(SqlServerTargetWriter.ToSqlServer(5, Column("N", "int")), Is.EqualTo(5));
        });
    }

    private static TablePlan Bracket() => new(new TableName("Event", "MatchBracketInfo"),
        [Column("ID", "int"), Column("PreviousID", "int"), Column("CaptainID", "int")],
        key: [0], selfReferences: [([1], [0])], deferred: [2]);

    [Test]
    public void OrderForInsert_PutsTheRowsARowRefersToBeforeIt()
    {
        List<object?[]> rows = [[1, 3, null], [2, null, null], [3, 2, null]];

        var ordered = Bracket().OrderForInsert(rows).Select(r => (int)r[0]!);

        Assert.That(ordered, Is.EqualTo(new[] { 2, 3, 1 }));
    }

    [Test]
    public void OrderForInsert_RefusesRowsInACycle()
    {
        List<object?[]> rows = [[1, 2, null], [2, 1, null]];

        Assert.That(() => Bracket().OrderForInsert(rows), Throws.TypeOf<ConversionException>().With.Message.Contains("cycle"));
    }

    [Test]
    public void Deferred_AreWrittenNull_AndSetForTheRowsThatHaveThem()
    {
        var plan = Bracket();
        object?[] withCaptain = [1, null, 7];
        object?[] without = [2, null, null];

        Assert.Multiple(() =>
        {
            Assert.That(plan.WithoutDeferred(withCaptain), Is.EqualTo(new object?[] { 1, null, null }));
            Assert.That(withCaptain[2], Is.EqualTo(7), "the row read is left as it was");
            Assert.That(plan.WithDeferred([withCaptain, without]), Is.EqualTo(new[] { withCaptain }));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(names.IndexOf("Seasons"), Is.LessThan(names.IndexOf("Teams")));
            Assert.That(names.IndexOf("Teams"), Is.LessThan(names.IndexOf("TeamPlayers")));
            Assert.That(names.IndexOf("SeasonMatches"), Is.LessThan(names.IndexOf("MatchReschedules")));
            Assert.That(names.IndexOf("Users"), Is.LessThan(names.IndexOf("TeamPlayers")));
            Assert.That(tables.DeferredForeignKeys, Has.Some.Contains("Teams.").And.Some.Contains("SeasonMatches."));
            Assert.That(tables.DeferredForeignKeys, Has.None.Contains("MatchBracketInfo."), "a table's keys to itself go by row order");
        });
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
}
