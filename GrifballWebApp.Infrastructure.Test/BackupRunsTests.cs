using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Infrastructure.Test;

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

    [Test]
    public void Text_IsTheString_WhenItIsOne()
        => Assert.That(BackupRuns.Text(BackupRuns.ParseMetrics("""{"format":"dump"}"""), BackupRuns.FormatMetric), Is.EqualTo("dump"));

    [TestCase("{}", Description = "missing")]
    [TestCase("""{"format":42}""", Description = "not a string")]
    public void Text_IsNull_WhenItIsntAString(string json)
        => Assert.That(BackupRuns.Text(BackupRuns.ParseMetrics(json), BackupRuns.FormatMetric), Is.Null);

    [Test]
    public void Number_IsTheCount_WhenOneWasMade()
        => Assert.That(BackupRuns.Number(BackupRuns.ParseMetrics("""{"rows":7}"""), BackupRuns.RowsMetric), Is.EqualTo(7));

    [TestCase("{}", Description = "missing")]
    [TestCase("""{"rows":"many"}""", Description = "not a number")]
    public void Number_IsZero_WhenNeverMeasured(string json)
        => Assert.That(BackupRuns.Number(BackupRuns.ParseMetrics(json), BackupRuns.RowsMetric), Is.Zero);

    [Test]
    public void ParseMetrics_IsEmpty_ForNoMetricsJson()
        => Assert.That(BackupRuns.ParseMetrics(null), Is.Empty);
}
