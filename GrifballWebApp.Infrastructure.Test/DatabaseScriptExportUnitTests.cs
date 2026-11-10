using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using static GrifballWebApp.Infrastructure.Test.DatabaseScriptExportTestData;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>DatabaseScriptExportTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DatabaseScriptExportUnitTests
{
    [Test]
    public async Task Is_for_the_servers_only()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DatabaseScriptExporter.Supports(DatabaseProvider.SqlServer), Is.True);
            Assert.That(DatabaseScriptExporter.Supports(DatabaseProvider.Postgres), Is.True);
            Assert.That(DatabaseScriptExporter.Supports(DatabaseProvider.Sqlite), Is.False);
        }
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Exporter().WriteAsync(
            new DatabaseEndpoint(DatabaseProvider.Sqlite, "Data Source=unused"), DatabaseProvider.Sqlite, TextWriter.Null));
    }

    [Test]
    public void SqlServer_keeps_every_literal_on_one_line()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SqlServerScript.Unicode("it's"), Is.EqualTo("N'it''s'"));
            Assert.That(SqlServerScript.Unicode("a\r\nGO\nb"),
                Is.EqualTo("CONVERT(nvarchar(max), N'a' + NCHAR(13) + N'' + NCHAR(10) + N'GO' + NCHAR(10) + N'b')"));
        }
    }

    [Test]
    public void Literals_as_each_server_reads_them()
    {
        var sqlServer = new SqlServerScript();
        var postgres = new PostgresScript();
        ColumnInfo Column(string type) => new("C", type, Generated: false, Identity: false);
        var instant = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_567);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sqlServer.Literal(instant, Column("datetime2")), Is.EqualTo("'2026-10-10T12:00:00.1234567'"));
            Assert.That(sqlServer.Literal(instant, Column("datetime")), Is.EqualTo("'2026-10-10T12:00:00.123'"));
            Assert.That(sqlServer.Literal(instant, Column("datetimeoffset")), Is.EqualTo("'2026-10-10T12:00:00.1234567+00:00'"));
            Assert.That(sqlServer.Literal(DateTime.MaxValue, Column("datetime2")), Is.EqualTo("'9999-12-31T23:59:59.9999999'"));
            Assert.That(sqlServer.Literal(true, Column("bit")), Is.EqualTo("1"));
            Assert.That(sqlServer.Literal(new byte[] { 0xDE, 0xAD }, Column("varbinary(max)")), Is.EqualTo("0xDEAD"));
            Assert.That(sqlServer.Literal(TimeSpan.FromHours(20.5), Column("time")), Is.EqualTo("'20:30:00.0000000'"));
            Assert.That(sqlServer.Literal(new TimeOnly(9, 15), Column("time")), Is.EqualTo("'09:15:00.0000000'"));
            Assert.That(sqlServer.Literal(new DateOnly(2026, 1, 2), Column("date")), Is.EqualTo("'2026-01-02'"));
            Assert.That(sqlServer.Literal(1.5f, Column("real")), Is.EqualTo("1.5"));
            Assert.That(sqlServer.Literal(0.25, Column("float")), Is.EqualTo("0.25"));
            Assert.That(sqlServer.Literal(2535405290000001L, Column("bigint")), Is.EqualTo("2535405290000001"));
            Assert.That(sqlServer.Literal(12.345m, Column("decimal(6, 3)")), Is.EqualTo("12.345"));
            Assert.That(sqlServer.Literal('x', Column("nchar(1)")), Is.EqualTo("N'x'"));
            Assert.That(sqlServer.Literal(null, Column("int")), Is.EqualTo("NULL"));

            Assert.That(postgres.Literal(instant, Column("timestamp with time zone")), Is.EqualTo("'2026-10-10 12:00:00.123456+00'"));
            Assert.That(postgres.Literal(DateTime.MaxValue, Column("timestamp with time zone")), Is.EqualTo("'infinity'"));
            Assert.That(postgres.Literal(DateTime.MinValue, Column("timestamp with time zone")), Is.EqualTo("'-infinity'"));
            Assert.That(postgres.Literal(42, Column("integer")), Is.EqualTo("42"));
            Assert.That(postgres.Literal(12.345m, Column("numeric")), Is.EqualTo("12.345"));
            Assert.That(postgres.Literal(false, Column("boolean")), Is.EqualTo("FALSE"));
            Assert.That(postgres.Literal("it's \\ not", Column("text")), Is.EqualTo("'it''s \\ not'"));
            Assert.That(postgres.Literal(new byte[] { 0xBE, 0xEF }, Column("bytea")), Is.EqualTo("'\\xBEEF'"));
            Assert.That(postgres.Literal(new TimeSpan(1, 2, 3, 4, 500), Column("interval")), Is.EqualTo("'P1DT2H3M4.5S'"));
            Assert.That(postgres.Literal(-TimeSpan.FromMinutes(90), Column("interval")), Is.EqualTo("'-P0DT1H30M0S'"));
            Assert.That(postgres.Literal(TimeSpan.FromHours(20.5), Column("time without time zone")), Is.EqualTo("'20:30:00.000000'"));
            Assert.That(postgres.Literal(new TimeOnly(9, 15), Column("time without time zone")), Is.EqualTo("'09:15:00.000000'"));
            Assert.That(postgres.Literal(new DateOnly(2026, 1, 2), Column("date")), Is.EqualTo("'2026-01-02'"));
            Assert.That(postgres.Literal(double.NaN, Column("double precision")), Is.EqualTo("'NaN'"));
            Assert.That(postgres.Literal(double.NegativeInfinity, Column("double precision")), Is.EqualTo("'-Infinity'"));
            Assert.That(postgres.Literal(double.PositiveInfinity, Column("double precision")), Is.EqualTo("'Infinity'"));
            Assert.That(postgres.Literal(float.NegativeInfinity, Column("real")), Is.EqualTo("'-Infinity'"));
            Assert.That(postgres.Literal(float.PositiveInfinity, Column("real")), Is.EqualTo("'Infinity'"));
            Assert.That(postgres.Literal(float.NaN, Column("real")), Is.EqualTo("'NaN'"));
            Assert.That(postgres.Literal(2.25, Column("double precision")), Is.EqualTo("2.25"));
            Assert.That(postgres.Literal(Guid.Empty, Column("uuid")), Is.EqualTo("'00000000-0000-0000-0000-000000000000'"));
            Assert.That(postgres.Literal('x', Column("character(1)")), Is.EqualTo("'x'"));
        }
    }
}

/// <summary>DatabaseScriptExportTests' helpers, shared with its integration tests.</summary>
internal static class DatabaseScriptExportTestData
{
    internal static DatabaseScriptExporter Exporter() => new(NullLogger<DatabaseConverter>.Instance);
}
