using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

#pragma warning disable EF1001 // EF's temporal query roots, as PostgresTemporalQueries takes them.

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>PostgresTemporalQueriesTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class PostgresTemporalQueriesUnitTests
{
    internal static readonly DateTime A = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Literal_IsAUtcTimestamptz_ToTheMicrosecond()
    {
        var time = A.AddTicks(1234567);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(PostgresTemporalQueries.Literal(time), Is.EqualTo("'2026-10-01 12:00:00.123456+00'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(time, roundUp: true), Is.EqualTo("'2026-10-01 12:00:00.123457+00'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(A, roundUp: true), Is.EqualTo("'2026-10-01 12:00:00.000000+00'::timestamptz"), "a whole microsecond stays");
            Assert.That(PostgresTemporalQueries.Literal(DateTime.SpecifyKind(time, DateTimeKind.Unspecified)), Is.EqualTo(PostgresTemporalQueries.Literal(time)));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MaxValue), Is.EqualTo("'infinity'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MinValue), Is.EqualTo("'-infinity'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MaxValue.AddTicks(-1)), Is.EqualTo("'9999-12-31 23:59:59.999999+00'::timestamptz"));
            Assert.That(PostgresTemporalQueries.Literal(DateTime.MaxValue.AddTicks(-1), roundUp: true), Is.EqualTo("'infinity'::timestamptz"));
        }
    }

    [Test]
    public void Literal_RefusesLocalTimes()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PostgresTemporalQueries.Literal(DateTime.Now));

        Assert.That(ex!.Message, Does.Contain("Kind Local"));
    }

    [Test]
    public void Instant_IsAUtcTime_ToTheMicrosecond_TheEndsOfTimeKept()
    {
        var time = A.AddTicks(1234567);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(PostgresTemporalQueries.Instant(time), Is.EqualTo(A.AddTicks(1234560)));
            Assert.That(PostgresTemporalQueries.Instant(time).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(PostgresTemporalQueries.Instant(time, roundUp: true), Is.EqualTo(A.AddTicks(1234570)));
            Assert.That(PostgresTemporalQueries.Instant(A, roundUp: true), Is.EqualTo(A), "a whole microsecond stays");
            Assert.That(PostgresTemporalQueries.Instant(DateTime.SpecifyKind(time, DateTimeKind.Unspecified)).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MaxValue), Is.EqualTo(DateTime.MaxValue));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MinValue), Is.EqualTo(DateTime.MinValue));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MaxValue.AddTicks(-1), roundUp: true), Is.EqualTo(DateTime.MaxValue));
            Assert.That(PostgresTemporalQueries.Instant(DateTime.MaxValue.AddTicks(-1), roundUp: true).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.Throws<InvalidOperationException>(() => PostgresTemporalQueries.Instant(DateTime.Now));
        }
    }
}
