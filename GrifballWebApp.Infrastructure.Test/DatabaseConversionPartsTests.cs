using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.EntityFrameworkCore.Metadata;
using static GrifballWebApp.Infrastructure.Test.DatabaseConversionPartsTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The conversion's parts that need no database and no EF model: values, fingerprints, the order of rows.</summary>
[TestFixture]
public class DatabaseConversionPartsTests
{
    private static readonly int[] ExpectedOrdered = [2, 3, 1];

    internal static readonly TableName Seasons = new("Event", "Seasons");
    private static readonly TableName Users = new("Auth", "Users");
    internal static readonly TableName Matches = new("Infinite", "Matches");
    internal static readonly TableName Stats = new("Event", "PlayerSeasonStats");

    // Each value as EF stores it on SQLite, from whichever server read it (SqliteColumns).
    [Test]
    public void Sqlite_WritesEachValue_AsEFStoresIt()
    {
        var instant = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_500);
        var guid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        var columns = SqliteColumns.Instance;

        using (Assert.EnterMultipleScope())
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
            Assert.That(columns.Write(18446744073709551615m, new TableName("Event", "MatchReschedules"), "DiscordThreadID"), Is.EqualTo(ulong.MaxValue), "a ulong, as the others keep it (a decimal)");
            Assert.That(columns.Write(new TimeOnly(19, 30), new TableName("Event", "AvailabilityOptions"), "Time"), Is.EqualTo("19:30:00"));
            Assert.That(columns.Write(new TimeSpan(19, 30, 15), new TableName("Event", "AvailabilityOptions"), "Time"), Is.EqualTo("19:30:15"), "SQL Server's time, as a TimeSpan");
            Assert.That(columns.Write(new DateOnly(2026, 11, 1), new TableName("", "Nowhere"), "Date"), Is.EqualTo("2026-11-01"));
            Assert.That(columns.Write("text", new TableName("", "Nowhere"), "Text"), Is.EqualTo("text"));
        }
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
    public void Fingerprint_StreamsLargeValues_TellingTextBytesAndChunkBoundariesApart()
    {
        // Longer than a chunk (8192 chars), a surrogate pair across the boundary, and the archive-sized value the copy
        // check ran out of memory on as one string a row
        var text = new string('a', 8191) + "\U0001F600" + new string('b', 20000);
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var large = new string('x', 64 * 1024 * 1024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableFingerprint.Of([[text]]), Is.EqualTo(TableFingerprint.Of([[new string(text.AsSpan())]])), "the same text");
            Assert.That(TableFingerprint.Of([[text]]), Is.Not.EqualTo(TableFingerprint.Of([[text[..^1] + "c"]])), "a change at the end");
            Assert.That(TableFingerprint.Of([[text]]), Is.Not.EqualTo(TableFingerprint.Of([[bytes]])), "text isn't its bytes");
            Assert.That(TableFingerprint.Of([[bytes]]), Is.EqualTo(TableFingerprint.Of([[(byte[])bytes.Clone()]])));
            Assert.That(TableFingerprint.Of([[large], [large]]).Rows, Is.EqualTo(2));
        }
    }

    [Test]
    public void Fingerprint_TellsRowsApart_InAnyOrder()
    {
        object?[] a = ["a", 1, null];
        object?[] b = ["b", 2, true];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableFingerprint.Of([a, b]), Is.EqualTo(TableFingerprint.Of([b, a])));
            Assert.That(TableFingerprint.Of([a, b]), Is.Not.EqualTo(TableFingerprint.Of([a, a])));
            Assert.That(TableFingerprint.Of([["A"]]), Is.Not.EqualTo(TableFingerprint.Of([["a"]])), "case counts");
            Assert.That(TableFingerprint.Of([["a", "b"]]), Is.Not.EqualTo(TableFingerprint.Of([["a\u001eb"]])));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableFingerprint.Canonical(new byte[] { 0, 255 }), Is.EqualTo("x00FF"));
            Assert.That(TableFingerprint.Canonical(Guid.Empty), Is.EqualTo("g00000000-0000-0000-0000-000000000000"));
            Assert.That(TableFingerprint.Canonical(DateTime.MinValue), Is.EqualTo("-infinity"));
            Assert.That(TableFingerprint.Canonical(DateTimeOffset.MinValue), Is.EqualTo("-infinity"));
            Assert.That(TableFingerprint.Canonical(new TimeOnly(1, 2, 3)), Is.EqualTo(TableFingerprint.Canonical(new TimeSpan(1, 2, 3))));
            Assert.That(() => TableFingerprint.Canonical(new Uri("https://example.com")), Throws.TypeOf<ConversionException>());
        }
    }

    [Test]
    public void ToPostgres_MakesInstantsUtc_ToTheMicrosecond_AndKeepsTheEndsOfTime()
    {
        var instant = new DateTime(2026, 10, 10, 12, 0, 0).AddTicks(1_234_567);
        var column = Column("At", "timestamptz");

        var plain = (DateTime)PostgresTargetWriter.ToPostgres(instant, column)!;
        var offset = (DateTime)PostgresTargetWriter.ToPostgres(new DateTimeOffset(instant.AddHours(2), TimeSpan.FromHours(2)), column)!;
        using (Assert.EnterMultipleScope())
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
        }
    }

    [Test]
    public void ToSqlServer_TakesPostgresInstantsAndTheEndsOfTime()
    {
        var instant = DateTime.SpecifyKind(new DateTime(2026, 10, 10, 12, 0, 0), DateTimeKind.Utc);
        var offset = Column("LockoutEnd", "datetimeoffset");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SqlServerTargetWriter.ToSqlServer(instant, offset), Is.EqualTo(new DateTimeOffset(instant)));
            Assert.That(SqlServerTargetWriter.ToSqlServer(DateTime.MaxValue, offset), Is.EqualTo(DateTimeOffset.MaxValue));
            Assert.That(SqlServerTargetWriter.ToSqlServer(DateTime.MinValue, offset), Is.EqualTo(DateTimeOffset.MinValue));
            Assert.That(SqlServerTargetWriter.ToSqlServer(new DateTimeOffset(instant), Column("At", "datetime2")), Is.EqualTo(instant));
            Assert.That(((DateTime)SqlServerTargetWriter.ToSqlServer(instant, Column("At", "datetime2"))!).Kind, Is.EqualTo(DateTimeKind.Unspecified));
            Assert.That(SqlServerTargetWriter.ToSqlServer(new TimeOnly(19, 30), Column("Time", "time")), Is.EqualTo(new TimeSpan(19, 30, 0)));
            Assert.That(SqlServerTargetWriter.ToSqlServer(5, Column("N", "int")), Is.EqualTo(5));
        }
    }

    private static TablePlan Bracket() => new(new TableName("Event", "MatchBracketInfo"),
        [Column("ID", "int"), Column("PreviousID", "int"), Column("CaptainID", "int")],
        key: [0], selfReferences: [([1], [0])], deferred: [2]);

    [Test]
    public void OrderForInsert_PutsTheRowsARowRefersToBeforeIt()
    {
        List<object?[]> rows = [[1, 3, null], [2, null, null], [3, 2, null]];

        var ordered = Bracket().OrderForInsert(rows).Select(r => (int)r[0]!);

        Assert.That(ordered, Is.EqualTo(ExpectedOrdered));
    }

    private static readonly int[] SelfReferringOrdered = [1, 2];

    [Test]
    public void OrderForInsert_TakesARowThatRefersToItselfAsItIs()
    {
        List<object?[]> rows = [[1, 1, null], [2, 1, null]];

        Assert.That(Bracket().OrderForInsert(rows).Select(r => (int)r[0]!), Is.EqualTo(SelfReferringOrdered));
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.WithoutDeferred(withCaptain), Is.EqualTo(new object?[] { 1, null, null }));
            Assert.That(withCaptain[2], Is.EqualTo(7), "the row read is left as it was");
            Assert.That(plan.WithDeferred([withCaptain, without]), Is.EqualTo(new[] { withCaptain }));
        }
    }

    [TestCase("QRTZ_TRIGGERS", true)]
    [TestCase("qrtz_triggers", true)]
    [TestCase("QRTZ_EXECUTION_HISTORY", true)]
    [TestCase("Seasons", false)]
    [TestCase("QrtzLike", false)]
    public void IsJobStore_IsQuartzsTables_InEitherCase(string name, bool jobStore)
    {
        Assert.That(ConversionTables.IsJobStore(new TableName("", name)), Is.EqualTo(jobStore));
    }
}

/// <summary>DatabaseConversionPartsTests' helpers, shared with its integration tests.</summary>
internal static class DatabaseConversionPartsTestData
{
    internal static ColumnInfo Column(string name, string type) => new(name, type, Generated: false, Identity: false);
}
