using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Integration.Test;

/// <summary>A backup run's metrics, as the page's status reads them back.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class BackupRunsTests
{
    [Test]
    public void OptionalNumber_IsTheCount_WhenOneWasMade()
        => Assert.That(BackupRuns.OptionalNumber(BackupRuns.ParseMetrics("""{"tables":42}"""), BackupRuns.TablesMetric), Is.EqualTo(42));

    [TestCase("{}", Description = "a .bak or .dump: never counted")]
    [TestCase("""{"rows":"many"}""", Description = "not a number")]
    public void OptionalNumber_IsNull_WhenNothingWasCounted(string json)
        => Assert.That(BackupRuns.OptionalNumber(BackupRuns.ParseMetrics(json), BackupRuns.RowsMetric), Is.Null);
}
