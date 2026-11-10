using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>BackupDownloadsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class BackupDownloadsUnitTests
{
    [Test]
    public void A_runs_metrics_read_what_is_there_and_nothing_else()
    {
        var metrics = BackupRuns.ParseMetrics("{\"fileName\":\"b.sql\",\"tables\":3,\"flag\":true}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BackupRuns.Text(metrics, "fileName"), Is.EqualTo("b.sql"));
            Assert.That(BackupRuns.Text(metrics, "tables"), Is.Null, "not text");
            Assert.That(BackupRuns.Text(metrics, "missing"), Is.Null);
            Assert.That(BackupRuns.Number(metrics, "tables"), Is.EqualTo(3));
            Assert.That(BackupRuns.Number(metrics, "fileName"), Is.Zero, "not a number");
            Assert.That(BackupRuns.Number(metrics, "missing"), Is.Zero);
            Assert.That(BackupRuns.ParseMetrics(null), Is.Empty);
            Assert.That(BackupRuns.ParseMetrics(""), Is.Empty);
            Assert.That(BackupRuns.ParseMetrics("null"), Is.Empty);
        }
    }
}
